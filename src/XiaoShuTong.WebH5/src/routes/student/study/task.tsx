import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { useState, useEffect, useRef, useCallback } from 'react';
import { useAppStore } from '@/store/appStore';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { TaskProgressBar } from '@/components/TaskProgressBar';
import { MemoryStateBadge } from '@/components/MemoryStateBadge';
import { QuestionCard } from '@/components/QuestionCard';
import { useAttemptFlow } from '@/hooks/use-attempt-flow';
import { Tkwf } from '@tkwf/tsclient';
import type { GetNextQuestionResDto, SessionQuestion_ExecuteService, Hint_ExecuteService, EndStudySession_ExecuteService } from '@/gql/ts-client.g';
import { serverStateToMemoryState } from '@/lib/memory-state';
import {
  ArrowLeft,
  Lightbulb,
  CheckCircle2,
  XCircle,
  RotateCcw,
  ChevronRight,
  Sparkles,
  Loader2,
  BookOpen
} from 'lucide-react';
import { cn } from '@/lib/utils';

// 默认题库（task 未关联题库时的兜底，来源：MOCK_SPEC listBanksResDtos.bank-001）
const DEFAULT_BANK_ID = 'bank-001';
// 会话期望题数兜底（createStudySession 请求必需；响应 questionCount 为权威值）
const DEFAULT_QUESTION_COUNT = 10;

// 求助档位：None → Partial → Full（回填 submitAttempt.hintLevel；Full 时后端 BR-16 showAnswer 早退）
type HintLevel = 'None' | 'Partial' | 'Full';

// hintLevel → GetHint difficultySlot 映射（后端仅允许 S1/S2/S3，BR-30 状态越低提示越深）
// None（首次求助，未掌握）→ S3 最深 / Partial（二次求助，模糊）→ S2 / Full（三次求助）→ S1 最浅
const HINT_LEVEL_TO_DIFFICULTY_SLOT: Record<HintLevel, string> = {
  None: 'S3',
  Partial: 'S2',
  Full: 'S1',
};

// 判题反馈四态（决策表 §4.2 + §4.3；hook 已降维，本组件仅渲染分支）
type FeedbackState =
  | { type: 'celebrate'; postState: string; matchedKeywords: string[] }
  | { type: 'guidance'; hint: string; attemptCount: number; maxAttempts: number }
  | { type: 'answer-sheet'; matchedKeywords: string[]; missingKeywords: string[]; postState: string }
  | { type: 'error'; message: string }
  | null;

// 会话初始化状态（含加载失败 / 无参数空态兜底）
type InitStatus = 'idle' | 'loading' | 'error' | 'guide' | 'ready';

export const Route = createFileRoute('/student/study/task')({
  validateSearch: (search: Record<string, unknown>): { taskId?: string; reviewQuestionId?: string } => ({
    taskId: typeof search?.taskId === 'string' ? search.taskId : undefined,
    reviewQuestionId: typeof search?.reviewQuestionId === 'string' ? search.reviewQuestionId : undefined,
  }),
  component: TaskStudyPage,
});

function TaskStudyPage() {
  const navigate = useNavigate();
  const search = Route.useSearch();
  const {
    isLoggedIn,
    currentTaskId,
    tasks,
    sessionUid,
    questionCount,
    currentQuestionIndex,
    startSession,
    recordAnswer,
    nextQuestion,
  } = useAppStore();

  const { submitAttempt, isSubmitting: isSubmittingAttempt, error: submitError } = useAttemptFlow();

  const [hintLevel, setHintLevel] = useState<HintLevel>('None');
  const [serverHint, setServerHint] = useState<string | null>(null);
  const [question, setQuestion] = useState<GetNextQuestionResDto | null>(null);
  const [feedback, setFeedback] = useState<FeedbackState>(null);
  const [showNextButton, setShowNextButton] = useState(false);
  const [initStatus, setInitStatus] = useState<InitStatus>('idle');
  const [degradedNotice, setDegradedNotice] = useState(false);
  const [questionKey, setQuestionKey] = useState(0); // QuestionCard 重挂载键（下一题/重试清空输入）
  const inputRef = useRef<HTMLInputElement>(null);
  const cancelledRef = useRef(false);
  const bankIdRef = useRef<string>(DEFAULT_BANK_ID);
  const questionStartRef = useRef<number>(Date.now());
  const nextTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const endedRef = useRef(false); // 防止 endStudy fire-and-forget 重复调用

  // 结束会话（T9：题集耗尽/中途退出 → endStudySession_Execute 写 EndedAt；fire-and-forget，幂等后端）
  const endStudy = useCallback(async (endReason: 'NaturalExhaust' | 'UserExit' = 'UserExit') => {
    if (!sessionUid || endedRef.current) return;
    endedRef.current = true;
    try {
      await Tkwf.User.Use<EndStudySession_ExecuteService>().endStudySession_Execute({
        request: { sessionUid, endReason },
      });
    } catch {
      // fire-and-forget：失败不阻塞跳转（后端幂等 + BR-05 续做兜底，悬挂会话可继续）
    }
  }, [sessionUid]);

  // 检查登录状态
  useEffect(() => {
    if (!isLoggedIn) {
      navigate({ to: '/auth/login' });
    }
  }, [isLoggedIn, navigate]);

  // 任务关联（新学会话：search.taskId 优先，其次 store.currentTaskId）
  const effectiveTaskId = search.taskId ?? currentTaskId;
  const currentTask = tasks.find(t => t.id === effectiveTaskId);

  // 会话初始化（T5）：createStudySession → sessionUid 存 store → 首次 getSessionQuestion 取第 1 题
  // 归口为 useCallback：错误态"重试"直接复调，无需 trigger 计数器（会话重建走 BR-05 幂等）
  const initSession = useCallback(async () => {
    // 无参数进入（无 taskId / reviewQuestionId / currentTaskId）→ 任务选择引导（验收 B1）
    if (!search.taskId && !search.reviewQuestionId && !currentTaskId) {
      setInitStatus('guide');
      return;
    }
    setInitStatus('loading');
    try {
      const isReview = Boolean(search.reviewQuestionId);
      const bankId = currentTask?.bankId ?? DEFAULT_BANK_ID;
      bankIdRef.current = bankId;
      const result = await startSession(bankId, {
        scenario: isReview ? 'Assess' : 'Memorize', // 复习=Assess、新学=Memorize
        sessionType: 'Progressive',
        taskId: search.taskId ? Number(search.taskId) || null : null,
        questionCount: currentTask?.totalQuestions ?? DEFAULT_QUESTION_COUNT,
      });
      if (cancelledRef.current) return;

      const firstQuestion = result.question;
      // 首次取题失败（BankNotFound / Forbidden 等）→ 错误态卡片（Oracle #7 修订）
      if (!firstQuestion.success) {
        setInitStatus('error');
        return;
      }
      // 题集耗尽（BR-18：success=true 且 questionId 为空）→ 先 endStudy 再直接结果页
      if (!firstQuestion.questionId) {
        await endStudy('NaturalExhaust');
        navigate({ to: '/student/study/result' });
        return;
      }
      setQuestion(firstQuestion);
      questionStartRef.current = Date.now();
      setInitStatus('ready');
    } catch {
      setInitStatus('error');
    }
  }, [search.taskId, search.reviewQuestionId, currentTaskId, currentTask?.bankId, currentTask?.totalQuestions, startSession, navigate, endStudy]);

  useEffect(() => {
    void initSession();
    return () => {
      cancelledRef.current = true;
      if (nextTimerRef.current) clearTimeout(nextTimerRef.current);
      // 中途退出：fire-and-forget 结束会话（beforeunload 移动端受限；失败由 BR-05 续做兜底）
      void endStudy('UserExit');
    };
  }, [initSession, endStudy]);

  // 自动聚焦输入框
  useEffect(() => {
    if (inputRef.current && !feedback && initStatus === 'ready') {
      inputRef.current.focus();
    }
  }, [feedback, initStatus]);

  // 取下一题（T6：celebrate / answer-sheet 后调用；也供错误态重试）
  const fetchNextQuestion = async (): Promise<'ok' | 'exhausted' | 'error'> => {
    if (!sessionUid) return 'error';
    try {
      const res = await Tkwf.User.Use<SessionQuestion_ExecuteService>().sessionQuestion_Execute({
        request: { sessionUid, bankId: bankIdRef.current, type: null, knowledgePoint: null },
      });
      if (!res.success) return 'error';
      // 题集耗尽（BR-18：success=true 无 questionId）= 会话结束信号
      if (!res.questionId) return 'exhausted';
      setQuestion(res);
      nextQuestion(); // 服务端已答排除（BR-18/GetNextQuestion Callee），本地索引跟随推进
      questionStartRef.current = Date.now();
      setQuestionKey((k) => k + 1); // 重挂 QuestionCard 清空输入
      setHintLevel('None');
      setServerHint(null);
      setFeedback(null);
      setShowNextButton(false);
      setDegradedNotice(false);
      return 'ok';
    } catch {
      return 'error';
    }
  };

  const goNext = async () => {
    const result = await fetchNextQuestion();
    if (result === 'exhausted') {
      await endStudy('NaturalExhaust');
      navigate({ to: '/student/study/result' });
    } else if (result === 'error') {
      setFeedback({ type: 'error', message: '下一题加载失败，请重试' });
    }
  };

  // 判题接线（T6）：useAttemptFlow 四态驱动，替换本地 includes() 判题
  // QuestionCard 分发各题型输入 → answer 字符串（R 系列文本 / O 系列选中标识 "B" / "A,B,D"）
  const handleSubmit = async (answer: string) => {
    if (!question || !sessionUid || isSubmittingAttempt) return;
    const trimmed = answer.trim();
    if (!trimmed) return;

    const { action, response } = await submitAttempt({
      sessionUid,
      questionId: question.questionId,
      userAnswer: trimmed,
      hintLevel,
      timeCostMs: Math.max(0, Date.now() - questionStartRef.current),
    });

    // 原始响应透传：答案记录扩展为服务端字段（result/preState/postState/needsGuidance/...）
    if (response) recordAnswer(question.questionId, response);
    if (response?.isDegraded) setDegradedNotice(true);

    switch (action.type) {
      case 'celebrate':
        setFeedback({
          type: 'celebrate',
          postState: response?.postState ?? '',
          matchedKeywords: action.matchedKeywords,
        });
        setShowNextButton(false);
        // 1.5s 后出"下一题"按钮（delay 归调用方，hook 已注明）
        nextTimerRef.current = setTimeout(() => setShowNextButton(true), 1500);
        break;
      case 'guidance':
        // 停当前题：橙色△ + hint + 鼓励 → 「再试一次」+「求助升级」（T7）
        setFeedback({
          type: 'guidance',
          hint: action.hint,
          attemptCount: action.attemptCount,
          maxAttempts: action.maxAttempts,
        });
        break;
      case 'answer-sheet':
        // 展示 matched+missing 合集（matched 标绿 / missing 标红 = 完整答案要点，Oracle #5）→ 必进下一题
        setFeedback({
          type: 'answer-sheet',
          matchedKeywords: response?.matchedKeywords ?? [],
          missingKeywords: action.missingKeywords,
          postState: response?.postState ?? '',
        });
        nextTimerRef.current = setTimeout(() => { void goNext(); }, 1500);
        break;
      case 'error':
        // 停留当前题（action.code/message 提示；全局 onUnauthorized/onGlobalError 已兜底）
        setFeedback({ type: 'error', message: action.message ?? action.code ?? '提交失败，请稍后重试' });
        break;
    }
  };

  const handleNext = () => {
    void goNext();
  };

  // 再试一次（guidance / error 分支）：清反馈、保持当前题与 hintLevel 档位
  const handleRetry = () => {
    if (nextTimerRef.current) clearTimeout(nextTimerRef.current);
    setFeedback(null);
    setShowNextButton(false);
    setDegradedNotice(false);
    setQuestionKey((k) => k + 1); // 重挂 QuestionCard 清空输入
    questionStartRef.current = Date.now();
    if (inputRef.current) inputRef.current.focus();
  };

  // 求助升级（T7）：None → Partial → Full 递增，取服务端 hint（BR-29 ≤20 字）
  const handleHint = async () => {
    if (!question || hintLevel === 'Full') return;
    const nextLevel: Exclude<HintLevel, 'None'> = hintLevel === 'None' ? 'Partial' : 'Full';
    try {
      const res = await Tkwf.User.Use<Hint_ExecuteService>().hint_Execute({
        request: { questionId: question.questionId, difficultySlot: HINT_LEVEL_TO_DIFFICULTY_SLOT[nextLevel] },
      });
      if (res.success) {
        setServerHint(res.hint || '再想想，回忆下要点'); // R1：空 hint → 通用鼓励语兜底
        setHintLevel(nextLevel); // 仅成功升档（失败不升级，避免 BR-16 误触发）
      } else {
        setServerHint('再想想，回忆下要点');
      }
    } catch {
      setServerHint('再想想，回忆下要点');
    }
  };

  // ── 无参数进入：任务选择引导 ──
  if (initStatus === 'guide') {
    return (
      <div className="min-h-screen bg-background flex items-center justify-center p-4">
        <Card className="p-8 max-w-sm w-full text-center">
          <div className="text-5xl mb-4">📚</div>
          <h2 className="font-semibold text-lg mb-2">请从任务中心或复习队列进入</h2>
          <p className="text-sm text-muted-foreground mb-6">
            作答会话需要关联任务或复习知识点，先去任务中心选一个任务开始吧。
          </p>
          <Button onClick={() => navigate({ to: '/student/home' })} className="w-full h-12">
            <BookOpen className="mr-2 h-4 w-4" />
            返回首页
          </Button>
        </Card>
      </div>
    );
  }

  // ── 加载中 ──
  if (initStatus === 'loading') {
    return (
      <div className="min-h-screen bg-background flex items-center justify-center">
        <Loader2 className="w-8 h-8 animate-spin text-primary" />
      </div>
    );
  }

  // ── 题目加载失败（首次 getSessionQuestion 失败 / 网络错误）→ 错误态卡片 + 重试 ──
  if (initStatus === 'error') {
    return (
      <div className="min-h-screen bg-background flex flex-col items-center justify-center p-4">
        <Card className="p-8 max-w-sm w-full text-center">
          <XCircle className="w-12 h-12 text-destructive mx-auto mb-4" />
          <h2 className="font-semibold text-lg mb-2">题目加载失败</h2>
          <p className="text-sm text-muted-foreground mb-6">网络开小差了，点重试再试一次。</p>
          <Button
            onClick={() => void initSession()}
            className="w-full h-12"
            size="lg"
          >
            <RotateCcw className="mr-2 h-4 w-4" />
            重试
          </Button>
        </Card>
      </div>
    );
  }

  // ── 就绪：题目区 + 答题区 + 反馈区 ──
  return (
    <div className="min-h-screen bg-background">
      {/* 顶部导航 */}
      <header className="sticky top-0 z-10 bg-card border-b border-border px-4 py-3">
        <div className="flex items-center gap-3">
          <button
            type="button"
            onClick={() => {
              void endStudy('UserExit'); // 中途退出：fire-and-forget 结束会话
              navigate({ to: '/student/home' });
            }}
            className="p-2 -ml-2 rounded-full hover:bg-muted transition-colors"
          >
            <ArrowLeft className="w-5 h-5" />
          </button>
          <div className="flex-1 min-w-0">
            <h1 className="font-semibold text-sm truncate">
              {currentTask?.title ?? (search.reviewQuestionId ? '复习会话' : '学习任务')}
            </h1>
            <div className="flex items-center gap-2 text-xs text-muted-foreground">
              {currentTask && <span>{currentTask.teacherName}</span>}
              {currentTask && <span>·</span>}
              {/* 第 x/y 题分母 = 真实会话题数（createStudySession.questionCount），非 MOCK_QUESTIONS.length */}
              <span>第 {currentQuestionIndex + 1}/{questionCount || 1} 题</span>
            </div>
          </div>
        </div>

        {/* 进度条 */}
        <div className="mt-3">
          <TaskProgressBar
            current={currentQuestionIndex + (feedback?.type === 'celebrate' ? 1 : 0)}
            total={questionCount || 1}
            size="sm"
            showText={false}
          />
        </div>
      </header>

      {/* 题目区 */}
      <div className="p-4">
        <Card className="p-6 min-h-[200px] flex flex-col items-center justify-center">
          {/* 提示按钮（T7：None→Partial→Full 逐级加深） */}
          {!feedback && hintLevel !== 'Full' && question && (
            <button
              type="button"
              onClick={handleHint}
              className="mb-4 text-xs text-muted-foreground flex items-center gap-1 hover:text-primary transition-colors self-start"
            >
              <Lightbulb className="w-3 h-3" />
              {hintLevel === 'None' ? '想不起来？' : '再给点提示'}
            </button>
          )}

          {/* 提示内容（服务端 hint，≤20 字 BR-29） */}
          {serverHint && !feedback && (
            <div className="mb-4 p-3 bg-amber-50 border border-amber-200 rounded-lg self-start">
              <p className="text-sm text-amber-800 flex items-start gap-2">
                <Lightbulb className="w-4 h-4 mt-0.5 flex-shrink-0" />
                {serverHint}
              </p>
            </div>
          )}

          {/* 题目 + 答题（QuestionCard 按题型分发：R1/R2 文本、R3a/R3b textarea、O1 单选、O2 多选、
              O3 对错双键、O5 填空、R4 展示；含语音输入 use-voice-input） */}
          {question && (
            <QuestionCard
              key={questionKey}
              type={question.type}
              content={question.content}
              onSubmit={handleSubmit}
              submitting={isSubmittingAttempt}
              inputRef={inputRef}
            />
          )}
        </Card>

        {/* 域级失败（success=false / RPC 抛错）→ 停留当前题，提示 + 再试（T6 error 分支） */}
        {submitError && !feedback && (
          <p className="text-center text-xs text-destructive mt-4">
            {submitError}，请再试一次
          </p>
        )}

        {/* 反馈区（四态渲染） */}
        {feedback && (
          <div className="mt-6 space-y-4">
            <Card
              className={cn(
                'p-6 text-center',
                feedback.type === 'celebrate' && 'bg-green-50 border-green-200',
                feedback.type === 'guidance' && 'bg-amber-50 border-amber-200',
                (feedback.type === 'answer-sheet' || feedback.type === 'error') && 'bg-red-50 border-red-200'
              )}
            >
              {feedback.type === 'celebrate' && (
                <div className="animate-star-pop inline-block mb-3">
                  <CheckCircle2 className="w-12 h-12 text-green-500" />
                </div>
              )}
              {feedback.type === 'guidance' && (
                <div className="inline-block mb-3">
                  <span className="text-3xl">△</span>
                </div>
              )}
              {feedback.type === 'answer-sheet' && (
                <div className="inline-block mb-3">
                  <XCircle className="w-12 h-12 text-red-500" />
                </div>
              )}
              {feedback.type === 'error' && (
                <div className="inline-block mb-3">
                  <XCircle className="w-12 h-12 text-destructive" />
                </div>
              )}

              <p className={cn(
                'font-medium mb-2',
                feedback.type === 'celebrate' && 'text-green-700',
                feedback.type === 'guidance' && 'text-amber-700',
                (feedback.type === 'answer-sheet' || feedback.type === 'error') && 'text-red-700'
              )}>
                {feedback.type === 'celebrate' ? '回答正确！' :
                 feedback.type === 'guidance' ? '还没答对，再试试' :
                 feedback.type === 'answer-sheet' ? '看看答案要点' : '出错了'}
              </p>

              {/* celebrate：postState 记忆状态徽章（preState→postState 由服务端驱动） */}
              {feedback.type === 'celebrate' && feedback.postState && (
                <div className="flex justify-center mb-2">
                  <MemoryStateBadge state={serverStateToMemoryState(feedback.postState)} size="md" />
                </div>
              )}

              {feedback.type === 'guidance' && (
                <>
                  <p className="text-sm text-amber-800">{feedback.hint}</p>
                  <p className="text-xs text-muted-foreground mt-1">再想想，回忆下要点</p>
                </>
              )}

              {/* answer-sheet：matched 标绿 / missing 标红 = 完整答案要点（Oracle #5） */}
              {feedback.type === 'answer-sheet' && (
                <div className="mt-4 p-3 bg-white rounded-lg text-left">
                  <p className="text-xs text-muted-foreground mb-1">答案要点：</p>
                  <div className="space-y-1">
                    {feedback.matchedKeywords.length === 0 && feedback.missingKeywords.length === 0 && (
                      <p className="text-sm text-muted-foreground">暂无要点</p>
                    )}
                    {feedback.matchedKeywords.map((kw) => (
                      <p key={kw} className="text-sm text-green-700">✓ {kw}</p>
                    ))}
                    {feedback.missingKeywords.map((kw) => (
                      <p key={kw} className="text-sm text-red-600">✗ {kw}</p>
                    ))}
                  </div>
                </div>
              )}

              {feedback.type === 'error' && (
                <p className="text-sm text-muted-foreground">{feedback.message}</p>
              )}

              {/* isDegraded 轻提示（判题引擎降级，UI 提示"判题可能不精确"） */}
              {degradedNotice && (
                <p className="text-xs text-muted-foreground mt-2">判题可能不精确</p>
              )}
            </Card>

            {/* celebrate：1.5s 后出现"下一题"按钮（既有交互保留） */}
            {showNextButton && feedback.type === 'celebrate' && (
              <Button
                onClick={handleNext}
                className="w-full h-12 text-base"
                size="lg"
              >
                下一题
                <ChevronRight className="ml-2 h-4 w-4" />
              </Button>
            )}

            {/* guidance：再试一次 + 求助升级（T7） */}
            {feedback.type === 'guidance' && (
              <div className="flex gap-3">
                <Button
                  variant="outline"
                  onClick={handleRetry}
                  className="flex-1 h-12"
                >
                  <RotateCcw className="mr-2 h-4 w-4" />
                  再试一次
                </Button>
                <Button
                  onClick={handleHint}
                  variant="secondary"
                  className="flex-1 h-12"
                >
                  <Lightbulb className="mr-2 h-4 w-4" />
                  求助升级
                </Button>
              </div>
            )}

            {/* answer-sheet：必进下一题（自动推进中提示） */}
            {feedback.type === 'answer-sheet' && (
              <p className="text-center text-xs text-muted-foreground">即将进入下一题…</p>
            )}

            {/* error：再试一次（停留当前题） */}
            {feedback.type === 'error' && (
              <Button
                variant="outline"
                onClick={handleRetry}
                className="w-full h-12"
              >
                <RotateCcw className="mr-2 h-4 w-4" />
                再试一次
              </Button>
            )}
          </div>
        )}
      </div>

      {/* 底部进度点阵（当前高亮；已完成绿点；答案展示/引导当前题红点） */}
      <div className="fixed bottom-0 left-0 right-0 bg-card border-t border-border p-4 safe-bottom">
        <div className="flex justify-center gap-2 flex-wrap">
          {Array.from({ length: Math.max(1, questionCount || 1) }, (_, index) => {
            const isCurrent = index === currentQuestionIndex;
            const isCompleted = index < currentQuestionIndex ||
              (index === currentQuestionIndex && feedback?.type === 'celebrate');
            const isBlocked = index === currentQuestionIndex &&
              (feedback?.type === 'answer-sheet' || feedback?.type === 'guidance');

            return (
              <div
                // biome-ignore lint/suspicious/noArrayIndexKey: 进度点阵为纯占位圆点（无业务 id），索引即语义位置
                key={index}
                className={cn(
                  'w-2 h-2 rounded-full transition-all',
                  isCurrent && 'w-3 h-3 bg-primary scale-125',
                  isCompleted && 'bg-green-500',
                  isBlocked && 'bg-red-500',
                  !isCurrent && !isCompleted && !isBlocked && 'bg-muted'
                )}
              />
            );
          })}
        </div>
      </div>
    </div>
  );
}

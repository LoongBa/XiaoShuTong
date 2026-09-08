import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { useState, useEffect, useRef } from 'react';
import { useAppStore } from '@/store/appStore';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { TaskProgressBar } from '@/components/TaskProgressBar';
import { 
  ArrowLeft, 
  Mic, 
  Lightbulb,
  CheckCircle2,
  XCircle,
  RotateCcw,
  ChevronRight,
  Sparkles
} from 'lucide-react';
import { cn } from '@/lib/utils';

// 模拟题目数据
const MOCK_QUESTIONS = [
  {
    id: 'q-1',
    type: 'R2',
    content: '"黄河远上白云间，____"',
    hint: '这是一首描写边塞风光的诗',
    answer: '一片孤城万仞山',
    order: 1
  },
  {
    id: 'q-2',
    type: 'R1',
    content: '"____，春风不度玉门关"',
    hint: '前一句提到了柳树',
    answer: '羌笛何须怨杨柳',
    order: 2
  },
  {
    id: 'q-3',
    type: 'R2',
    content: '"白日依山尽，____"',
    hint: '描写黄河入海',
    answer: '黄河入海流',
    order: 3
  },
  {
    id: 'q-4',
    type: 'R1',
    content: '"____，更上一层楼"',
    hint: '前一句说想看更远的地方',
    answer: '欲穷千里目',
    order: 4
  },
  {
    id: 'q-5',
    type: 'R2',
    content: '"床前明月光，____"',
    hint: '地上像结了霜',
    answer: '疑是地上霜',
    order: 5
  }
];

// 鼓励语库
const ENCOURAGEMENTS = [
  '全对！今天的★都被你点亮了',
  '这句百分百原味，老师都要给你点赞',
  '背得很顺！下次试试不看提示',
  '提示一下就想起来了，记忆正在长出来',
  '这题还不熟，谁都会卡，再看一眼就熟了',
  '记得牢，才是真的会'
];

export const Route = createFileRoute('/student/study/task')({
  component: TaskStudyPage,
});

function TaskStudyPage() {
  const navigate = useNavigate();
  const { 
    currentUser, 
    isLoggedIn, 
    currentTaskId, 
    tasks,
    currentQuestionIndex,
    answers,
    submitAnswer,
    nextQuestion,
    completeTask
  } = useAppStore();
  
  const [userAnswer, setUserAnswer] = useState('');
  const [showHint, setShowHint] = useState(false);
  const [feedback, setFeedback] = useState<{
    type: 'correct' | 'wrong' | 'hint-correct';
    message: string;
    correctAnswer?: string;
  } | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [showNextButton, setShowNextButton] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);
  
  // 检查登录状态
  useEffect(() => {
    if (!isLoggedIn) {
      navigate({ to: '/auth/login' });
    }
  }, [isLoggedIn, navigate]);
  
  // 获取当前任务
  const currentTask = tasks.find(t => t.id === currentTaskId);
  
  // 获取当前题目
  const currentQuestion = MOCK_QUESTIONS[currentQuestionIndex];
  
  // 自动聚焦输入框
  useEffect(() => {
    if (inputRef.current && !feedback) {
      inputRef.current.focus();
    }
  }, [currentQuestionIndex, feedback]);
  
  const handleSubmit = async () => {
    if (!userAnswer.trim() || !currentQuestion || isSubmitting) return;
    
    setIsSubmitting(true);
    
    // 模拟判题（简化版：包含关键词即算对）
    await new Promise(resolve => setTimeout(resolve, 500));
    
    const isCorrect = currentQuestion.answer.includes(userAnswer.trim()) || 
                      userAnswer.trim().includes(currentQuestion.answer.slice(0, 4));
    const usedHint = showHint;
    
    submitAnswer(currentQuestion.id, userAnswer.trim(), isCorrect, usedHint);
    
    if (isCorrect) {
      const encouragement = ENCOURAGEMENTS[Math.floor(Math.random() * ENCOURAGEMENTS.length)];
      setFeedback({
        type: usedHint ? 'hint-correct' : 'correct',
        message: usedHint ? '提示一下就想起来了，记忆正在长出来' : encouragement
      });
      
      // 1.5秒后显示下一题按钮
      setTimeout(() => {
        setShowNextButton(true);
      }, 1500);
    } else {
      setFeedback({
        type: 'wrong',
        message: '这题还不熟，谁都会卡，再看一眼就熟了',
        correctAnswer: currentQuestion.answer
      });
    }
    
    setIsSubmitting(false);
  };
  
  const handleNext = () => {
    if (currentQuestionIndex < MOCK_QUESTIONS.length - 1) {
      nextQuestion();
      setUserAnswer('');
      setShowHint(false);
      setFeedback(null);
      setShowNextButton(false);
    } else {
      // 完成任务
      completeTask();
      navigate({ to: '/student/study/result' });
    }
  };
  
  const handleRetry = () => {
    setUserAnswer('');
    setShowHint(false);
    setFeedback(null);
    setShowNextButton(false);
  };
  
  const handleHint = () => {
    setShowHint(true);
  };
  
  if (!currentTask || !currentQuestion) {
    return (
      <div className="min-h-screen bg-background flex items-center justify-center">
        <p className="text-muted-foreground">题目加载失败</p>
        <Button onClick={() => navigate({ to: '/student/home' })} className="mt-4">
          返回首页
        </Button>
      </div>
    );
  }
  
  return (
    <div className="min-h-screen bg-background">
      {/* 顶部导航 */}
      <header className="sticky top-0 z-10 bg-card border-b border-border px-4 py-3">
        <div className="flex items-center gap-3">
          <button
            onClick={() => navigate({ to: '/student/home' })}
            className="p-2 -ml-2 rounded-full hover:bg-muted transition-colors"
          >
            <ArrowLeft className="w-5 h-5" />
          </button>
          <div className="flex-1 min-w-0">
            <h1 className="font-semibold text-sm truncate">{currentTask.title}</h1>
            <div className="flex items-center gap-2 text-xs text-muted-foreground">
              <span>{currentTask.teacherName}</span>
              <span>·</span>
              <span>第 {currentQuestionIndex + 1}/{MOCK_QUESTIONS.length} 题</span>
            </div>
          </div>
        </div>
        
        {/* 进度条 */}
        <div className="mt-3">
          <TaskProgressBar
            current={currentQuestionIndex + (feedback?.type === 'correct' || feedback?.type === 'hint-correct' ? 1 : 0)}
            total={MOCK_QUESTIONS.length}
            size="sm"
            showText={false}
          />
        </div>
      </header>
      
      {/* 题目区 */}
      <div className="p-4">
        <Card className="p-6 min-h-[200px] flex flex-col items-center justify-center">
          {/* 题型标签 */}
          <span className="text-xs text-muted-foreground mb-4 px-2 py-1 bg-muted rounded-full">
            {currentQuestion.type === 'R1' ? '上句接下句' : 
             currentQuestion.type === 'R2' ? '下句接上句' : '段落默写'}
          </span>
          
          {/* 题目内容 */}
          <p className="question-text text-foreground">
            {currentQuestion.content}
          </p>
          
          {/* 提示按钮 */}
          {!showHint && !feedback && (
            <button
              onClick={handleHint}
              className="mt-4 text-xs text-muted-foreground flex items-center gap-1 hover:text-primary transition-colors"
            >
              <Lightbulb className="w-3 h-3" />
              想不起来？
            </button>
          )}
          
          {/* 提示内容 */}
          {showHint && !feedback && (
            <div className="mt-4 p-3 bg-amber-50 border border-amber-200 rounded-lg">
              <p className="text-sm text-amber-800 flex items-start gap-2">
                <Lightbulb className="w-4 h-4 mt-0.5 flex-shrink-0" />
                {currentQuestion.hint}
              </p>
            </div>
          )}
        </Card>
        
        {/* 答题区 */}
        {!feedback && (
          <div className="mt-6 space-y-4">
            <div className="relative">
              <Input
                ref={inputRef}
                value={userAnswer}
                onChange={(e) => setUserAnswer(e.target.value)}
                onKeyDown={(e) => e.key === 'Enter' && handleSubmit()}
                placeholder="请输入答案..."
                className="h-14 text-base pr-12"
                disabled={isSubmitting}
              />
              <button
                onClick={() => {}}
                className="absolute right-3 top-1/2 -translate-y-1/2 p-2 text-muted-foreground hover:text-primary transition-colors"
              >
                <Mic className="w-5 h-5" />
              </button>
            </div>
            
            <Button
              onClick={handleSubmit}
              disabled={!userAnswer.trim() || isSubmitting}
              className="w-full h-12 text-base"
              size="lg"
            >
              {isSubmitting ? (
                <>
                  <Sparkles className="mr-2 h-4 w-4 animate-spin" />
                  判题中…
                </>
              ) : (
                '提交'
              )}
            </Button>
          </div>
        )}
        
        {/* 反馈区 */}
        {feedback && (
          <div className="mt-6 space-y-4">
            <Card
              className={cn(
                'p-6 text-center',
                feedback.type === 'correct' && 'bg-green-50 border-green-200',
                feedback.type === 'hint-correct' && 'bg-amber-50 border-amber-200',
                feedback.type === 'wrong' && 'bg-red-50 border-red-200'
              )}
            >
              {feedback.type === 'correct' && (
                <div className="animate-star-pop inline-block mb-3">
                  <CheckCircle2 className="w-12 h-12 text-green-500" />
                </div>
              )}
              {feedback.type === 'hint-correct' && (
                <div className="inline-block mb-3">
                  <span className="text-3xl">△</span>
                </div>
              )}
              {feedback.type === 'wrong' && (
                <div className="inline-block mb-3">
                  <XCircle className="w-12 h-12 text-red-500" />
                </div>
              )}
              
              <p className={cn(
                'font-medium mb-2',
                feedback.type === 'correct' && 'text-green-700',
                feedback.type === 'hint-correct' && 'text-amber-700',
                feedback.type === 'wrong' && 'text-red-700'
              )}>
                {feedback.type === 'correct' ? '回答正确！' : 
                 feedback.type === 'hint-correct' ? '求助后答对' : '回答错误'}
              </p>
              
              <p className="text-sm text-muted-foreground">
                {feedback.message}
              </p>
              
              {feedback.correctAnswer && (
                <div className="mt-4 p-3 bg-white rounded-lg">
                  <p className="text-xs text-muted-foreground mb-1">正确答案：</p>
                  <p className="question-text text-base">{feedback.correctAnswer}</p>
                </div>
              )}
            </Card>
            
            {showNextButton && (
              <Button
                onClick={handleNext}
                className="w-full h-12 text-base"
                size="lg"
              >
                {currentQuestionIndex < MOCK_QUESTIONS.length - 1 ? (
                  <>
                    下一题
                    <ChevronRight className="ml-2 h-4 w-4" />
                  </>
                ) : (
                  '完成任务'
                )}
              </Button>
            )}
            
            {feedback.type === 'wrong' && (
              <div className="flex gap-3">
                <Button
                  variant="outline"
                  onClick={handleRetry}
                  className="flex-1 h-12"
                >
                  <RotateCcw className="mr-2 h-4 w-4" />
                  再看一遍
                </Button>
                <Button
                  onClick={handleNext}
                  variant="secondary"
                  className="flex-1 h-12"
                >
                  下一题
                  <ChevronRight className="ml-2 h-4 w-4" />
                </Button>
              </div>
            )}
          </div>
        )}
      </div>
      
      {/* 底部进度点阵 */}
      <div className="fixed bottom-0 left-0 right-0 bg-card border-t border-border p-4 safe-bottom">
        <div className="flex justify-center gap-2">
          {MOCK_QUESTIONS.map((q, index) => {
            const isCurrent = index === currentQuestionIndex;
            const isCompleted = index < currentQuestionIndex || 
              (index === currentQuestionIndex && feedback?.type === 'correct');
            const isWrong = index === currentQuestionIndex && feedback?.type === 'wrong';
            
            return (
              <div
                key={q.id}
                className={cn(
                  'w-2 h-2 rounded-full transition-all',
                  isCurrent && 'w-3 h-3 bg-primary scale-125',
                  isCompleted && 'bg-green-500',
                  isWrong && 'bg-red-500',
                  !isCurrent && !isCompleted && !isWrong && 'bg-muted'
                )}
              />
            );
          })}
        </div>
      </div>
    </div>
  );
}

import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { useEffect, useState } from 'react';
import { useAppStore } from '@/store/appStore';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { EmptyState } from '@/components/EmptyState';
import { BottomNav } from '@/components/BottomNav';
import { Tkwf } from '@tkwf/tsclient';
import type { WrongQuestions_ExecuteService, WrongQuestionsDto, MarkMastered_ExecuteService } from '@/gql/ts-client.g';
import { 
  ArrowLeft,
  RotateCcw,
  CheckCircle2,
  BookOpen,
  Loader2,
  AlertCircle
} from 'lucide-react';
import { cn } from '@/lib/utils';

export const Route = createFileRoute('/student/stats/wrong')({
  component: WrongAnswersPage,
});

function WrongAnswersPage() {
  const navigate = useNavigate();
  const { isLoggedIn, setWrongPracticeQuestionIds } = useAppStore();
  const [activeTab, setActiveTab] = useState<'wrong' | 'mastered'>('wrong');
  const [wrongItems, setWrongItems] = useState<WrongQuestionsDto[]>([]);
  const [masteredItems, setMasteredItems] = useState<WrongQuestionsDto[]>([]);
  const [wrongTotal, setWrongTotal] = useState(0);
  const [masteredTotal, setMasteredTotal] = useState(0);
  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState('');
  const [reloadKey, setReloadKey] = useState(0);

  // 检查登录状态
  useEffect(() => {
    if (!isLoggedIn) {
      navigate({ to: '/auth/login' });
    }
  }, [isLoggedIn, navigate]);

  // V0.6.9：真实契约接线——双 tab 分查（mastered:false/true），计数策略 Oracle M2 方案 A（两 tab 各查一次拿 total）
  useEffect(() => {
    if (!isLoggedIn) return;
    let cancelled = false;
    const loadWrong = async (mastered: boolean) => {
      try {
        const res = await Tkwf.User.Use<WrongQuestions_ExecuteService>().wrongQuestions_Execute({
          request: { mastered, subject: null, pageIndex: 1, pageSize: 100 },
        });
        if (cancelled) return;
        if (!res.success) {
          setLoadError(res.errorCode || '加载失败');
          return;
        }
        const items = res.items ?? [];
        if (mastered) {
          setMasteredItems(items);
          setMasteredTotal(res.total ?? items.length);
        } else {
          setWrongItems(items);
          setWrongTotal(res.total ?? items.length);
        }
      } catch (err: any) {
        if (!cancelled) {
          // RPC 抛错 → 读 err.code
          setLoadError(err?.code || err?.message || '加载失败');
        }
      }
    };
    // 切 tab 时清错误态重新加载（Oracle C5）
    setIsLoading(true);
    setLoadError('');
    Promise.all([loadWrong(false), loadWrong(true)]).finally(() => {
      if (!cancelled) setIsLoading(false);
    });
    return () => { cancelled = true; };
  }, [isLoggedIn, activeTab, reloadKey]);

  const handlePractice = () => {
    // V0.6.14（Oracle C7）：错题重练白名单经 store 传递（非路由参数）——当前 tab 待掌握错题 QuestionId 集
    // P2-3 修复：同时携带错题来源题库 bankId（WrongQuestionsDto.bankId 契约已有）→ task.tsx 复习路径
    // 用真实 bankId 替代 DEFAULT_BANK_ID 假兜底（服务端 GetNextQuestionService BR-17 先按 BankId 查库，
    // 假 bank-001 必 BankNotFound → 错题重练此前不可用）。错题可能跨题库，取首项 bankId 作为会话来源。
    const ids = wrongItems.map((item) => item.questionId);
    const bankId = wrongItems[0]?.bankId ?? null;
    setWrongPracticeQuestionIds(ids, bankId);
    navigate({ to: '/student/study/task' });
  };

  // V0.6.14（学习-BR-52）：手动标记已掌握/移回——调 markMastered_Execute + refetch 当前 tab（Oracle C5）
  const handleMarkMastered = async (questionId: string) => {
    try {
      await Tkwf.User.Use<MarkMastered_ExecuteService>().markMastered_Execute({
        request: { questionId, mastered: true },
      });
      setReloadKey((k) => k + 1); // refetch 两 tab（list 重新加载）
    } catch {
      // RPC 抛错：页面保留现状（全局 onGlobalError 已记录）
    }
  };

  const handleMoveBack = async (questionId: string) => {
    try {
      await Tkwf.User.Use<MarkMastered_ExecuteService>().markMastered_Execute({
        request: { questionId, mastered: false },
      });
      setReloadKey((k) => k + 1);
    } catch {
      // RPC 抛错：页面保留现状
    }
  };

  const displayItems = activeTab === 'wrong' ? wrongItems : masteredItems;
  const displayTotal = activeTab === 'wrong' ? wrongTotal : masteredTotal;
  const lastWrongDate = (iso: string | Date | null | undefined): string => {
    if (!iso) return '';
    return String(iso).slice(0, 10);
  };

  return (
    <div className="min-h-screen bg-background pb-20">
      {/* 顶部导航 */}
      <header className="sticky top-0 z-10 bg-card border-b border-border px-4 py-3">
        <div className="flex items-center gap-3">
          <button
            onClick={() => navigate({ to: '/student/home' })}
            className="p-2 -ml-2 rounded-full hover:bg-muted transition-colors"
          >
            <ArrowLeft className="w-5 h-5" />
          </button>
          <h1 className="font-semibold">错题本</h1>
        </div>
      </header>
      
      <div className="p-4 space-y-4">
        {/* 标签切换 */}
        <div className="flex gap-2">
          <button
            onClick={() => setActiveTab('wrong')}
            className={cn(
              'flex-1 py-2 px-4 rounded-lg text-sm font-medium transition-colors',
              activeTab === 'wrong' 
                ? 'bg-primary text-primary-foreground' 
                : 'bg-muted text-muted-foreground'
            )}
          >
            待复习 ({wrongTotal})
          </button>
          <button
            onClick={() => setActiveTab('mastered')}
            className={cn(
              'flex-1 py-2 px-4 rounded-lg text-sm font-medium transition-colors',
              activeTab === 'mastered' 
                ? 'bg-green-500 text-white' 
                : 'bg-muted text-muted-foreground'
            )}
          >
            已掌握 ({masteredTotal})
          </button>
        </div>

        {/* 加载态 */}
        {isLoading && (
          <div className="flex items-center justify-center py-12 text-muted-foreground">
            <Loader2 className="w-5 h-5 animate-spin mr-2" />
            错题加载中…
          </div>
        )}

        {/* 错误态 */}
        {!isLoading && loadError && (
          <Card className="p-6 text-center">
            <AlertCircle className="w-8 h-8 text-destructive mx-auto mb-2" />
            <p className="text-sm text-destructive mb-3">{loadError}</p>
            <Button variant="outline" size="sm" onClick={() => setReloadKey((k) => k + 1)}>
              重试
            </Button>
          </Card>
        )}

        {/* 空态：文案对齐 UI 设计 L262 正面空态（Oracle C5） */}
        {!isLoading && !loadError && displayItems.length === 0 && activeTab === 'wrong' && (
          <EmptyState
            title="没有错题"
            description="记得很牢！"
            icon="wrong"
            action={{
              label: '去挑战新内容',
              onClick: () => navigate({ to: '/student/bank/self' })
            }}
          />
        )}
        {!isLoading && !loadError && displayItems.length === 0 && activeTab === 'mastered' && (
          <EmptyState
            title="还没有已掌握的错题"
            description="连续答对 2 次后，错题会自动转到这里"
            icon="default"
          />
        )}

        {/* 错题列表（真实契约：知识点 + 错因 + 标准答案，V0.6.9 接线） */}
        {!isLoading && !loadError && activeTab === 'wrong' && displayItems.length > 0 && (
          <div className="space-y-3">
            {displayItems.map((item) => (
              <Card
                key={item.uId || item.id}
                className="p-4"
              >
                <div className="flex items-start justify-between mb-3">
                  <h3 className="font-medium flex-1 pr-2">{item.knowledgePoint}</h3>
                  <span className="text-xs text-muted-foreground">
                    错{item.wrongCount}次
                  </span>
                </div>
                
                <div className="space-y-2 mb-3">
                  <div className="p-2 bg-muted rounded text-sm">
                    <span className="text-muted-foreground text-xs">错因：</span>
                    <span>{item.summary}</span>
                  </div>
                  <div className="p-2 bg-green-50 rounded text-sm">
                    <span className="text-green-500 text-xs">正确答案：</span>
                    <span className="text-green-700">{item.answer}</span>
                  </div>
                </div>
                
                <div className="flex items-center justify-between">
                  <span className="text-xs text-muted-foreground">
                    {lastWrongDate(item.lastWrongAt) && `最近出错 ${lastWrongDate(item.lastWrongAt)}`}
                  </span>
                  <div className="flex gap-2">
                    {/* V0.6.14（BR-51）：重练=整 tab 待掌握错题白名单定向出题 */}
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={handlePractice}
                    >
                      <RotateCcw className="w-3 h-3 mr-1" />
                      重练
                    </Button>
                    {/* V0.6.14（BR-52）：手动标记已掌握（真实后端操作，refetch 刷新） */}
                    <Button
                      variant="outline"
                      size="sm"
                      className="text-green-600 border-green-200 hover:bg-green-50"
                      onClick={() => handleMarkMastered(item.questionId)}
                    >
                      <CheckCircle2 className="w-3 h-3 mr-1" />
                      已掌握
                    </Button>
                  </div>
                </div>
              </Card>
            ))}
          </div>
        )}

        {/* 已掌握列表（V0.6.14 BR-52：补"移回"真实后端操作） */}
        {!isLoading && !loadError && activeTab === 'mastered' && displayItems.length > 0 && (
          <div className="space-y-3">
            {displayItems.map((item) => (
              <Card
                key={item.uId || item.id}
                className="p-4 opacity-70"
              >
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <CheckCircle2 className="w-4 h-4 text-green-500" />
                    <span className="font-medium">{item.knowledgePoint}</span>
                  </div>
                  <div className="flex items-center gap-2">
                    <span className="text-xs text-muted-foreground">
                      错{item.wrongCount}次
                    </span>
                    {/* V0.6.14（BR-52）：移回错题本（真实后端操作，refetch 刷新） */}
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => handleMoveBack(item.questionId)}
                    >
                      移回
                    </Button>
                  </div>
                </div>
              </Card>
            ))}
          </div>
        )}
        
        {/* 复习建议（对齐真实语义：已掌握自动流转） */}
        {!isLoading && !loadError && activeTab === 'wrong' && displayItems.length > 0 && (
          <Card className="p-4 bg-amber-50 border-amber-200">
            <div className="flex items-start gap-3">
              <BookOpen className="w-5 h-5 text-amber-600 flex-shrink-0 mt-0.5" />
              <div>
                <h3 className="font-medium text-sm text-amber-800 mb-1">复习建议</h3>
                <p className="text-xs text-amber-700">
                  连续答对 2 次后，错题会自动转"已掌握"。定期重练错题，让知识真正变成你的！
                </p>
              </div>
            </div>
          </Card>
        )}
      </div>
      
      {/* 底部导航 */}
      <BottomNav />
    </div>
  );
}
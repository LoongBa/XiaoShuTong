import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { useEffect, useState } from 'react';
import { useAppStore } from '@/store/appStore';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { MemoryStateBadge } from '@/components/MemoryStateBadge';
import { Tkwf } from '@tkwf/tsclient';
import type { SessionResult_ExecuteService, BlockedPointDto } from '@/gql/ts-client.g';
import { serverStateToMemoryState } from '@/lib/memory-state';
import {
  Sparkles,
  Star,
  RotateCcw,
  Home,
  Share2,
  Loader2,
  XCircle
} from 'lucide-react';

export const Route = createFileRoute('/student/study/result')({
  component: StudyResultPage,
});

interface SessionResult {
  correctCount: number;
  totalCount: number;
  newStarCount: number;
  blockedPoints: BlockedPointDto[];
}

function StudyResultPage() {
  const navigate = useNavigate();
  const { isLoggedIn, sessionUid } = useAppStore();
  const [showAnimation, setShowAnimation] = useState(true);
  const [result, setResult] = useState<SessionResult | null>(null);
  const [loadState, setLoadState] = useState<'loading' | 'error' | 'ready'>('loading');

  // 检查登录状态
  useEffect(() => {
    if (!isLoggedIn) {
      navigate({ to: '/auth/login' });
    }
  }, [isLoggedIn, navigate]);

  // 加载会话结果（sessionResult_Execute）：正确率 / 新增★ / 卡住知识点全部来自服务端契约
  useEffect(() => {
    if (!isLoggedIn) return;

    // 空会话（BR-40）：无 sessionUid → 不产生结果页，直接回首页
    if (!sessionUid) {
      navigate({ to: '/student/home' });
      return;
    }

    let cancelled = false;
    const loadResult = async () => {
      setLoadState('loading');
      try {
        const res = await Tkwf.User.Use<SessionResult_ExecuteService>().sessionResult_Execute({
          request: { sessionUid },
        });
        if (cancelled) return;
        if (!res.success) {
          setLoadState('error');
          return;
        }
        setResult({
          correctCount: res.correctCount,
          totalCount: res.totalCount,
          newStarCount: res.newStarCount,
          blockedPoints: res.blockedPoints,
        });
        setLoadState('ready');
      } catch {
        if (cancelled) return;
        setLoadState('error');
      }
    };
    void loadResult();
    return () => { cancelled = true; };
  }, [isLoggedIn, sessionUid, navigate]);

  // 隐藏动画
  useEffect(() => {
    const timer = setTimeout(() => {
      setShowAnimation(false);
    }, 2000);
    return () => clearTimeout(timer);
  }, []);

  const totalQuestions = result?.totalCount ?? 0;
  const correctCount = result?.correctCount ?? 0;
  const newStars = result?.newStarCount ?? 0;
  const isAllCorrect = totalQuestions > 0 && correctCount === totalQuestions;

  // 卡住的知识点（BR-42：✕/△ 题目 + 知识点，State 徽章渲染；替换 stuckItems 硬编码）
  const stuckItems = (result?.blockedPoints ?? []).map((bp) => {
    const state = serverStateToMemoryState(bp.state);
    return {
      id: bp.questionId,
      title: bp.knowledgePoint,
      state,
      // reason 按转换后 MemoryState 判定（真实后端下发 PascalCase 枚举名，经 serverStateToMemoryState 转换，不再比对显示符号）
      reason: state === 'gray' ? '卡壳' : state === 'yellow' ? '求助后对' : '薄弱',
    };
  });

  // 加载中
  if (loadState === 'loading') {
    return (
      <div className="min-h-screen bg-background flex items-center justify-center">
        <Loader2 className="w-8 h-8 animate-spin text-primary" />
      </div>
    );
  }

  // 结果加载失败 → 错误态 + 返回首页
  if (loadState === 'error' || !result) {
    return (
      <div className="min-h-screen bg-background flex flex-col items-center justify-center p-4">
        <Card className="p-8 max-w-sm w-full text-center">
          <XCircle className="w-12 h-12 text-destructive mx-auto mb-4" />
          <h2 className="font-semibold text-lg mb-2">结果加载失败</h2>
          <p className="text-sm text-muted-foreground mb-6">网络开小差了，稍后再来。</p>
          <Button onClick={() => navigate({ to: '/student/home' })} className="w-full h-12">
            <Home className="mr-2 h-4 w-4" />
            返回首页
          </Button>
        </Card>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-background">
      {/* 庆祝动画 */}
      {showAnimation && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50">
          <div className="text-center">
            <div className="text-6xl mb-4 animate-bounce">
              {isAllCorrect ? '🎉' : '✨'}
            </div>
            <p className="text-white text-xl font-bold">
              {isAllCorrect ? '全对！今天又是满分状态' : '完成！'}
            </p>
          </div>
        </div>
      )}

      {/* 顶部 */}
      <div className="bg-gradient-to-b from-primary/10 to-background px-4 pt-8 pb-6">
        <div className="text-center">
          {/* 星星动画 */}
          <div className="relative inline-block mb-4">
            <div className="text-6xl animate-star-pop">
              <Star className="w-20 h-20 text-amber-400 fill-amber-400" />
            </div>
            <div className="absolute -top-2 -right-2 text-2xl animate-float-up">
              ✨
            </div>
            <div className="absolute -bottom-1 -left-2 text-xl animate-float-up" style={{ animationDelay: '0.3s' }}>
              ✨
            </div>
          </div>

          {/* 结果统计（服务端 sessionResult_Execute） */}
          <h1 className="text-2xl font-bold mb-2">
            这次背对了 {correctCount}/{totalQuestions}
          </h1>
          <p className="text-muted-foreground mb-4">
            新增 {newStars} 颗★
          </p>

          {/* 鼓励语 */}
          <div className="inline-flex items-center gap-2 px-4 py-2 bg-primary/10 rounded-full">
            <Sparkles className="w-4 h-4 text-primary" />
            <span className="text-sm text-primary font-medium">
              {isAllCorrect ? '全对！今天的★都被你点亮了' : '继续加油，下次会更好'}
            </span>
          </div>
        </div>
      </div>

      {/* 内容区 */}
      <div className="px-4 pb-24 space-y-4">
        {/* 卡住的知识点 */}
        {stuckItems.length > 0 && (
          <Card className="p-4">
            <h2 className="font-semibold mb-3 flex items-center gap-2">
              <span className="w-1.5 h-5 bg-amber-500 rounded-full" />
              下次重点复习
            </h2>
            <div className="space-y-2">
              {stuckItems.map((item) => (
                <div
                  key={item.id}
                  className="flex items-center justify-between p-3 bg-muted rounded-lg"
                >
                  <div className="flex items-center gap-2">
                    <MemoryStateBadge state={item.state} size="sm" />
                    <span className="font-medium">{item.title}</span>
                  </div>
                  <span className="text-xs text-muted-foreground">
                    {item.reason}
                  </span>
                </div>
              ))}
            </div>
          </Card>
        )}

        {/* 分享卡片 */}
        <Card className="p-4 bg-gradient-to-r from-primary/5 to-amber-50 border-primary/20">
          <div className="flex items-center justify-between">
            <div>
              <h3 className="font-semibold mb-1">分享你的进步</h3>
              <p className="text-xs text-muted-foreground">
                让老师和家长看到你的努力
              </p>
            </div>
            <Button variant="outline" size="sm" className="gap-1">
              <Share2 className="w-4 h-4" />
              分享
            </Button>
          </div>
        </Card>
      </div>

      {/* 底部按钮 */}
      <div className="fixed bottom-0 left-0 right-0 bg-card border-t border-border p-4 safe-bottom">
        <div className="flex gap-3">
          <Button
            variant="outline"
            onClick={() => navigate({ to: '/student/home' })}
            className="flex-1 h-12"
          >
            <Home className="mr-2 h-4 w-4" />
            返回首页
          </Button>
          <Button
            onClick={() => navigate({ to: '/student/bank/self' })}
            className="flex-1 h-12"
          >
            <RotateCcw className="mr-2 h-4 w-4" />
            继续背
          </Button>
        </div>
      </div>
    </div>
  );
}

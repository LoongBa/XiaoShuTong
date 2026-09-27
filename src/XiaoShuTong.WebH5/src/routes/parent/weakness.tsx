import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { useCallback, useEffect, useState } from 'react';
import { useAppStore } from '@/store/appStore';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Tkwf } from '@tkwf/tsclient';
import type { WeaknessReport_ExecuteService, ParentWeakPointDto } from '@/gql/ts-client.g';
import {
  ArrowLeft,
  Crown,
  Loader2,
  AlertCircle,
  Lock,
  Target,
} from 'lucide-react';
import { cn } from '@/lib/utils';

export const Route = createFileRoute('/parent/weakness')({
  validateSearch: (search: Record<string, unknown>): { studentId?: number } => ({
    studentId: typeof search.studentId === 'number' ? search.studentId : undefined,
  }),
  component: ParentWeaknessPage,
});

// 状态文案 → 徽章色调（BR-27 State 映射家长文案：未掌握✕ / 模糊△ / 掌握○ / 熟练★）
function stateBadgeClass(stateText: string): string {
  switch (stateText) {
    case '未掌握': return 'bg-red-50 text-red-600 border-red-200';
    case '模糊': return 'bg-amber-50 text-amber-600 border-amber-200';
    case '掌握': return 'bg-green-50 text-green-600 border-green-200';
    case '熟练': return 'bg-primary/10 text-primary border-primary/20';
    default: return 'bg-muted text-muted-foreground border-border';
  }
}

function ParentWeaknessPage() {
  const navigate = useNavigate();
  const { isLoggedIn } = useAppStore();
  const { studentId } = Route.useSearch();
  const [weakPoints, setWeakPoints] = useState<ParentWeakPointDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [errorCode, setErrorCode] = useState<string | null>(null);
  const [reloadKey, setReloadKey] = useState(0);

  // 检查登录状态
  useEffect(() => {
    if (!isLoggedIn) {
      navigate({ to: '/auth/login' });
    }
  }, [isLoggedIn, navigate]);

  // V0.6.15（Oracle C3）：studentId 缺失 → 回 dashboard
  useEffect(() => {
    if (isLoggedIn && studentId == null) {
      navigate({ to: '/parent/dashboard' });
    }
  }, [isLoggedIn, studentId, navigate]);

  // 拉取薄弱知识点（weaknessReport_Execute；BR-25 订阅门控 8001 / BR-26 按需聚合 / 无数据 4001）
  const loadWeakness = useCallback(async () => {
    if (!isLoggedIn || studentId == null) return;
    let cancelled = false;
    setIsLoading(true);
    setErrorCode(null);
    try {
      const res = await Tkwf.User.Use<WeaknessReport_ExecuteService>().weaknessReport_Execute({
        request: { studentId },
      });
      if (cancelled) return;
      if (!res.success) {
        setErrorCode(res.errorCode ?? 'UNKNOWN');
        setWeakPoints([]);
        return;
      }
      setWeakPoints(res.weakPoints ?? []);
    } catch (err: any) {
      if (!cancelled) setErrorCode(err?.code || err?.message || '加载失败');
    } finally {
      if (!cancelled) setIsLoading(false);
    }
    return () => { cancelled = true; };
  }, [isLoggedIn, studentId]);

  useEffect(() => {
    void loadWeakness();
  }, [loadWeakness, reloadKey]);

  const isSubscriptionRequired = errorCode === 'SUBSCRIPTION_REQUIRED' || errorCode === 'TRIAL_EXPIRED';
  const isNoStats = errorCode === 'NO_STATS_DATA';

  // 学科分组（weakPoints.subject；Oracle C1：mock 已补该字段）
  const bySubject = new Map<string, ParentWeakPointDto[]>();
  for (const wp of weakPoints) {
    const list = bySubject.get(wp.subject) ?? [];
    list.push(wp);
    bySubject.set(wp.subject, list);
  }

  return (
    <div className="min-h-screen bg-background pb-8">
      {/* 顶部导航 */}
      <header className="sticky top-0 z-10 bg-card border-b border-border px-4 py-3">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-3">
            <button
              onClick={() => navigate({ to: '/parent/dashboard' })}
              className="p-2 -ml-2 rounded-full hover:bg-muted transition-colors"
            >
              <ArrowLeft className="w-5 h-5" />
            </button>
            <h1 className="font-semibold">薄弱知识点</h1>
          </div>
          <Button size="sm" variant="ghost" onClick={() => navigate({ to: '/parent/progress', search: { studentId } })}>
            进度趋势
          </Button>
        </div>
      </header>

      <div className="p-4 space-y-4">
        {/* 加载态 */}
        {isLoading && (
          <div className="flex items-center justify-center py-12 text-muted-foreground">
            <Loader2 className="w-5 h-5 animate-spin mr-2" />
            报告加载中…
          </div>
        )}

        {/* 未订阅 → 引导开通（V0.6.15 C4：纯引导卡） */}
        {!isLoading && isSubscriptionRequired && (
          <Card className="p-6 text-center">
            <Lock className="w-8 h-8 text-amber-500 mx-auto mb-2" />
            <h3 className="font-semibold text-amber-900 mb-1">开通成长报告</h3>
            <p className="text-sm text-muted-foreground mb-4">
              订阅后查看完整进度趋势与薄弱知识点分析
            </p>
            <Button className="w-full" onClick={() => navigate({ to: '/parent/dashboard' })}>
              <Crown className="w-4 h-4 mr-2" />
              去开通
            </Button>
          </Card>
        )}

        {/* 无数据 → 空态（UI L415） */}
        {!isLoading && isNoStats && (
          <Card className="p-6 text-center text-sm text-muted-foreground">
            孩子还没有学习记录，让他/她背第一首诗吧
          </Card>
        )}

        {/* 其他错误 → 重试 */}
        {!isLoading && errorCode && !isSubscriptionRequired && !isNoStats && (
          <Card className="p-6 text-center">
            <AlertCircle className="w-8 h-8 text-destructive mx-auto mb-2" />
            <p className="text-sm text-destructive mb-3">{errorCode}</p>
            <Button variant="outline" size="sm" onClick={() => setReloadKey((k) => k + 1)}>
              重试
            </Button>
          </Card>
        )}

        {/* 弱项空态（有权限但无薄弱点） */}
        {!isLoading && !errorCode && weakPoints.length === 0 && (
          <Card className="p-6 text-center">
            <Target className="w-8 h-8 text-green-500 mx-auto mb-2" />
            <p className="text-sm text-muted-foreground">
              暂无薄弱知识点，孩子掌握得很好！
            </p>
          </Card>
        )}

        {/* 薄弱知识点列表（学科分组 + accuracy 升序 + stateText 徽章） */}
        {!isLoading && !errorCode && weakPoints.length > 0 && (
          <div className="space-y-4">
            {[...bySubject.entries()].map(([subject, points]) => (
              <Card key={subject} className="p-4">
                <h3 className="font-semibold text-sm mb-3 flex items-center gap-2">
                  <Target className="w-4 h-4 text-amber-500" />
                  {subject}
                  <span className="text-xs text-muted-foreground">{points.length} 个薄弱点</span>
                </h3>
                <div className="space-y-2">
                  {points.map((wp) => (
                    <div
                      key={wp.knowledgePoint}
                      className="flex items-center justify-between p-2 rounded-lg bg-muted/50"
                    >
                      <span className="text-sm font-medium">{wp.knowledgePoint}</span>
                      <div className="flex items-center gap-2">
                        <span className={cn('text-xs px-2 py-0.5 rounded-full border', stateBadgeClass(wp.stateText))}>
                          {wp.stateText}
                        </span>
                        <span className="text-xs text-muted-foreground">
                          {Math.round(wp.accuracy * 100)}%
                        </span>
                      </div>
                    </div>
                  ))}
                </div>
              </Card>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
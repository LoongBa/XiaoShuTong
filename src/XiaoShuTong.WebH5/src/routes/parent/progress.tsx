import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { useCallback, useEffect, useState } from 'react';
import { useAppStore } from '@/store/appStore';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Tkwf } from '@tkwf/tsclient';
import type { ProgressReport_ExecuteService, DailyStatsDto } from '@/gql/ts-client.g';
import { accuracyToPercent } from '@/lib/accuracy';
import {
  ArrowLeft,
  Crown,
  TrendingUp,
  TrendingDown,
  Loader2,
  AlertCircle,
  Lock,
} from 'lucide-react';
import { cn } from '@/lib/utils';

export const Route = createFileRoute('/parent/progress')({
  validateSearch: (search: Record<string, unknown>): { studentId?: number } => ({
    studentId: typeof search.studentId === 'number' ? search.studentId : undefined,
  }),
  component: ParentProgressPage,
});

function ParentProgressPage() {
  const navigate = useNavigate();
  const { isLoggedIn } = useAppStore();
  const { studentId } = Route.useSearch();
  const [period, setPeriod] = useState<'Week' | 'Month'>('Week');
  const [trend, setTrend] = useState<DailyStatsDto[]>([]);
  const [vsLastWeek, setVsLastWeek] = useState<{ learnedDelta: number; weaknessShift: number } | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [errorCode, setErrorCode] = useState<string | null>(null);
  const [reloadKey, setReloadKey] = useState(0);

  // 检查登录状态
  useEffect(() => {
    if (!isLoggedIn) {
      navigate({ to: '/auth/login' });
    }
  }, [isLoggedIn, navigate]);

  // Oracle C3（V0.6.15）：studentId 缺失（书签/深链接失效）→ 回 dashboard
  useEffect(() => {
    if (isLoggedIn && studentId == null) {
      navigate({ to: '/parent/dashboard' });
    }
  }, [isLoggedIn, studentId, navigate]);

  // 拉取进度趋势（progressReport_Execute；BR-20 订阅门控 8001 / BR-22 无数据 4001）
  const loadProgress = useCallback(async () => {
    if (!isLoggedIn || studentId == null) return;
    let cancelled = false;
    setIsLoading(true);
    setErrorCode(null);
    try {
      const res = await Tkwf.User.Use<ProgressReport_ExecuteService>().progressReport_Execute({
        request: { studentId, period },
      });
      if (cancelled) return;
      if (!res.success) {
        setErrorCode(res.errorCode ?? 'UNKNOWN');
        setTrend([]);
        setVsLastWeek(null);
        return;
      }
      setTrend(res.trend ?? []);
      setVsLastWeek(res.vsLastWeek ?? null);
    } catch (err: any) {
      if (!cancelled) setErrorCode(err?.code || err?.message || '加载失败');
    } finally {
      if (!cancelled) setIsLoading(false);
    }
    return () => { cancelled = true; };
  }, [isLoggedIn, studentId, period]);

  useEffect(() => {
    void loadProgress();
  }, [loadProgress, reloadKey]);

  // 背诵量条形图（SVG 自制，无新依赖——Oracle 验证可行）
  const maxLearned = Math.max(...trend.map((d) => d.learnedCount), 1);
  const barW = 100 / Math.max(trend.length, 1);

  // 正确率折线（跳过 null 日；整段空 → 空态卡）
  const accuracyPoints = trend
    .map((d, i) => (d.accuracy != null ? { x: i, y: d.accuracy } : null))
    .filter((p): p is { x: number; y: number } => p !== null);
  const hasAnyAccuracy = accuracyPoints.length > 0;
  const polylinePoints = hasAnyAccuracy
    ? accuracyPoints.map((p) => `${(p.x + 0.5) * barW},${100 - p.y * 100}`).join(' ')
    : '';

  // Gate：8001 未订阅 → 引导开通；4001 无数据 → 空态；其他 → 错误重试
  const isSubscriptionRequired = errorCode === 'SUBSCRIPTION_REQUIRED' || errorCode === 'TRIAL_EXPIRED';
  const isNoStats = errorCode === 'NO_STATS_DATA';

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
            <h1 className="font-semibold">进度趋势</h1>
          </div>
          <Button size="sm" variant="ghost" onClick={() => navigate({ to: '/parent/weakness', search: { studentId } })}>
            薄弱知识点
          </Button>
        </div>
      </header>

      <div className="p-4 space-y-4">
        {/* 周/月切换（Oracle C3：切换期间旧数据保留 + loading bar） */}
        <div className="flex gap-2">
          {(['Week', 'Month'] as const).map((p) => (
            <button
              key={p}
              onClick={() => setPeriod(p)}
              className={cn(
                'flex-1 py-2 px-4 rounded-lg text-sm font-medium transition-colors',
                period === p
                  ? 'bg-primary text-primary-foreground'
                  : 'bg-muted text-muted-foreground'
              )}
            >
              {p === 'Week' ? '本周' : '本月'}
            </button>
          ))}
        </div>

        {/* 加载态 */}
        {isLoading && (
          <div className="flex items-center justify-center py-12 text-muted-foreground">
            <Loader2 className="w-5 h-5 animate-spin mr-2" />
            报告加载中…
          </div>
        )}

        {/* 未订阅 → 引导开通（V0.6.15 C4：纯引导卡，产品确认不阻塞） */}
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

        {/* 数据就绪 */}
        {!isLoading && !errorCode && (
          <>
            {/* vsLastWeek 相对进步（BR-23/24：仅与自己比，无群组排名） */}
            {vsLastWeek && (
              <Card className="p-4 bg-gradient-to-r from-primary/5 to-transparent">
                <h3 className="font-semibold text-sm mb-2 flex items-center gap-2">
                  <TrendingUp className="w-4 h-4" />
                  相对进步（本周 vs 上周）
                </h3>
                <div className="flex gap-4">
                  <div>
                    <p className="text-2xl font-bold text-primary">
                      {vsLastWeek.learnedDelta > 0 ? '+' : ''}{vsLastWeek.learnedDelta}
                    </p>
                    <p className="text-xs text-muted-foreground">多背篇数</p>
                  </div>
                  <div>
                    <p className="text-2xl font-bold flex items-center gap-1">
                      {vsLastWeek.weaknessShift}
                      {vsLastWeek.weaknessShift > 0
                        ? <TrendingDown className="w-4 h-4 text-destructive" />
                        : <TrendingUp className="w-4 h-4 text-green-500" />}
                    </p>
                    <p className="text-xs text-muted-foreground">薄弱点</p>
                  </div>
                </div>
              </Card>
            )}

            {/* 背诵量曲线（逐日条形，SVG 自制） */}
            {trend.length > 0 && (
              <Card className="p-4">
                <h3 className="font-semibold text-sm mb-3">背诵量（每日篇数）</h3>
                <div className="flex items-end gap-1 h-28">
                  {trend.map((d, i) => (
                    <div key={d.statDate} className="flex-1 flex flex-col items-center gap-1">
                      <span className="text-[10px] text-muted-foreground">{d.learnedCount}</span>
                      <div
                        className="w-full bg-primary/30 rounded-t"
                        style={{ height: `${(d.learnedCount / maxLearned) * 72}px` }}
                        title={`${d.statDate}: ${d.learnedCount} 篇`}
                      />
                    </div>
                  ))}
                </div>
              </Card>
            )}

            {/* 正确率曲线（SVG 折线；整段 null → 空态卡，Oracle C3） */}
            <Card className="p-4">
              <h3 className="font-semibold text-sm mb-3">正确率</h3>
              {!hasAnyAccuracy ? (
                <p className="text-sm text-muted-foreground py-4 text-center">
                  暂无正确率数据（完成作答后可查看）
                </p>
              ) : (
                <svg viewBox="0 0 100 100" className="w-full h-24" preserveAspectRatio="none">
                  <polyline
                    points={polylinePoints}
                    fill="none"
                    stroke="hsl(var(--primary))"
                    strokeWidth="1.5"
                    strokeLinecap="round"
                    strokeLinejoin="round"
                  />
                </svg>
              )}
              {hasAnyAccuracy && (
                <p className="text-xs text-muted-foreground mt-2 text-right">
                  最高 {accuracyToPercent(Math.max(...accuracyPoints.map((p) => p.y)))}%
                </p>
              )}
            </Card>
          </>
        )}
      </div>
    </div>
  );
}
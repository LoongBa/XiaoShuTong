import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { useEffect, useState } from 'react';
import { useAppStore } from '@/store/appStore';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';

import { MemoryStateBadge } from '@/components/MemoryStateBadge';
import { StreakBadge } from '@/components/StreakBadge';
import { Tkwf } from '@tkwf/tsclient';
import type { DashboardReport_ExecuteService, SubjectMasteryDto, StartTrial_ExecuteService } from '@/gql/ts-client.g';
import { Children_ExecuteService } from '@/gql/ts-client.g';
import { accuracyToPercent, accuracyToState } from '@/lib/accuracy';
import { 
  ArrowLeft,
  ChevronRight,
  Crown,
  Lock,
  CheckCircle2,
  XCircle,
  TrendingUp,
  Target,
  Clock,
  Loader2
} from 'lucide-react';
import { cn } from '@/lib/utils';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';

// 孩子列表（2b 配合项闭环：children_Execute → ChildItemDto.studentId 数值直通，
// 替换 MOCK_CHILDREN——nickname/className 跨账户域为空串占位；studentId 与 dashboardReport 同源）
interface ChildLocal {
  id: string;
  name: string;
  grade: string;
  streakDays: number;
  todayTaskCompleted: boolean;
  studentId: number;
}

const toChildLocal = (c: { studentId: number; studentUid: string; nickname: string; className: string }): ChildLocal => ({
  id: c.studentUid,
  name: c.nickname || `学生${c.studentId}`,
  grade: c.className || '',
  streakDays: 0, // dashboardReport.streakDays 真实值（P1-5 已接）
  todayTaskCompleted: false, // dashboardReport.todayCompleted 真实值（P1-5 已接）
  studentId: c.studentId,
});

export const Route = createFileRoute('/parent/dashboard')({
  component: ParentDashboardPage,
});

function ParentDashboardPage() {
  const navigate = useNavigate();
  const { isLoggedIn, currentUser, logout } = useAppStore();
  const [selectedChild, setSelectedChild] = useState<ChildLocal | null>(null);
  const [children, setChildren] = useState<ChildLocal[]>([]);
  const [isSubscribed, setIsSubscribed] = useState(false);
  const [showSubscribeDialog, setShowSubscribeDialog] = useState(false);
  const [trialDaysLeft, setTrialDaysLeft] = useState(0); // V0.6.12：初始 0，由 dashboardReport.trialEndAt 派生
  const [trialSubmitting, setTrialSubmitting] = useState(false); // V0.6.12：试用提交态
  const [trialError, setTrialError] = useState(''); // V0.6.12：试用错误提示
  const [masteryLoading, setMasteryLoading] = useState(true);
  const [subjectMastery, setSubjectMastery] = useState<SubjectMasteryDto[]>([]);
  // P1-5：消费 dashboardReport 已返回字段（替代硬编码/未消费值）
  const [streakDays, setStreakDays] = useState(0);
  const [todayCompleted, setTodayCompleted] = useState(false);
  const [weekLearned, setWeekLearned] = useState(0);
  const [weekAccuracy, setWeekAccuracy] = useState<number | null>(null);
  const [locked, setLocked] = useState(false);
  
  // 检查登录状态
  useEffect(() => {
    if (!isLoggedIn) {
      navigate({ to: '/auth/login' });
    }
  }, [isLoggedIn, navigate]);

  // 加载孩子列表（2b 配合项闭环：children_Execute 真实拉取 → 默认选中第一个）
  useEffect(() => {
    if (!isLoggedIn) return;
    let cancelled = false;
    const loadChildren = async () => {
      try {
        const res = await Tkwf.User.Use<Children_ExecuteService>().listChildren_Execute();
        if (cancelled) return;
        const items = (res.success && res.items ? res.items : []).map(toChildLocal);
        setChildren(items);
        if (items.length > 0) {
          setSelectedChild((prev) => prev ?? items[0]);
        }
      } catch {
        if (!cancelled) setChildren([]);
      }
    };
    void loadChildren();
    return () => { cancelled = true; };
  }, [isLoggedIn]);
  
  // 加载各学科掌握度（dashboardReport_Execute → subjectsMastery 真实契约；V0.6.3 接线，替换原硬编码 MOCK_SUBJECT_MASTERY）
  // P1-5（Oracle V0.6.7）：同时消费已返回但未使用的 streakDays/weekProgress/todayCompleted/subscription/locked
  useEffect(() => {
    if (!isLoggedIn || !selectedChild) return;
    let cancelled = false;
    const loadMastery = async () => {
      setMasteryLoading(true);
      try {
        const res = await Tkwf.User.Use<DashboardReport_ExecuteService>().dashboardReport_Execute({
          request: { studentId: selectedChild.studentId },
        });
        if (cancelled) return;
        if (res.success) {
          setSubjectMastery(res.subjectsMastery ?? []);
          // P1-5：已返回字段消费——连续天数 / 今日完成 / 订阅状态 / 锁态
          setStreakDays(res.streakDays ?? 0);
          setTodayCompleted(!!res.todayCompleted);
          setWeekLearned(res.weekProgress?.learnedCount ?? 0);
          setWeekAccuracy(res.weekProgress?.accuracy ?? null);
          // V0.6.12：订阅状态从 dashboardReport.subscription 契约派生（T1；修正 V0.6.7 P1-5 遗留契约错配 'Trial'→'Trialing'）
          // 有效权益 = Active / Trialing / Cancelled 周期内（对齐 ParentReportGate L60）
          const sub = res.subscription;
          setIsSubscribed(sub !== null && (sub.status === 'Active' || sub.status === 'Trialing' || sub.status === 'Cancelled'));
          // 试用剩余天数：从 trialEndAt 计算（替代本地硬编码 7）；Trialing 且已过 → 0（过期态由 locked 表达）
          setTrialDaysLeft(sub?.status === 'Trialing' && sub.trialEndAt
            ? Math.max(0, Math.ceil((new Date(sub.trialEndAt).getTime() - Date.now()) / 86400000))
            : 0);
          // locked=true 时订阅不受约（预览模式）；试用过期/已过期 → locked=true（ParentReportGate）
          setLocked(res.locked ?? false);
        } else {
          setSubjectMastery([]);
        }
      } catch {
        if (!cancelled) setSubjectMastery([]);
      } finally {
        if (!cancelled) setMasteryLoading(false);
      }
    };
    void loadMastery();
    return () => { cancelled = true; };
  }, [isLoggedIn, selectedChild?.studentId]);

  // V0.6.12：免费试用 7 天（T2：startTrial_Execute 真实接线；BR-05 授权链→8002 / BR-06 幂等返回原记录 / BR-07 +7 天）
  const handleStartTrial = async () => {
    if (!selectedChild) return;
    setTrialSubmitting(true);
    setTrialError('');
    try {
      const res = await Tkwf.User.Use<StartTrial_ExecuteService>().startTrial_Execute({
        request: { studentId: selectedChild.studentId },
      });
      if (!res.success) {
        setTrialError(res.errorCode || '开通试用失败');
        return;
      }
      // 成功（含幂等返回原记录 BR-06）→ 置试用态 + 重拉 dashboardReport（后端权威校正 locked/status/trialEndAt）
      setTrialError('');
      setShowSubscribeDialog(false);
      setMasteryLoading(true);
      // loadMastery 依赖 selectedChild?.studentId——引用重拉需触发：用与 useEffect 相同的调用路径
      await Tkwf.User.Use<DashboardReport_ExecuteService>().dashboardReport_Execute({
        request: { studentId: selectedChild.studentId },
      }).then((res2) => {
        const sub = res2.subscription;
        setIsSubscribed(sub !== null && (sub.status === 'Active' || sub.status === 'Trialing' || sub.status === 'Cancelled'));
        setTrialDaysLeft(sub?.status === 'Trialing' && sub.trialEndAt
          ? Math.max(0, Math.ceil((new Date(sub.trialEndAt).getTime() - Date.now()) / 86400000))
          : 0);
        setLocked(res2.locked ?? false);
      }).catch(() => { /* 重拉失败保留本地置态 */ });
    } catch (err: any) {
      setTrialError(err?.code || err?.message || '开通试用失败');
    } finally {
      setTrialSubmitting(false);
    }
  };
  
  return (
    <div className="min-h-screen bg-background">
      {/* 顶部导航 */}
      <header className="sticky top-0 z-10 bg-card border-b border-border px-4 py-3">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-3">
            <button
              onClick={() => navigate({ to: '/student/user/profile' })}
              className="p-2 -ml-2 rounded-full hover:bg-muted transition-colors"
            >
              <ArrowLeft className="w-5 h-5" />
            </button>
            <h1 className="font-semibold">成长报告</h1>
          </div>
          {/* V0.6.11（C2）：parent 端登出（走查三端切换闭环） */}
          <Button size="sm" variant="ghost" onClick={() => { logout(); navigate({ to: '/auth/login' }); }}>
            退出
          </Button>
        </div>
        
        {/* 孩子切换器（2b：真实 children 列表；单孩子隐藏） */}
        {children.length > 1 && (
          <div className="mt-3 flex gap-2">
            {children.map((child) => (
              <button
                key={child.id}
                onClick={() => setSelectedChild(child)}
                className={cn(
                  'px-3 py-1.5 rounded-full text-sm font-medium transition-colors',
                  selectedChild?.id === child.id
                    ? 'bg-primary text-primary-foreground'
                    : 'bg-muted text-muted-foreground'
                )}
              >
                {child.name}
              </button>
            ))}
          </div>
        )}
      </header>
      
      <div className="p-4 space-y-4">
        {/* 试用期提示（V0.6.12：trialDaysLeft 由 dashboardReport.trialEndAt 派生；0 且未订阅则不外显黄条——过期态由 locked 表达） */}
        {!isSubscribed && trialDaysLeft > 0 && (
          <div className="p-3 bg-amber-50 border border-amber-200 rounded-lg">
            <p className="text-sm text-amber-800 flex items-center gap-2">
              <Crown className="w-4 h-4" />
              试用中，剩余 {trialDaysLeft} 天
            </p>
          </div>
        )}
        
        {/* 孩子信息卡片（2b：children 未加载完前显示占位） */}
        <Card className="p-4">
          <div className="flex items-center justify-between mb-4">
            <div>
              <h2 className="font-semibold text-lg">{selectedChild?.name ?? '加载中…'}</h2>
              <p className="text-sm text-muted-foreground">{selectedChild?.grade || '—'}</p>
            </div>
            {/* P1-5：streakDays 改用 dashboardReport 真实值（替代 mock selectedChild.streakDays） */}
            <StreakBadge days={streakDays} size="lg" />
          </div>
          
          <div className="flex items-center gap-2">
            {/* P1-5：todayCompleted 改用 dashboardReport 真实值（替代 mock selectedChild.todayTaskCompleted） */}
            {todayCompleted ? (
              <span className="flex items-center gap-1 text-sm text-green-600">
                <CheckCircle2 className="w-4 h-4" />
                今日任务已完成
              </span>
            ) : (
              <span className="flex items-center gap-1 text-sm text-destructive">
                <XCircle className="w-4 h-4" />
                今日任务未完成
              </span>
            )}
          </div>
        </Card>
        
        {/* 本周进度（P1-5：weekProgress.learnedCount + accuracy 真实契约；total 用掌握度覆盖学科数兜底） */}
        <Card className="p-4">
          <h3 className="font-semibold mb-3 flex items-center gap-2">
            <Clock className="w-4 h-4" />
            本周进度
          </h3>
          <div className="flex items-baseline gap-2 mb-2">
            <span className="text-2xl font-bold">{weekLearned}</span>
            <span className="text-sm text-muted-foreground">篇已学</span>
            {weekAccuracy !== null && (
              <span className="ml-auto text-sm font-medium text-primary">
                正确率 {Math.round(weekAccuracy * 100)}%
              </span>
            )}
          </div>
          <p className="text-sm text-muted-foreground">
            本周学习 {weekLearned} 篇{weekAccuracy !== null ? `，正确率 ${Math.round(weekAccuracy * 100)}%` : ''}
          </p>
        </Card>
        
        {/* 各学科掌握度（V0.6.3：真实 dashboardReport_Execute → subjectsMastery.accuracy 渲染百分比 + 四阶徽章） */}
        <Card className="p-4">
          <h3 className="font-semibold mb-3 flex items-center gap-2">
            <Target className="w-4 h-4" />
            各学科掌握度
          </h3>
          {masteryLoading ? (
            <div className="flex items-center gap-2 text-sm text-muted-foreground py-2">
              <Loader2 className="w-4 h-4 animate-spin" />
              加载中…
            </div>
          ) : subjectMastery.length === 0 ? (
            <p className="text-sm text-muted-foreground">暂无掌握度数据（完成作答后可查看）</p>
          ) : (
            <div className="flex flex-wrap gap-2">
              {subjectMastery.map((item) => (
                <div key={item.subject} className="flex items-center gap-1.5">
                  <span className="text-sm">{item.subject}</span>
                  <MemoryStateBadge state={accuracyToState(item.accuracy)} size="sm" showLabel={false} />
                  <span className="text-sm font-medium text-muted-foreground">
                    {accuracyToPercent(item.accuracy)}%
                  </span>
                </div>
              ))}
            </div>
          )}
        </Card>
        
        {/* 付费功能入口（P1-6：/parent/progress 路由不存在——完整报告页待建；订阅后点击提示而非 404 导航） */}
        <Card 
          className={cn(
            'p-4 cursor-pointer transition-all',
            !isSubscribed && 'opacity-70'
          )}
          onClick={() => {
            if (!isSubscribed) {
              setShowSubscribeDialog(true);
            } else {
              // 完整报告页（进度趋势/薄弱知识点）为后续迭代项，路由未建；此处提示避免 404
              alert('完整报告页将在后续版本上线，敬请期待');
            }
          }}
        >
          <div className="flex items-center justify-between">
            <div>
              <h3 className="font-semibold flex items-center gap-2">
                <TrendingUp className="w-4 h-4" />
                查看完整报告
              </h3>
              <p className="text-sm text-muted-foreground mt-1">
                进度趋势、薄弱知识点、相对进步
              </p>
            </div>
            {!isSubscribed && (
              <div className="flex items-center gap-2">
                <Lock className="w-4 h-4 text-muted-foreground" />
                <ChevronRight className="w-5 h-5 text-muted-foreground" />
              </div>
            )}
            {isSubscribed && (
              <ChevronRight className="w-5 h-5 text-muted-foreground" />
            )}
          </div>
        </Card>
        
        {/* 订阅提示 */}
        {!isSubscribed && (
          <Card className="p-4 bg-gradient-to-r from-amber-50 to-orange-50 border-amber-200">
            <div className="flex items-start gap-3">
              <div className="w-10 h-10 rounded-full bg-amber-100 flex items-center justify-center flex-shrink-0">
                <Crown className="w-5 h-5 text-amber-600" />
              </div>
              <div className="flex-1">
                <h3 className="font-semibold text-amber-900 mb-1">
                  解锁完整成长报告
                </h3>
                <p className="text-sm text-amber-700 mb-3">
                  了解孩子的学习进度、薄弱知识点，帮助孩子更好地成长
                </p>
                <div className="flex items-center gap-2">
                  <span className="text-lg font-bold text-amber-900">¥10</span>
                  <span className="text-sm text-amber-700">/月</span>
                  <span className="text-xs text-amber-600 bg-amber-100 px-2 py-0.5 rounded-full">
                    年付¥100省¥20
                  </span>
                </div>
                {/* V0.6.12（T2）：未订阅时"免费试用"主 CTA（真实 startTrial_Execute）+ "立即开通"副 CTA（弹窗，支付待基建） */}
                {trialDaysLeft === 0 && (
                  <Button
                    className="w-full mt-3"
                    onClick={handleStartTrial}
                    disabled={trialSubmitting}
                  >
                    {trialSubmitting ? (
                      <>
                        <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                        开通中…
                      </>
                    ) : (
                      <>
                        <Crown className="mr-2 h-4 w-4" />
                        免费试用 7 天
                      </>
                    )}
                  </Button>
                )}
                <Button
                  variant="outline"
                  className="w-full mt-2"
                  onClick={() => setShowSubscribeDialog(true)}
                >
                  立即开通
                </Button>
                {trialError && (
                  <p className="text-sm text-destructive mt-2">
                    {trialError}
                  </p>
                )}
              </div>
            </div>
          </Card>
        )}
      </div>
      
      {/* 订阅弹窗 */}
      <Dialog open={showSubscribeDialog} onOpenChange={setShowSubscribeDialog}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              <Crown className="w-5 h-5 text-amber-500" />
              开通成长报告
            </DialogTitle>
            <DialogDescription>
              解锁完整的学习数据分析，了解孩子的成长轨迹
            </DialogDescription>
          </DialogHeader>
          
          <div className="space-y-3 mt-4">
            <div className="p-4 border rounded-lg">
              <div className="flex items-center justify-between mb-2">
                <span className="font-medium">月付</span>
                <span className="text-lg font-bold">¥10/月</span>
              </div>
              <p className="text-xs text-muted-foreground">按月订阅，随时取消</p>
            </div>
            
            <div className="p-4 border-2 border-primary rounded-lg bg-primary/5">
              <div className="flex items-center justify-between mb-2">
                <span className="font-medium flex items-center gap-2">
                  年付
                  <span className="text-xs bg-primary text-primary-foreground px-1.5 py-0.5 rounded">
                    推荐
                  </span>
                </span>
                <span className="text-lg font-bold">¥100/年</span>
              </div>
              <p className="text-xs text-muted-foreground">省¥20，平均¥8.3/月</p>
            </div>
          </div>
          
          <div className="flex gap-3 mt-4">
            <Button
              variant="outline"
              className="flex-1"
              onClick={() => setShowSubscribeDialog(false)}
            >
              取消
            </Button>
            {/* V0.6.12（T3）：支付开通为生产配合项（ActivateSubscriptionService=Callee，微信支付回调未接）——
                不假 setIsSubscribed(true)，明确提示即将上线；免费试用走 handleStartTrial 真实链路 */}
            <Button
              className="flex-1"
              onClick={() => {
                setShowSubscribeDialog(false);
                alert('支付功能即将上线，可先免费试用 7 天');
              }}
            >
              确认支付
            </Button>
          </div>
        </DialogContent>
      </Dialog>
    </div>
  );
}

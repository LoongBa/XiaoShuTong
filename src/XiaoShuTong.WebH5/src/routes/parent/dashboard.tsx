import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { useEffect, useState } from 'react';
import { useAppStore } from '@/store/appStore';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { TaskProgressBar } from '@/components/TaskProgressBar';
import { MemoryStateBadge } from '@/components/MemoryStateBadge';
import { StreakBadge } from '@/components/StreakBadge';
import { Tkwf } from '@tkwf/tsclient';
import type { DashboardReport_ExecuteService, SubjectMasteryDto } from '@/gql/ts-client.g';
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

// 模拟孩子数据（studentId 对齐联调种子 ParentStudentRelations：10003 家长 → 10001 小明）
const MOCK_CHILDREN = [
  { id: 'child-1', name: '小明', grade: '初二(3)班', streakDays: 7, todayTaskCompleted: true, studentId: 10001 },
];

export const Route = createFileRoute('/parent/dashboard')({
  component: ParentDashboardPage,
});

function ParentDashboardPage() {
  const navigate = useNavigate();
  const { isLoggedIn, currentUser } = useAppStore();
  const [selectedChild, setSelectedChild] = useState(MOCK_CHILDREN[0]);
  const [isSubscribed, setIsSubscribed] = useState(false);
  const [showSubscribeDialog, setShowSubscribeDialog] = useState(false);
  const [trialDaysLeft, setTrialDaysLeft] = useState(7);
  const [masteryLoading, setMasteryLoading] = useState(true);
  const [subjectMastery, setSubjectMastery] = useState<SubjectMasteryDto[]>([]);
  
  // 检查登录状态
  useEffect(() => {
    if (!isLoggedIn) {
      navigate({ to: '/auth/login' });
    }
  }, [isLoggedIn, navigate]);
  
  // 加载各学科掌握度（dashboardReport_Execute → subjectsMastery 真实契约；V0.6.3 接线，替换原硬编码 MOCK_SUBJECT_MASTERY）
  useEffect(() => {
    if (!isLoggedIn) return;
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
  }, [isLoggedIn, selectedChild.studentId]);
  
  const weeklyProgress = { current: 5, total: 10 };
  
  return (
    <div className="min-h-screen bg-background">
      {/* 顶部导航 */}
      <header className="sticky top-0 z-10 bg-card border-b border-border px-4 py-3">
        <div className="flex items-center gap-3">
          <button
            onClick={() => navigate({ to: '/student/user/profile' })}
            className="p-2 -ml-2 rounded-full hover:bg-muted transition-colors"
          >
            <ArrowLeft className="w-5 h-5" />
          </button>
          <h1 className="font-semibold">成长报告</h1>
        </div>
        
        {/* 孩子切换器 */}
        {MOCK_CHILDREN.length > 1 && (
          <div className="mt-3 flex gap-2">
            {MOCK_CHILDREN.map((child) => (
              <button
                key={child.id}
                onClick={() => setSelectedChild(child)}
                className={cn(
                  'px-3 py-1.5 rounded-full text-sm font-medium transition-colors',
                  selectedChild.id === child.id
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
        {/* 试用期提示 */}
        {!isSubscribed && trialDaysLeft > 0 && (
          <div className="p-3 bg-amber-50 border border-amber-200 rounded-lg">
            <p className="text-sm text-amber-800 flex items-center gap-2">
              <Crown className="w-4 h-4" />
              试用中，剩余 {trialDaysLeft} 天
            </p>
          </div>
        )}
        
        {/* 孩子信息卡片 */}
        <Card className="p-4">
          <div className="flex items-center justify-between mb-4">
            <div>
              <h2 className="font-semibold text-lg">{selectedChild.name}</h2>
              <p className="text-sm text-muted-foreground">{selectedChild.grade}</p>
            </div>
            <StreakBadge days={selectedChild.streakDays} size="lg" />
          </div>
          
          <div className="flex items-center gap-2">
            {selectedChild.todayTaskCompleted ? (
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
        
        {/* 本周进度 */}
        <Card className="p-4">
          <h3 className="font-semibold mb-3 flex items-center gap-2">
            <Clock className="w-4 h-4" />
            本周进度
          </h3>
          <TaskProgressBar
            current={weeklyProgress.current}
            total={weeklyProgress.total}
            size="md"
          />
          <p className="text-sm text-muted-foreground mt-2">
            已完成 {weeklyProgress.current}/{weeklyProgress.total} 篇
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
        
        {/* 付费功能入口 */}
        <Card 
          className={cn(
            'p-4 cursor-pointer transition-all',
            !isSubscribed && 'opacity-70'
          )}
          onClick={() => {
            if (!isSubscribed) {
              setShowSubscribeDialog(true);
            } else {
              navigate({ to: '/parent/progress' });
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
                <Button 
                  className="w-full mt-3"
                  onClick={() => setShowSubscribeDialog(true)}
                >
                  立即开通
                </Button>
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
            <Button
              className="flex-1"
              onClick={() => {
                setIsSubscribed(true);
                setShowSubscribeDialog(false);
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

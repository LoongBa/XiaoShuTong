import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { useEffect, useState } from 'react';
import { useAppStore } from '@/store/appStore';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { BottomNav } from '@/components/BottomNav';
import { TaskProgressBar } from '@/components/TaskProgressBar';
import { StreakBadge } from '@/components/StreakBadge';
import { MemoryStateBadge } from '@/components/MemoryStateBadge';
import { EmptyState } from '@/components/EmptyState';
import { Heatmap } from '@/components/Heatmap';
import { 
  BookOpen, 
  Users, 
  Trophy, 
  BarChart3, 
  ChevronRight,
  Clock,
  AlertCircle,
  Loader2
} from 'lucide-react';
import { cn } from '@/lib/utils';
import { Tkwf } from '@tkwf/tsclient';
import type { 
  MyTasks_ExecuteService, 
  ReviewQueue_ExecuteService,
  Heatmap_ExecuteService,
  Streak_ExecuteService
} from '@/gql/ts-client.g';

export const Route = createFileRoute('/student/home')({
  component: StudentHomePage,
});

function StudentHomePage() {
  const navigate = useNavigate();
  const { currentUser, isLoggedIn, startTask } = useAppStore();
  
  const [tasks, setTasks] = useState<any[]>([]);
  const [reviewItems, setReviewItems] = useState<any[]>([]);
  const [learningStats, setLearningStats] = useState<any[]>([]);
  const [streakDays, setStreakDays] = useState(0);
  const [isLoading, setIsLoading] = useState(true);
  
  // 检查登录状态
  useEffect(() => {
    if (!isLoggedIn) {
      navigate({ to: '/auth/login' });
    }
  }, [isLoggedIn, navigate]);
  
  // 从 API 加载数据（登录后已有 session，使用 Tkwf.User 正常鉴权）
  useEffect(() => {
    if (!isLoggedIn) return;
    
    const loadData = async () => {
      setIsLoading(true);
      try {
        // 并行加载所有数据（参数与真实 DTO 契约对齐：GraphQL_Api.md §三）
        const [tasksResult, reviewResult, heatmapResult, streakResult] = await Promise.all([
          Tkwf.User.Use<MyTasks_ExecuteService>().listMyTasks_Execute({
            request: { status: null }
          }),
          Tkwf.User.Use<ReviewQueue_ExecuteService>().reviewQueue_Execute({
            request: { date: new Date().toISOString().slice(0, 10), pageIndex: 1, pageSize: 20 }
          }),
          Tkwf.User.Use<Heatmap_ExecuteService>().heatmap_Execute({
            request: { start: '', end: '' }
          }),
          Tkwf.User.Use<Streak_ExecuteService>().streak_Execute(),
        ]);
        
        // 处理任务数据（MyTaskItemDto → 页面形状）
        if (tasksResult.success && tasksResult.items) {
          setTasks(tasksResult.items.map((item: any) => ({
            id: item.taskUid || item.id,
            title: item.title,
            description: '',
            teacherName: item.ownerName || '老师',
            teacherId: '',
            classId: '',
            totalQuestions: 1,
            completedQuestions: Math.round((item.progress ?? 0) * 100),
            deadline: item.deadlineAt || new Date().toISOString(),
            status: item.status?.toLowerCase() || 'pending',
            createdAt: new Date().toISOString(),
            source: 'teacher'
          })));
        }
        
        // 处理复习队列数据（MemoryStatesDto → 页面形状）
        if (reviewResult.success && reviewResult.items) {
          setReviewItems(reviewResult.items.map((item: any) => ({
            id: item.uId || item.questionId,
            knowledgePointId: item.questionId,
            title: '',
            memoryState: item.state === '✕' ? 'gray' : item.state === '△' ? 'yellow' : item.state === '○' ? 'green' : 'gold',
            daysSinceLastReview: Math.max(0, Math.ceil((Date.now() - new Date(item.nextReviewAt || Date.now()).getTime()) / 86400000)),
            isOverdue: new Date(item.nextReviewAt || Date.now()) < new Date()
          })));
        }
        
        // 处理热力图数据（DailyStatsDto → 页面形状）
        if (heatmapResult.success && heatmapResult.days) {
          setLearningStats(heatmapResult.days.map((day: any) => ({
            date: day.statDate || day.date,
            count: day.learnedCount || 0,
            correctCount: day.starredCount || 0
          })));
        }
        
        // 处理连续打卡数据
        if (streakResult.success) {
          setStreakDays(streakResult.currentStreak || 0);
        }
      } catch (error) {
        console.error('加载数据失败:', error);
      } finally {
        setIsLoading(false);
      }
    };
    
    loadData();
  }, [isLoggedIn]);
  
  // 获取今日任务
  const todayTasks = tasks.filter(t => 
    t.status === 'pending' || t.status === 'in_progress'
  );
  
  // 获取今日到期的复习项
  const todayReviews = reviewItems.filter(r => !r.isOverdue);
  const overdueReviews = reviewItems.filter(r => r.isOverdue);
  
  // 获取本周热力图数据
  const weeklyHeatmapData: { date: string; level: 0 | 1 | 2 | 3 | 4 }[] = learningStats.slice(-7).map((stat) => ({
    date: stat.date ?? '',
    level: (stat.count === 0 ? 0 : stat.count < 5 ? 1 : stat.count < 10 ? 2 : stat.count < 15 ? 3 : 4) as 0 | 1 | 2 | 3 | 4
  }));
  
  const handleTaskClick = (taskId: string) => {
    startTask(taskId);
    navigate({ to: '/student/study/task' });
  };
  
  const handleReviewClick = () => {
    navigate({ to: '/student/study/review' });
  };
  
  const getGreeting = () => {
    const hour = new Date().getHours();
    if (hour < 12) return '早上好';
    if (hour < 18) return '下午好';
    return '晚上好';
  };
  
  const formatDeadline = (deadline: string) => {
    const date = new Date(deadline);
    const now = new Date();
    const diffDays = Math.ceil((date.getTime() - now.getTime()) / (1000 * 60 * 60 * 24));
    
    if (diffDays < 0) return '已截止';
    if (diffDays === 0) return '今天截止';
    if (diffDays === 1) return '明天截止';
    return `${diffDays}天后截止`;
  };
  
  if (!currentUser) return null;
  
  if (isLoading) {
    return (
      <div className="min-h-screen bg-background flex items-center justify-center">
        <Loader2 className="w-8 h-8 animate-spin text-primary" />
      </div>
    );
  }
  
  return (
    <div className="min-h-screen bg-background pb-20">
      {/* 顶部问候条 */}
      <header className="sticky top-0 z-10 bg-card border-b border-border px-4 py-3">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-lg font-semibold">
              {getGreeting()}，{currentUser.name}
            </h1>
            <p className="text-xs text-muted-foreground">
              今天也是进步的一天
            </p>
          </div>
          <StreakBadge days={streakDays} size="md" />
        </div>
      </header>
      
      <div className="p-4 space-y-4">
        {/* 今日任务区 */}
        <section>
          <div className="flex items-center justify-between mb-3">
            <h2 className="text-base font-semibold flex items-center gap-2">
              <span className="w-1.5 h-5 bg-primary rounded-full" />
              今日任务
            </h2>
            {todayTasks.length > 3 && (
              <button className="text-xs text-primary flex items-center">
                查看全部 {todayTasks.length} 个
                <ChevronRight className="w-3 h-3" />
              </button>
            )}
          </div>
          
          {todayTasks.length === 0 ? (
            <EmptyState
              title="今天没有老师布置的任务"
              description="休息也是积累，可以去自由背诵"
              icon="task"
              action={{
                label: '去自由背诵',
                onClick: () => navigate({ to: '/student/bank/self' })
              }}
            />
          ) : (
            <div className="space-y-3">
              {todayTasks.slice(0, 3).map((task) => {
                const isExpired = new Date(task.deadline) < new Date();
                const isTodayDeadline = formatDeadline(task.deadline) === '今天截止';
                
                return (
              <Card
                key={task.id}
                onClick={() => handleTaskClick(task.id)}
                className={cn(
                  'p-4 cursor-pointer transition-all hover:shadow-md',
                  isExpired && 'opacity-60'
                )}
              >
                <div className="flex items-start justify-between mb-2">
                  <h3 className="font-medium text-foreground flex-1 pr-2 flex items-center gap-2">
                    {task.source === 'teacher' && (
                      <span className="inline-flex items-center px-1.5 py-0.5 rounded text-[10px] font-medium bg-primary/10 text-primary">
                        老师
                      </span>
                    )}
                    {task.source === 'self' && (
                      <span className="inline-flex items-center px-1.5 py-0.5 rounded text-[10px] font-medium bg-amber-100 text-amber-700">
                        自建
                      </span>
                    )}
                    {task.title}
                  </h3>
                      {isExpired && (
                        <span className="text-xs text-destructive font-medium">
                          已截止
                        </span>
                      )}
                      {isTodayDeadline && !isExpired && (
                        <span className="text-xs text-amber-600 font-medium flex items-center gap-1">
                          <AlertCircle className="w-3 h-3" />
                          今天截止
                        </span>
                      )}
                    </div>
                    
                    <div className="flex items-center gap-4 text-xs text-muted-foreground mb-3">
                      <span className="flex items-center gap-1">
                        <Users className="w-3 h-3" />
                        {task.teacherName}
                      </span>
                      <span className="flex items-center gap-1">
                        <Clock className="w-3 h-3" />
                        {formatDeadline(task.deadline)}
                      </span>
                    </div>
                    
                    <TaskProgressBar
                      current={task.completedQuestions}
                      total={task.totalQuestions}
                      size="sm"
                    />
                  </Card>
                );
              })}
            </div>
          )}
        </section>
        
        {/* 到期复习区 */}
        {(todayReviews.length > 0 || overdueReviews.length > 0) && (
          <section>
            <div className="flex items-center justify-between mb-3">
              <h2 className="text-base font-semibold flex items-center gap-2">
                <span className="w-1.5 h-5 bg-amber-500 rounded-full" />
                该复习了
                <span className="text-xs text-muted-foreground">
                  ({todayReviews.length + overdueReviews.length}个知识点)
                </span>
              </h2>
              <button 
                onClick={handleReviewClick}
                className="text-xs text-primary flex items-center"
              >
                去复习
                <ChevronRight className="w-3 h-3" />
              </button>
            </div>
            
            <div className="space-y-2">
              {overdueReviews.map((item) => (
                <Card
                  key={item.id}
                  onClick={handleReviewClick}
                  className="p-3 cursor-pointer border-l-4 border-l-destructive"
                >
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-2">
                      <MemoryStateBadge state={item.memoryState} size="sm" />
                      <span className="font-medium">{item.title}</span>
                    </div>
                    <span className="text-xs text-destructive">
                      逾期{item.daysSinceLastReview}天
                    </span>
                  </div>
                </Card>
              ))}
              
              {todayReviews.slice(0, 3 - overdueReviews.length).map((item) => (
                <Card
                  key={item.id}
                  onClick={handleReviewClick}
                  className="p-3 cursor-pointer"
                >
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-2">
                      <MemoryStateBadge state={item.memoryState} size="sm" />
                      <span className="font-medium">{item.title}</span>
                    </div>
                    <span className="text-xs text-muted-foreground">
                      {item.daysSinceLastReview}天前
                    </span>
                  </div>
                </Card>
              ))}
            </div>
          </section>
        )}
        
        {/* 快捷入口 */}
        <section>
          <h2 className="text-base font-semibold mb-3 flex items-center gap-2">
            <span className="w-1.5 h-5 bg-secondary rounded-full" />
            快捷入口
          </h2>
          
          <div className="grid grid-cols-2 gap-3">
            <Button
              variant="outline"
              className="h-20 flex flex-col items-center justify-center gap-2"
              onClick={() => navigate({ to: '/student/bank/self' })}
            >
              <BookOpen className="w-6 h-6 text-primary" />
              <span className="text-sm">自由背诵</span>
            </Button>
            
            <Button
              variant="outline"
              className="h-20 flex flex-col items-center justify-center gap-2"
              onClick={() => navigate({ to: '/student/stats/wrong' })}
            >
              <Trophy className="w-6 h-6 text-amber-500" />
              <span className="text-sm">错题本</span>
            </Button>
            
            <Button
              variant="outline"
              className="h-20 flex flex-col items-center justify-center gap-2"
              onClick={() => navigate({ to: '/student/stats/heatmap' })}
            >
              <BarChart3 className="w-6 h-6 text-green-500" />
              <span className="text-sm">热力图</span>
            </Button>
            
            <Button
              variant="outline"
              className="h-20 flex flex-col items-center justify-center gap-2"
              onClick={() => navigate({ to: '/student/study/review' })}
            >
              <Users className="w-6 h-6 text-blue-500" />
              <span className="text-sm">PK竞技</span>
            </Button>
          </div>
        </section>
        
        {/* 本周热力图 */}
        <section>
          <h2 className="text-base font-semibold mb-3 flex items-center gap-2">
            <span className="w-1.5 h-5 bg-green-500 rounded-full" />
            本周努力
          </h2>
          
          <Card className="p-4">
            <Heatmap data={weeklyHeatmapData} />
          </Card>
        </section>
      </div>
      
      {/* 底部导航 */}
      <BottomNav />
    </div>
  );
}

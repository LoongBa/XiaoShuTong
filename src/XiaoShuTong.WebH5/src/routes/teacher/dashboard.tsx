import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { useEffect, useState } from 'react';
import { useAppStore } from '@/store/appStore';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { TaskProgressBar } from '@/components/TaskProgressBar';
import { 
  ArrowLeft,
  Users,
  BookOpen,
  AlertCircle,
  TrendingUp,
  Clock,
  ChevronRight,
  Plus,
  BarChart3,
  Loader2
} from 'lucide-react';
import { cn } from '@/lib/utils';
import { Tkwf } from '@tkwf/tsclient';
import type { 
  Groups_ExecuteService, 
  OwnerDashboard_ExecuteService,
  Tasks_ExecuteService
} from '@/gql/ts-client.g';

export const Route = createFileRoute('/teacher/dashboard')({
  component: TeacherDashboardPage,
});

function TeacherDashboardPage() {
  const navigate = useNavigate();
  const { isLoggedIn, currentUser } = useAppStore();
  
  const [groups, setGroups] = useState<any[]>([]);
  const [selectedGroup, setSelectedGroup] = useState<any>(null);
  const [dashboardData, setDashboardData] = useState<any>(null);
  const [tasks, setTasks] = useState<any[]>([]);
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
        // 加载班级列表
        const groupsResult = await Tkwf.User.Use<Groups_ExecuteService>().listGroups_Execute({
          request: { pageIndex: 1, pageSize: 10 }
        });
        
        if (groupsResult.success && groupsResult.items) {
          setGroups(groupsResult.items);
          if (groupsResult.items.length > 0) {
            setSelectedGroup(groupsResult.items[0]);
          }
        }
        
        // 加载任务列表
        const tasksResult = await Tkwf.User.Use<Tasks_ExecuteService>().listTasks_Execute({
          request: { groupUid: null, status: null, pageIndex: 1, pageSize: 10 }
        });
        
        if (tasksResult.success && tasksResult.items) {
          setTasks(tasksResult.items);
        }
      } catch (error) {
        console.error('加载数据失败:', error);
      } finally {
        setIsLoading(false);
      }
    };
    
    loadData();
  }, [isLoggedIn]);
  
  // 加载班级详情
  useEffect(() => {
    if (!selectedGroup) return;
    
    const loadDashboard = async () => {
      try {
        const result = await Tkwf.User.Use<OwnerDashboard_ExecuteService>().ownerDashboard_Execute({
          // P1-3：契约 GetOwnerDashboardReqDtoInput.groupUid 为字符串 UId，非数值 groupId 转串
          request: { groupUid: selectedGroup.groupUid }
        });
        
        if (result.success) {
          setDashboardData(result);
        }
      } catch (error) {
        console.error('加载看板数据失败:', error);
      }
    };
    
    loadDashboard();
  }, [selectedGroup]);
  
  // 计算统计数据（P1-4：字段对齐 GetOwnerDashboardResDto/TaskListItemDto 契约）
  const todayExecutionRate = dashboardData?.todayExecutionRate || 0;
  const avgProgress = dashboardData?.avgProgress || 0;
  // 逾期任务：契约用 overdueCount（GetOwnerDashboardResDto），非从任务列表 filter status==='expired'
  const expiredTasks = dashboardData?.overdueCount ?? 0;
  // 薄弱点 Top5：契约 weakPointsTop5（WeakPointDto[]）
  const weakPointCount = dashboardData?.weakPointsTop5?.length ?? 0;
  
  // 学生执行表：GetOwnerDashboardResDto 无 students 字段（契约缺失项），改为展示 taskList 任务执行明细
  // （DashboardTaskDto：task: TasksDto | null + completionRate + status）；学生明细表待后端补契约
  const taskExecutions: { taskUid: string; title: string; completionRate: number; status: string }[] =
    (dashboardData?.taskList ?? []).map((t: any) => ({
      taskUid: t.task?.uId ?? '',
      title: t.task?.title ?? '(未知任务)',
      completionRate: t.completionRate ?? 0,
      status: t.status ?? ''
    }));
  
  return (
    <div className="min-h-screen bg-background">
      {/* 顶部导航 */}
      <header className="sticky top-0 z-10 bg-card border-b border-border px-4 py-3">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-3">
            <button
              onClick={() => navigate({ to: '/student/home' })}
              className="p-2 -ml-2 rounded-full hover:bg-muted transition-colors"
            >
              <ArrowLeft className="w-5 h-5" />
            </button>
            <h1 className="font-semibold">班级看板</h1>
          </div>
          <Button size="sm" onClick={() => navigate({ to: '/teacher/tasks/create' })}>
            <Plus className="w-4 h-4 mr-1" />
            布置任务
          </Button>
        </div>
        
        {/* 班级选择器 */}
        <div className="mt-3 flex gap-2">
          {groups.map((group) => (
            <button
              key={group.groupId}
              onClick={() => setSelectedGroup(group)}
              className={cn(
                'px-3 py-1.5 rounded-full text-sm font-medium transition-colors',
                selectedGroup?.groupId === group.groupId
                  ? 'bg-primary text-primary-foreground'
                  : 'bg-muted text-muted-foreground hover:bg-muted/80'
              )}
            >
              {group.name}
            </button>
          ))}
        </div>
      </header>
      
      <div className="p-4 space-y-4">
        {/* 指标卡组 */}
        <div className="grid grid-cols-2 gap-3">
          <Card className="p-4">
            <div className="flex items-center gap-2 mb-2">
              <div className="w-8 h-8 rounded-full bg-primary/10 flex items-center justify-center">
                <Users className="w-4 h-4 text-primary" />
              </div>
              <span className="text-xs text-muted-foreground">今日执行率</span>
            </div>
            <p className={cn(
              'text-2xl font-bold',
              todayExecutionRate < 50 ? 'text-destructive' : 'text-foreground'
            )}>
              {todayExecutionRate}%
            </p>
          </Card>
          
          <Card className="p-4">
            <div className="flex items-center gap-2 mb-2">
              <div className="w-8 h-8 rounded-full bg-green-100 flex items-center justify-center">
                <TrendingUp className="w-4 h-4 text-green-600" />
              </div>
              <span className="text-xs text-muted-foreground">平均进度</span>
            </div>
            <p className="text-2xl font-bold">{avgProgress}%</p>
          </Card>
          
          <Card className="p-4">
            <div className="flex items-center gap-2 mb-2">
              <div className="w-8 h-8 rounded-full bg-red-100 flex items-center justify-center">
                <AlertCircle className="w-4 h-4 text-red-600" />
              </div>
              <span className="text-xs text-muted-foreground">逾期任务</span>
            </div>
            <p className={cn(
              'text-2xl font-bold',
              expiredTasks > 0 ? 'text-destructive' : 'text-foreground'
            )}>
              {expiredTasks}
            </p>
          </Card>
          
          <Card className="p-4">
            <div className="flex items-center gap-2 mb-2">
              <div className="w-8 h-8 rounded-full bg-amber-100 flex items-center justify-center">
                <BarChart3 className="w-4 h-4 text-amber-600" />
              </div>
              <span className="text-xs text-muted-foreground">薄弱点Top5</span>
            </div>
            <p className="text-2xl font-bold">{weakPointCount}</p>
          </Card>
        </div>
        
        {/* 任务执行列表 */}
        <section>
          <h2 className="font-semibold mb-3 flex items-center gap-2">
            <BookOpen className="w-4 h-4" />
            任务执行
          </h2>
          <div className="space-y-2">
            {tasks.map((task) => {
              // P1-4：TaskListItemDto.status 为 PascalCase 枚举（Pending/InProgress/Completed/Expired）
              const isExpired = task.status === 'Expired';
              const pct = Math.round((task.completionRate ?? 0) * 100);
              return (
                <Card
                  key={task.taskUid || task.taskId || task.id}
                  className={cn(
                    'p-4 cursor-pointer hover:shadow-md transition-shadow',
                    isExpired && 'border-l-4 border-l-destructive'
                  )}
                  onClick={() => navigate({ to: '/teacher/tasks/detail', search: { taskUid: task.taskUid } })}
                >
                  <div className="flex items-start justify-between mb-2">
                    <h3 className="font-medium flex-1 pr-2">{task.title}</h3>
                    {isExpired && (
                      <span className="text-xs text-destructive font-medium">
                        已截止
                      </span>
                    )}
                  </div>
                  <div className="flex items-center gap-4 text-xs text-muted-foreground mb-2">
                    <span className="flex items-center gap-1">
                      <Clock className="w-3 h-3" />
                      {/* P1-4：契约字段 deadlineAt（非 deadline）；ISO 纯展示裁剪到日期 */}
                      {(task.deadlineAt ? String(task.deadlineAt).slice(0, 10) : '未设置')}
                    </span>
                  </div>
                  {/* P1-4：TaskListItemDto 无 completedCount/assignedCount，用 completionRate 展示百分进度 */}
                  <div className="text-xs text-muted-foreground mb-1">
                    完成率 {pct}%（共 {task.questionCount ?? 0} 题）
                  </div>
                  <TaskProgressBar
                    current={pct}
                    total={100}
                    size="sm"
                  />
                </Card>
              );
            })}
          </div>
        </section>
        
        {/* 任务执行明细表（P1-4：GetOwnerDashboardResDto 无 students 字段，契约当前无学生明细；
            展示 taskList 任务执行情况；学生明细待后端补契约） */}
        <section>
          <h2 className="font-semibold mb-3 flex items-center gap-2">
            <Users className="w-4 h-4" />
            任务执行明细
          </h2>
          <Card className="overflow-hidden">
            <div className="divide-y">
              {taskExecutions.map((t, index) => (
                <div
                  key={t.taskUid || index}
                  className="p-3 flex items-center gap-3 hover:bg-muted/50 transition-colors"
                >
                  <span className="text-sm text-muted-foreground w-6">
                    {index + 1}
                  </span>
                  <div className="flex-1 min-w-0">
                    <p className="font-medium text-sm">{t.title}</p>
                    <p className="text-xs text-muted-foreground">{t.status}</p>
                  </div>
                  <div className="text-right">
                    <p className={cn(
                      'text-sm font-medium',
                      t.completionRate >= 0.8 ? 'text-green-600' :
                      t.completionRate >= 0.6 ? 'text-amber-600' : 'text-destructive'
                    )}>
                      {Math.round(t.completionRate * 100)}%
                    </p>
                  </div>
                </div>
              ))}
              {taskExecutions.length === 0 && (
                <div className="p-4 text-center text-sm text-muted-foreground">
                  暂无任务执行数据
                </div>
              )}
            </div>
          </Card>
        </section>
      </div>
    </div>
  );
}

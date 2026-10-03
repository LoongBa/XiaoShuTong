import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { useEffect, useState } from 'react';
import { useAppStore } from '@/store/appStore';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { TaskProgressBar } from '@/components/TaskProgressBar';
import { Tkwf } from '@tkwf/tsclient';
import type {
  TaskDetail_ExecuteService,
  TasksDto,
  TaskAssignmentsDto,
} from '@/gql/ts-client.g';
import {
  ArrowLeft,
  Users,
  Clock,
  AlertCircle,
  Loader2,
  Share2,
} from 'lucide-react';

// 状态映射（TaskListItemDto.status 为 PascalCase：Pending/InProgress/Completed/Expired）
const TASK_STATUS_LABELS: Record<string, string> = {
  Pending: '待开始',
  InProgress: '进行中',
  Completed: '已完成',
  Expired: '已截止',
};
const MEMBER_STATUS_LABELS: Record<string, string> = {
  Pending: '未开始',
  InProgress: '进行中',
  Completed: '已完成',
};
const statusLabelOf = (status: string | null | undefined, labels: Record<string, string>): string =>
  labels[status ?? ''] ?? status ?? '未知';

export const Route = createFileRoute('/teacher/tasks/detail')({
  validateSearch: (search: Record<string, unknown>): { taskUid?: string } => ({
    taskUid: typeof search.taskUid === 'string' ? search.taskUid : undefined,
  }),
  component: TaskDetailPage,
});

function TaskDetailPage() {
  const navigate = useNavigate();
  const { isLoggedIn } = useAppStore();
  // 路由参数 taskUid（teacher/dashboard 任务卡跳转带入）
  const { taskUid } = Route.useSearch();

  // 真实数据源（taskDetail_Execute：task + members）
  const [task, setTask] = useState<TasksDto | null>(null);
  const [members, setMembers] = useState<TaskAssignmentsDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState('');

  // 检查登录状态
  useEffect(() => {
    if (!isLoggedIn) {
      navigate({ to: '/auth/login' });
    }
  }, [isLoggedIn, navigate]);

  // 按 taskUid 拉取任务详情
  useEffect(() => {
    if (!isLoggedIn || !taskUid) return;
    let cancelled = false;

    const load = async () => {
      setIsLoading(true);
      setLoadError('');
      try {
        const res = await Tkwf.User.Use<TaskDetail_ExecuteService>().taskDetail_Execute({
          request: { taskUid },
        });
        if (cancelled) return;
        if (!res.success) {
          setLoadError(res.errorCode || '加载失败');
          return;
        }
        setTask(res.task);
        setMembers(res.members ?? []);
      } catch (err: any) {
        if (!cancelled) {
          // RPC 抛错 → 读 err.code
          setLoadError(err?.code || err?.message || '加载失败');
        }
      } finally {
        if (!cancelled) setIsLoading(false);
      }
    };

    void load();
    return () => { cancelled = true; };
  }, [isLoggedIn, taskUid]);

  const completedCount = members.filter((m) => m.status === 'Completed').length;
  const totalStudents = members.length;
  const completionRate = totalStudents > 0
    ? Math.round(completedCount / totalStudents * 100)
    : 0;

  // 薄弱点：契约 GetTaskDetailResDto 无薄弱点字段 → 从 members progress 最低的 3 人推导
  //（登记：真实薄弱知识点需后端补契约，如 GetWeaknessReportResDto 的 weakPoints）
  const weakMembers = [...members]
    .sort((a, b) => (a.progress ?? 0) - (b.progress ?? 0))
    .slice(0, 3);

  return (
    <div className="min-h-screen bg-background">
      {/* 顶部导航 */}
      <header className="sticky top-0 z-10 bg-card border-b border-border px-4 py-3">
        <div className="flex items-center gap-3">
          <button
            type="button"
            onClick={() => navigate({ to: '/teacher/dashboard' })}
            className="p-2 -ml-2 rounded-full hover:bg-muted transition-colors"
          >
            <ArrowLeft className="w-5 h-5" />
          </button>
          <h1 className="font-semibold">任务详情</h1>
        </div>
      </header>

      <div className="p-4 space-y-4">
        {isLoading ? (
          <div className="flex items-center justify-center py-12 text-muted-foreground">
            <Loader2 className="w-5 h-5 animate-spin mr-2" />
            任务详情加载中…
          </div>
        ) : loadError ? (
          <Card className="p-6 text-center">
            <AlertCircle className="w-8 h-8 text-destructive mx-auto mb-2" />
            <p className="text-sm text-destructive">{loadError}</p>
          </Card>
        ) : !task ? (
          <Card className="p-6 text-center text-sm text-muted-foreground">
            任务不存在或已删除
          </Card>
        ) : (
          <>
            {/* 任务信息 */}
            <Card className="p-4">
              <div className="flex items-start justify-between gap-2">
                <h2 className="font-semibold text-lg mb-2">{task.title}</h2>
                {/* V0.8.1：任务编辑入口（UpdateTaskService 消费端接入） */}
                <Button
                  variant="outline"
                  size="sm"
                  className="shrink-0"
                  onClick={() =>
                    navigate({ to: '/teacher/tasks/edit', search: { taskUid } })
                  }
                >
                  编辑任务
                </Button>
              </div>
              {task.description && (
                <p className="text-sm text-muted-foreground mb-2">{task.description}</p>
              )}
              <div className="flex items-center gap-4 text-sm text-muted-foreground mb-4">
                <span className="flex items-center gap-1">
                  <Clock className="w-4 h-4" />
                  截止 {task.deadlineAt ? String(task.deadlineAt).slice(0, 10) : '未设置'}
                </span>
                <span className="flex items-center gap-1">
                  <Users className="w-4 h-4" />
                  {completedCount}/{totalStudents} 人完成
                </span>
                <span className="text-xs px-2 py-0.5 bg-muted rounded-full">
                  {statusLabelOf(task.status, TASK_STATUS_LABELS)}
                </span>
              </div>
              <TaskProgressBar
                current={completionRate}
                total={100}
                size="md"
              />
            </Card>

            {/* 薄弱学生（契约无薄弱知识点 → progress 最低推导） */}
            <section>
              <h2 className="font-semibold mb-3 flex items-center gap-2">
                <AlertCircle className="w-4 h-4 text-amber-500" />
                进度落后 Top3
              </h2>
              {weakMembers.length === 0 ? (
                <Card className="p-3 text-center text-sm text-muted-foreground">
                  暂无学生数据
                </Card>
              ) : (
                <div className="space-y-2">
                  {weakMembers.map((m, index) => (
                    <Card key={m.uId} className="p-3">
                      <div className="flex items-center justify-between">
                        <div className="flex items-center gap-2">
                          <span className="text-sm text-muted-foreground">{index + 1}</span>
                          <span className="font-medium">
                            {/* 契约无 userName，昵称/姓名跨账户域不可得 → 展示 userId 占位 */}
                            学生 #{m.userId}
                          </span>
                        </div>
                        <span className="text-xs text-destructive">
                          进度 {Math.round((m.progress ?? 0) * 100)}%
                        </span>
                      </div>
                    </Card>
                  ))}
                </div>
              )}
              <Button variant="outline" className="w-full mt-2" size="sm">
                布置补救任务
              </Button>
            </section>

            {/* 学生完成情况 */}
            <section>
              <h2 className="font-semibold mb-3 flex items-center gap-2">
                <Users className="w-4 h-4" />
                学生完成情况
              </h2>
              <Card className="overflow-hidden">
                <div className="divide-y">
                  {members.map((student) => (
                    <div
                      key={student.uId}
                      className="p-3 flex items-center gap-3"
                    >
                      <div className="w-8 h-8 rounded-full bg-primary/10 flex items-center justify-center text-sm font-medium">
                        {`#${student.userId}`.slice(0, 1)}
                      </div>
                      <div className="flex-1 min-w-0">
                        {/* 契约无 userName → userId 占位（登记：跨账户域昵称不可得） */}
                        <p className="font-medium text-sm">学生 #{student.userId}</p>
                        <p className="text-xs text-muted-foreground">
                          {statusLabelOf(student.status, MEMBER_STATUS_LABELS)}
                        </p>
                      </div>
                      <div className="text-right">
                        <p className="text-sm font-medium">
                          {Math.round((student.progress ?? 0) * 100)}%
                        </p>
                        <p className="text-xs text-muted-foreground">
                          {student.completedAt ? String(student.completedAt).slice(0, 10) : '未完成'}
                        </p>
                      </div>
                    </div>
                  ))}
                  {members.length === 0 && (
                    <div className="p-4 text-center text-sm text-muted-foreground">
                      暂无学生数据
                    </div>
                  )}
                </div>
              </Card>
            </section>

            {/* 分享按钮 */}
            <Button variant="outline" className="w-full" size="lg">
              <Share2 className="w-4 h-4 mr-2" />
              分享到家长群
            </Button>
          </>
        )}
      </div>
    </div>
  );
}

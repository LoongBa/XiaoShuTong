import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { useEffect } from 'react';
import { useAppStore } from '@/store/appStore';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { TaskProgressBar } from '@/components/TaskProgressBar';
import { MemoryStateBadge } from '@/components/MemoryStateBadge';
import { 
  ArrowLeft,
  Users,
  Clock,
  AlertCircle,
  ChevronRight,
  Share2
} from 'lucide-react';
import { cn } from '@/lib/utils';

// 模拟任务详情数据
const MOCK_TASK_DETAIL = {
  id: 'task-1',
  title: '背诵《人文地理·第一章》',
  description: '地理基础知识背诵',
  teacherName: '王老师',
  totalQuestions: 10,
  completedCount: 35,
  totalStudents: 45,
  deadline: '明天 18:00',
  status: 'active',
  expired: false,
};

// 模拟学生完成详情
const MOCK_STUDENT_DETAILS = [
  { userId: 's1', userName: '小明', completed: 10, correct: 9, time: '15分钟', status: 'completed' },
  { userId: 's2', userName: '小红', completed: 8, correct: 7, time: '20分钟', status: 'in_progress' },
  { userId: 's3', userName: '小刚', completed: 4, correct: 3, time: '10分钟', status: 'in_progress' },
  { userId: 's4', userName: '小丽', completed: 10, correct: 10, time: '12分钟', status: 'completed' },
  { userId: 's5', userName: '小华', completed: 0, correct: 0, time: '-', status: 'not_started' },
];

// 模拟薄弱知识点（memoryState 对齐 MemoryState 联合类型，mock/真实切换不改页面代码）
const MOCK_WEAK_POINTS = [
  { id: 'wp1', title: '黄河流域地理特征', wrongCount: 12, memoryState: 'gray' as const },
  { id: 'wp2', title: '长江三峡', wrongCount: 8, memoryState: 'yellow' as const },
  { id: 'wp3', title: '五岳名山', wrongCount: 6, memoryState: 'yellow' as const },
];

export const Route = createFileRoute('/teacher/tasks/detail')({
  component: TaskDetailPage,
});

function TaskDetailPage() {
  const navigate = useNavigate();
  const { isLoggedIn } = useAppStore();
  
  // 检查登录状态
  useEffect(() => {
    if (!isLoggedIn) {
      navigate({ to: '/auth/login' });
    }
  }, [isLoggedIn, navigate]);
  
  const completionRate = Math.round(
    MOCK_TASK_DETAIL.completedCount / MOCK_TASK_DETAIL.totalStudents * 100
  );
  
  return (
    <div className="min-h-screen bg-background">
      {/* 顶部导航 */}
      <header className="sticky top-0 z-10 bg-card border-b border-border px-4 py-3">
        <div className="flex items-center gap-3">
          <button
            onClick={() => navigate({ to: '/teacher/dashboard' })}
            className="p-2 -ml-2 rounded-full hover:bg-muted transition-colors"
          >
            <ArrowLeft className="w-5 h-5" />
          </button>
          <h1 className="font-semibold">任务详情</h1>
        </div>
      </header>
      
      <div className="p-4 space-y-4">
        {/* 任务信息 */}
        <Card className="p-4">
          <h2 className="font-semibold text-lg mb-2">{MOCK_TASK_DETAIL.title}</h2>
          <div className="flex items-center gap-4 text-sm text-muted-foreground mb-4">
            <span className="flex items-center gap-1">
              <Clock className="w-4 h-4" />
              截止 {MOCK_TASK_DETAIL.deadline}
            </span>
            <span className="flex items-center gap-1">
              <Users className="w-4 h-4" />
              {MOCK_TASK_DETAIL.completedCount}/{MOCK_TASK_DETAIL.totalStudents} 人完成
            </span>
          </div>
          <TaskProgressBar
            current={MOCK_TASK_DETAIL.completedCount}
            total={MOCK_TASK_DETAIL.totalStudents}
            size="md"
          />
        </Card>
        
        {/* 薄弱知识点 */}
        <section>
          <h2 className="font-semibold mb-3 flex items-center gap-2">
            <AlertCircle className="w-4 h-4 text-amber-500" />
            薄弱知识点 Top3
          </h2>
          <div className="space-y-2">
            {MOCK_WEAK_POINTS.map((point, index) => (
              <Card key={point.id} className="p-3">
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <span className="text-sm text-muted-foreground">{index + 1}</span>
                    <span className="font-medium">{point.title}</span>
                  </div>
                  <div className="flex items-center gap-2">
                    <MemoryStateBadge state={point.memoryState} size="sm" />
                    <span className="text-xs text-destructive">
                      {point.wrongCount}人错
                    </span>
                  </div>
                </div>
              </Card>
            ))}
          </div>
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
              {MOCK_STUDENT_DETAILS.map((student) => (
                <div
                  key={student.userId}
                  className="p-3 flex items-center gap-3"
                >
                  <div className="w-8 h-8 rounded-full bg-primary/10 flex items-center justify-center text-sm font-medium">
                    {student.userName.charAt(0)}
                  </div>
                  <div className="flex-1 min-w-0">
                    <p className="font-medium text-sm">{student.userName}</p>
                    <p className="text-xs text-muted-foreground">
                      {student.status === 'completed' ? '已完成' :
                       student.status === 'in_progress' ? '进行中' : '未开始'}
                    </p>
                  </div>
                  <div className="text-right">
                    <p className="text-sm font-medium">
                      {student.completed}/{MOCK_TASK_DETAIL.totalQuestions}
                    </p>
                    <p className="text-xs text-muted-foreground">
                      {student.time}
                    </p>
                  </div>
                </div>
              ))}
            </div>
          </Card>
        </section>
        
        {/* 分享按钮 */}
        <Button variant="outline" className="w-full" size="lg">
          <Share2 className="w-4 h-4 mr-2" />
          分享到家长群
        </Button>
      </div>
    </div>
  );
}

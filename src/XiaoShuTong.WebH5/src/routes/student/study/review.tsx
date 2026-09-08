import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { useEffect } from 'react';
import { useAppStore } from '@/store/appStore';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { MemoryStateBadge } from '@/components/MemoryStateBadge';
import { EmptyState } from '@/components/EmptyState';
import { BottomNav } from '@/components/BottomNav';
import { 
  ArrowLeft,
  Clock,
  AlertCircle,
  ChevronRight
} from 'lucide-react';
import { cn } from '@/lib/utils';

export const Route = createFileRoute('/student/study/review')({
  component: ReviewQueuePage,
});

function ReviewQueuePage() {
  const navigate = useNavigate();
  const { isLoggedIn, reviewItems } = useAppStore();
  
  // 检查登录状态
  useEffect(() => {
    if (!isLoggedIn) {
      navigate({ to: '/auth/login' });
    }
  }, [isLoggedIn, navigate]);
  
  // 分组：今日到期和已逾期
  const overdueItems = reviewItems.filter(r => r.isOverdue);
  const todayItems = reviewItems.filter(r => !r.isOverdue);
  
  // 按记忆状态排序（△优先于○）
  const sortedTodayItems = [...todayItems].sort((a, b) => {
    const priority = { gray: 0, yellow: 1, green: 2, gold: 3 };
    return priority[a.memoryState] - priority[b.memoryState];
  });
  
  const handleReviewClick = (itemId: string) => {
    // 进入复习会话
    navigate({ to: '/student/study/task' });
  };
  
  const totalItems = reviewItems.length;
  
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
          <h1 className="font-semibold">复习队列</h1>
          {totalItems > 0 && (
            <span className="text-xs text-muted-foreground">
              ({totalItems}个知识点)
            </span>
          )}
        </div>
      </header>
      
      <div className="p-4 space-y-4">
        {totalItems === 0 ? (
          <EmptyState
            title="今天没有要复习的"
            description="记得很牢！继续保持"
            icon="review"
            action={{
              label: '回首页看看',
              onClick: () => navigate({ to: '/student/home' })
            }}
          />
        ) : (
          <>
            {/* 已逾期 */}
            {overdueItems.length > 0 && (
              <section>
                <h2 className="text-sm font-semibold text-destructive mb-3 flex items-center gap-2">
                  <AlertCircle className="w-4 h-4" />
                  已逾期 ({overdueItems.length})
                </h2>
                <div className="space-y-2">
                  {overdueItems.map((item) => (
                    <Card
                      key={item.id}
                      onClick={() => handleReviewClick(item.id)}
                      className="p-4 cursor-pointer border-l-4 border-l-destructive hover:shadow-md transition-shadow"
                    >
                      <div className="flex items-center justify-between">
                        <div className="flex items-center gap-3">
                          <MemoryStateBadge state={item.memoryState} size="md" />
                          <div>
                            <h3 className="font-medium">{item.title}</h3>
                            <p className="text-xs text-muted-foreground flex items-center gap-1 mt-0.5">
                              <Clock className="w-3 h-3" />
                              逾期 {item.daysSinceLastReview} 天
                            </p>
                          </div>
                        </div>
                        <ChevronRight className="w-5 h-5 text-muted-foreground" />
                      </div>
                    </Card>
                  ))}
                </div>
              </section>
            )}
            
            {/* 今日到期 */}
            {sortedTodayItems.length > 0 && (
              <section>
                <h2 className="text-sm font-semibold text-foreground mb-3 flex items-center gap-2">
                  <Clock className="w-4 h-4 text-amber-500" />
                  今日到期 ({sortedTodayItems.length})
                </h2>
                <div className="space-y-2">
                  {sortedTodayItems.map((item) => (
                    <Card
                      key={item.id}
                      onClick={() => handleReviewClick(item.id)}
                      className="p-4 cursor-pointer hover:shadow-md transition-shadow"
                    >
                      <div className="flex items-center justify-between">
                        <div className="flex items-center gap-3">
                          <MemoryStateBadge state={item.memoryState} size="md" />
                          <div>
                            <h3 className="font-medium">{item.title}</h3>
                            <p className="text-xs text-muted-foreground flex items-center gap-1 mt-0.5">
                              <Clock className="w-3 h-3" />
                              {item.daysSinceLastReview} 天前复习
                            </p>
                          </div>
                        </div>
                        <ChevronRight className="w-5 h-5 text-muted-foreground" />
                      </div>
                    </Card>
                  ))}
                </div>
              </section>
            )}
            
            {/* 复习提示 */}
            <Card className="p-4 bg-primary/5 border-primary/20">
              <div className="flex items-start gap-3">
                <div className="w-8 h-8 rounded-full bg-primary/10 flex items-center justify-center flex-shrink-0">
                  <span className="text-sm">💡</span>
                </div>
                <div>
                  <h3 className="font-medium text-sm mb-1">复习小贴士</h3>
                  <p className="text-xs text-muted-foreground">
                    根据记忆曲线，今天复习的内容会记得更牢。坚持复习，知识才能真正变成你的！
                  </p>
                </div>
              </div>
            </Card>
          </>
        )}
      </div>
      
      {/* 底部导航 */}
      <BottomNav />
    </div>
  );
}

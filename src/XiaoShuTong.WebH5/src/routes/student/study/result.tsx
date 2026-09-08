import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { useEffect, useState } from 'react';
import { useAppStore } from '@/store/appStore';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { MemoryStateBadge } from '@/components/MemoryStateBadge';
import { 
  Sparkles, 
  Star,
  RotateCcw,
  Home,
  Share2
} from 'lucide-react';
import { cn } from '@/lib/utils';

export const Route = createFileRoute('/student/study/result')({
  component: StudyResultPage,
});

function StudyResultPage() {
  const navigate = useNavigate();
  const { isLoggedIn, answers } = useAppStore();
  const [showAnimation, setShowAnimation] = useState(true);
  
  // 检查登录状态
  useEffect(() => {
    if (!isLoggedIn) {
      navigate({ to: '/auth/login' });
    }
  }, [isLoggedIn, navigate]);
  
  // 隐藏动画
  useEffect(() => {
    const timer = setTimeout(() => {
      setShowAnimation(false);
    }, 2000);
    return () => clearTimeout(timer);
  }, []);
  
  // 计算结果
  const totalQuestions = Object.keys(answers).length;
  const correctCount = Object.values(answers).filter(a => a.isCorrect).length;
  const hintCount = Object.values(answers).filter(a => a.usedHint && a.isCorrect).length;
  const wrongCount = totalQuestions - correctCount;
  const newStars = correctCount;
  
  // 卡住的知识点（模拟数据）
  const stuckItems = [
    { id: '1', title: '滕王阁序·落霞句', state: 'gray' as const, reason: '卡壳' },
    { id: '2', title: '蜀道难·开头', state: 'yellow' as const, reason: '求助后对' },
  ];
  
  const isAllCorrect = wrongCount === 0;
  
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
          
          {/* 结果统计 */}
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

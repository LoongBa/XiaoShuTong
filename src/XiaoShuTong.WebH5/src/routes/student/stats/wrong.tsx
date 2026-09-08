import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { useEffect, useState } from 'react';
import { useAppStore } from '@/store/appStore';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { EmptyState } from '@/components/EmptyState';
import { BottomNav } from '@/components/BottomNav';
import { 
  ArrowLeft,
  RotateCcw,
  CheckCircle2,
  BookOpen,
  X
} from 'lucide-react';
import { cn } from '@/lib/utils';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';

export const Route = createFileRoute('/student/stats/wrong')({
  component: WrongAnswersPage,
});

function WrongAnswersPage() {
  const navigate = useNavigate();
  const { isLoggedIn, wrongAnswers, setWrongAnswers } = useAppStore();
  const [selectedWrong, setSelectedWrong] = useState<typeof wrongAnswers[0] | null>(null);
  const [activeTab, setActiveTab] = useState<'wrong' | 'mastered'>('wrong');
  
  // 检查登录状态
  useEffect(() => {
    if (!isLoggedIn) {
      navigate({ to: '/auth/login' });
    }
  }, [isLoggedIn, navigate]);
  
  const wrongItems = wrongAnswers.filter(w => !w.isMastered);
  const masteredItems = wrongAnswers.filter(w => w.isMastered);
  
  const handlePractice = (wrongId: string) => {
    // 进入练习模式
    navigate({ to: '/student/study/task' });
  };
  
  const handleMarkMastered = (wrongId: string) => {
    const updated = wrongAnswers.map(w => 
      w.id === wrongId ? { ...w, isMastered: true } : w
    );
    setWrongAnswers(updated);
  };
  
  const handleMoveBack = (wrongId: string) => {
    const updated = wrongAnswers.map(w => 
      w.id === wrongId ? { ...w, isMastered: false } : w
    );
    setWrongAnswers(updated);
  };
  
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
          <h1 className="font-semibold">错题本</h1>
        </div>
      </header>
      
      <div className="p-4 space-y-4">
        {/* 标签切换 */}
        <div className="flex gap-2">
          <button
            onClick={() => setActiveTab('wrong')}
            className={cn(
              'flex-1 py-2 px-4 rounded-lg text-sm font-medium transition-colors',
              activeTab === 'wrong' 
                ? 'bg-primary text-primary-foreground' 
                : 'bg-muted text-muted-foreground'
            )}
          >
            待复习 ({wrongItems.length})
          </button>
          <button
            onClick={() => setActiveTab('mastered')}
            className={cn(
              'flex-1 py-2 px-4 rounded-lg text-sm font-medium transition-colors',
              activeTab === 'mastered' 
                ? 'bg-green-500 text-white' 
                : 'bg-muted text-muted-foreground'
            )}
          >
            已掌握 ({masteredItems.length})
          </button>
        </div>
        
        {/* 错题列表 */}
        {activeTab === 'wrong' && wrongItems.length === 0 && (
          <EmptyState
            title="没有错题"
            description="说明都会了，去挑战新内容吧"
            icon="wrong"
            action={{
              label: '去挑战新内容',
              onClick: () => navigate({ to: '/student/bank/self' })
            }}
          />
        )}
        
        {activeTab === 'wrong' && wrongItems.length > 0 && (
          <div className="space-y-3">
            {wrongItems.map((item) => (
              <Card
                key={item.id}
                className="p-4"
              >
                <div className="flex items-start justify-between mb-3">
                  <h3 className="font-medium flex-1 pr-2">{item.title}</h3>
                  <span className="text-xs text-muted-foreground">
                    错{item.wrongCount}次
                  </span>
                </div>
                
                <div className="space-y-2 mb-3">
                  <div className="p-2 bg-red-50 rounded text-sm">
                    <span className="text-red-500 text-xs">你的答案：</span>
                    <span className="text-red-700">{item.wrongAnswer}</span>
                  </div>
                  <div className="p-2 bg-green-50 rounded text-sm">
                    <span className="text-green-500 text-xs">正确答案：</span>
                    <span className="text-green-700">{item.correctAnswer}</span>
                  </div>
                </div>
                
                <div className="flex gap-2">
                  <Button
                    variant="outline"
                    size="sm"
                    className="flex-1"
                    onClick={() => handlePractice(item.id)}
                  >
                    <RotateCcw className="w-3 h-3 mr-1" />
                    重练
                  </Button>
                  <Button
                    variant="outline"
                    size="sm"
                    className="flex-1 text-green-600 border-green-200 hover:bg-green-50"
                    onClick={() => handleMarkMastered(item.id)}
                  >
                    <CheckCircle2 className="w-3 h-3 mr-1" />
                    已掌握
                  </Button>
                </div>
              </Card>
            ))}
          </div>
        )}
        
        {/* 已掌握列表 */}
        {activeTab === 'mastered' && masteredItems.length === 0 && (
          <EmptyState
            title="还没有已掌握的错题"
            description="通过重练掌握错题，它们会出现在这里"
            icon="default"
          />
        )}
        
        {activeTab === 'mastered' && masteredItems.length > 0 && (
          <div className="space-y-3">
            {masteredItems.map((item) => (
              <Card
                key={item.id}
                className="p-4 opacity-70"
              >
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <CheckCircle2 className="w-4 h-4 text-green-500" />
                    <span className="font-medium">{item.title}</span>
                  </div>
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={() => handleMoveBack(item.id)}
                  >
                    移回
                  </Button>
                </div>
              </Card>
            ))}
          </div>
        )}
        
        {/* 提示 */}
        {activeTab === 'wrong' && wrongItems.length > 0 && (
          <Card className="p-4 bg-amber-50 border-amber-200">
            <div className="flex items-start gap-3">
              <BookOpen className="w-5 h-5 text-amber-600 flex-shrink-0 mt-0.5" />
              <div>
                <h3 className="font-medium text-sm text-amber-800 mb-1">复习建议</h3>
                <p className="text-xs text-amber-700">
                  连续答对2次后，错题会自动标记为"已掌握"。定期复习错题，让知识真正变成你的！
                </p>
              </div>
            </div>
          </Card>
        )}
      </div>
      
      {/* 底部导航 */}
      <BottomNav />
    </div>
  );
}

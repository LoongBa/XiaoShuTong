import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { useEffect, useState } from 'react';
import { useAppStore } from '@/store/appStore';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { EmptyState } from '@/components/EmptyState';
import { BottomNav } from '@/components/BottomNav';
import { 
  ArrowLeft,
  Lock,
  BookOpen,
  Sparkles,
  ChevronRight,
  Flame
} from 'lucide-react';
import { cn } from '@/lib/utils';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';

export const Route = createFileRoute('/student/bank/self')({
  component: SelfBankPage,
});

function SelfBankPage() {
  const navigate = useNavigate();
  const { isLoggedIn, subjects } = useAppStore();
  const [selectedSubject, setSelectedSubject] = useState<string | null>(null);
  const [showLockDialog, setShowLockDialog] = useState(false);
  
  // 检查登录状态
  useEffect(() => {
    if (!isLoggedIn) {
      navigate({ to: '/auth/login' });
    }
  }, [isLoggedIn, navigate]);
  
  const handleSubjectClick = (subject: typeof subjects[0]) => {
    if (subject.isLocked) {
      setShowLockDialog(true);
      return;
    }
    setSelectedSubject(subject.id);
    // 进入该学科的内容列表
  };
  
  const hotSubjects = subjects.filter(s => s.isHot);
  const otherSubjects = subjects.filter(s => !s.isHot);
  
  const getSubjectIcon = (icon: string) => {
    return icon;
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
          <h1 className="font-semibold">自由背诵</h1>
        </div>
      </header>
      
      <div className="p-4 space-y-6">
        {/* 热门学科 */}
        <section>
          <div className="flex items-center gap-2 mb-4">
            <Flame className="w-4 h-4 text-orange-500" />
            <h2 className="text-sm font-semibold">热门学科</h2>
          </div>
          
          <div className="grid grid-cols-2 gap-3">
            {hotSubjects.map((subject) => (
              <Card
                key={subject.id}
                onClick={() => handleSubjectClick(subject)}
                className={cn(
                  'p-4 cursor-pointer transition-all hover:shadow-md',
                  subject.isLocked && 'opacity-70'
                )}
              >
                <div className="flex items-start justify-between mb-3">
                  <span className="text-3xl">{getSubjectIcon(subject.icon)}</span>
                  {subject.isLocked && (
                    <Lock className="w-4 h-4 text-muted-foreground" />
                  )}
                </div>
                <h3 className="font-medium mb-1">{subject.name}</h3>
                <p className="text-xs text-muted-foreground">
                  {subject.knowledgeCount} 个知识点
                </p>
              </Card>
            ))}
          </div>
        </section>
        
        {/* 其他学科 */}
        <section>
          <h2 className="text-sm font-semibold mb-4">更多学科</h2>
          
          <div className="space-y-2">
            {otherSubjects.map((subject) => (
              <Card
                key={subject.id}
                onClick={() => handleSubjectClick(subject)}
                className={cn(
                  'p-4 cursor-pointer transition-all hover:shadow-md',
                  subject.isLocked && 'opacity-70'
                )}
              >
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-3">
                    <span className="text-2xl">{getSubjectIcon(subject.icon)}</span>
                    <div>
                      <h3 className="font-medium">{subject.name}</h3>
                      <p className="text-xs text-muted-foreground">
                        {subject.knowledgeCount} 个知识点
                      </p>
                    </div>
                  </div>
                  <div className="flex items-center gap-2">
                    {subject.isLocked && (
                      <span className="text-xs text-muted-foreground flex items-center gap-1">
                        <Lock className="w-3 h-3" />
                        需老师布置
                      </span>
                    )}
                    <ChevronRight className="w-5 h-5 text-muted-foreground" />
                  </div>
                </div>
              </Card>
            ))}
          </div>
        </section>
        
        {/* 推荐提示 */}
        <Card className="p-4 bg-gradient-to-r from-amber-50 to-orange-50 border-amber-200">
          <div className="flex items-start gap-3">
            <div className="w-8 h-8 rounded-full bg-amber-100 flex items-center justify-center flex-shrink-0">
              <Sparkles className="w-4 h-4 text-amber-600" />
            </div>
            <div>
              <h3 className="font-medium text-sm mb-1">今日推荐</h3>
              <p className="text-xs text-muted-foreground mb-2">
                根据你的学习记录，推荐背诵《唐诗三百首》中的经典篇目
              </p>
              <Button size="sm" variant="outline" className="text-xs">
                <BookOpen className="w-3 h-3 mr-1" />
                开始背诵
              </Button>
            </div>
          </div>
        </Card>
      </div>
      
      {/* 锁定提示弹窗 */}
      <Dialog open={showLockDialog} onOpenChange={setShowLockDialog}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              <Lock className="w-5 h-5" />
              学科未解锁
            </DialogTitle>
            <DialogDescription>
              该学科需要老师布置任务后才能开启。你可以先完成老师布置的任务，或者选择其他已解锁的学科进行背诵。
            </DialogDescription>
          </DialogHeader>
          <div className="flex gap-3 mt-4">
            <Button
              variant="outline"
              className="flex-1"
              onClick={() => setShowLockDialog(false)}
            >
              关闭
            </Button>
            <Button
              className="flex-1"
              onClick={() => {
                setShowLockDialog(false);
                navigate({ to: '/student/home' });
              }}
            >
              返回首页看任务
            </Button>
          </div>
        </DialogContent>
      </Dialog>
      
      {/* 底部导航 */}
      <BottomNav />
    </div>
  );
}

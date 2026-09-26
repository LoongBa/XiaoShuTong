import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { useEffect, useState } from 'react';
import { useAppStore } from '@/store/appStore';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { EmptyState } from '@/components/EmptyState';
import { BottomNav } from '@/components/BottomNav';
import { Tkwf } from '@tkwf/tsclient';
import type { Banks_ExecuteService } from '@/gql/ts-client.g';
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
  const { isLoggedIn, subjects, setSubjects } = useAppStore();
  const [selectedSubject, setSelectedSubject] = useState<string | null>(null);
  const [showLockDialog, setShowLockDialog] = useState(false);
  const [loading, setLoading] = useState(true);

  // 批次三：接 banks_Execute 拉真实题库 → 按科目聚合学科卡（替代纯空 store.subjects）
  useEffect(() => {
    if (!isLoggedIn) return;
    let cancelled = false;
    const loadBanks = async () => {
      setLoading(true);
      try {
        const res = await Tkwf.User.Use<Banks_ExecuteService>().listBanks_Execute({
          request: { subject: null, purpose: null, pageIndex: 1, pageSize: 100 },
        });
        if (cancelled || !res.success) return;
        // 按 subject 聚合：subject 值域含中文（"语文"）与英文代码（"chinese"），统一映射
        const cluster = new Map<string, { label: string; count: number }>();
        const labelOf = (raw: string | null | undefined): string => {
          const r = (raw ?? 'All').toLowerCase();
          const map: Record<string, string> = {
            '语文': '语文', chinese: '语文',
            '数学': '数学', math: '数学',
            '英语': '英语', english: '英语',
            '物理': '物理', physics: '物理',
            '化学': '化学', chemistry: '化学',
            '生物': '生物', biology: '生物',
            '历史': '历史', history: '历史',
            '地理': '地理', geography: '地理',
            '道德与法治': '道德与法治', daodeyufazhi: '道德与法治', politics: '道德与法治',
          };
          return map[r] ?? (r === 'all' ? '综合' : (raw ?? '综合'));
        };
        for (const it of res.items ?? []) {
          const label = labelOf(it.bank?.subject);
          const cur = cluster.get(label) ?? { label, count: 0 };
          cur.count += (it.questionCount ?? 0);
          cluster.set(label, cur);
        }
        // 组装 Subject[]：isHot 按题库覆盖数 ≥2 或首 3 门；isLocked 统一 false（自由背诵可用）
        const built = [...cluster.entries()]
          .sort((a, b) => b[1].count - a[1].count)
          .map(([label, info], idx) => ({
            id: label,
            name: label,
            icon: '📚',
            isHot: idx < 3,
            isLocked: false,
            knowledgeCount: info.count,
          }));
        if (!cancelled) setSubjects(built);
      } catch {
        // 加载失败保留空态（走查可见）
      } finally {
        if (!cancelled) setLoading(false);
      }
    };
    void loadBanks();
    return () => { cancelled = true; };
  }, [isLoggedIn, setSubjects]);
  
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

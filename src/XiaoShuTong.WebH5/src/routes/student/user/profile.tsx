import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { useEffect, useState } from 'react';
import { useAppStore } from '@/store/appStore';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { BottomNav } from '@/components/BottomNav';
import { MemoryStateBadge } from '@/components/MemoryStateBadge';
import { 
  User,
  Settings,
  FileText,
  Crown,
  LogOut,
  ChevronRight,
  Star,
  BookOpen,
  Target,
  Sun,
  Moon,
  SunMoon
} from 'lucide-react';
import { cn } from '@/lib/utils';
import { getStoredTheme, setTheme, type ThemeMode } from '@/lib/theme';
import { APP_TITLE } from '@/config/app';

export const Route = createFileRoute('/student/user/profile')({
  component: UserProfilePage,
});

/** 主题切换下拉项 */
const THEME_OPTIONS: { mode: ThemeMode; label: string; icon: React.ReactNode }[] = [
  { mode: 'light', label: '浅色', icon: <Sun className="w-4 h-4" /> },
  { mode: 'dark', label: '深色', icon: <Moon className="w-4 h-4" /> },
  { mode: 'system', label: '随系统', icon: <SunMoon className="w-4 h-4" /> },
];

function ThemeSwitcher() {
  const [theme, setThemeMode] = useState<ThemeMode>(getStoredTheme());
  const [open, setOpen] = useState(false);

  const toggle = () => setOpen(!open);

  const choose = (mode: ThemeMode) => {
    setTheme(mode);
    setThemeMode(mode);
    setOpen(false);
  };

  // 当前按钮图标 = 选择模式（浅→Sun 白天，深→Moon 黑夜，随系统→SunMoon 半明半暗）
  const ActiveIcon = theme === 'dark'
    ? Moon
    : theme === 'light'
      ? Sun
      : SunMoon;

  return (
    <div className="relative">
      <button
        onClick={toggle}
        aria-label="切换主题"
        className="p-2.5 rounded-full bg-primary/10 text-primary hover:bg-primary/20 transition-colors"
      >
        <ActiveIcon className="w-5 h-5" />
      </button>

      {open && (
        <>
          {/* 点击外部关闭 */}
          <div className="fixed inset-0 z-10" onClick={() => setOpen(false)} />
          <div className="absolute right-0 mt-2 w-40 z-20 rounded-xl border bg-popover text-popover-foreground shadow-lg overflow-hidden animate-slide-up">
            {THEME_OPTIONS.map((opt) => {
              const active = theme === opt.mode;
              return (
                <button
                  key={opt.mode}
                  onClick={() => choose(opt.mode)}
                  className={cn(
                    'w-full px-4 py-2.5 flex items-center gap-3 text-sm hover:bg-accent transition-colors',
                    active && 'bg-accent font-medium text-accent-foreground'
                  )}
                >
                  <span className={cn(active ? 'text-primary' : 'text-muted-foreground')}>
                    {opt.icon}
                  </span>
                  {opt.label}
                  {active && (
                    <span className="ml-auto text-primary">✓</span>
                  )}
                </button>
              );
            })}
          </div>
        </>
      )}
    </div>
  );
}

function UserProfilePage() {
  const navigate = useNavigate();
  const { isLoggedIn, currentUser, logout, knowledgePoints } = useAppStore();
  
  // 检查登录状态
  useEffect(() => {
    if (!isLoggedIn) {
      navigate({ to: '/auth/login' });
    }
  }, [isLoggedIn, navigate]);
  
  // 计算记忆状态统计
  const memoryStats = {
    gray: knowledgePoints.filter(kp => kp.memoryState === 'gray').length,
    yellow: knowledgePoints.filter(kp => kp.memoryState === 'yellow').length,
    green: knowledgePoints.filter(kp => kp.memoryState === 'green').length,
    gold: knowledgePoints.filter(kp => kp.memoryState === 'gold').length,
  };
  
  const handleLogout = () => {
    logout();
    navigate({ to: '/auth/login' });
  };
  
  if (!currentUser) return null;
  
  return (
    <div className="min-h-screen bg-background pb-20">
      {/* 顶部 */}
      <div className="bg-gradient-to-b from-primary/10 to-background px-4 pt-8 pb-6">
        <div className="flex items-center gap-4">
          {/* 头像 */}
          <div className="w-20 h-20 rounded-full bg-primary/20 flex items-center justify-center text-3xl font-bold text-primary">
            {currentUser.avatar ? (
              <img 
                src={currentUser.avatar} 
                alt={currentUser.name}
                className="w-full h-full rounded-full object-cover"
              />
            ) : (
              currentUser.name.charAt(0)
            )}
          </div>
          
          <div className="flex-1">
            <h1 className="text-xl font-bold">{currentUser.name}</h1>
            <p className="text-sm text-muted-foreground">
              {currentUser.role === 'student' ? '学生' : currentUser.role}
            </p>
            <div className="flex items-center gap-2 mt-2">
              <span className="text-xs px-2 py-1 bg-primary/10 text-primary rounded-full">
                🔥 连续{currentUser.streakDays}天
              </span>
            </div>
          </div>

          {/* 右上角主题切换 */}
          <ThemeSwitcher />
        </div>
      </div>
      
      <div className="px-4 space-y-4">
        {/* 记忆状态统计 */}
        <Card className="p-4">
          <h2 className="font-semibold mb-4 flex items-center gap-2">
            <Target className="w-4 h-4" />
            记忆状态
          </h2>
          <div className="grid grid-cols-4 gap-2">
            <div className="text-center p-3 bg-gray-100 rounded-lg">
              <p className="text-lg font-bold text-gray-600">{memoryStats.gray}</p>
              <MemoryStateBadge state="gray" size="sm" className="mt-1" />
            </div>
            <div className="text-center p-3 bg-amber-50 rounded-lg">
              <p className="text-lg font-bold text-amber-600">{memoryStats.yellow}</p>
              <MemoryStateBadge state="yellow" size="sm" className="mt-1" />
            </div>
            <div className="text-center p-3 bg-green-50 rounded-lg">
              <p className="text-lg font-bold text-green-600">{memoryStats.green}</p>
              <MemoryStateBadge state="green" size="sm" className="mt-1" />
            </div>
            <div className="text-center p-3 bg-amber-100 rounded-lg">
              <p className="text-lg font-bold text-amber-700">{memoryStats.gold}</p>
              <MemoryStateBadge state="gold" size="sm" className="mt-1" />
            </div>
          </div>
        </Card>
        
        {/* 功能菜单 */}
        <Card className="divide-y">
          <button 
            onClick={() => navigate({ to: '/parent/dashboard' })}
            className="w-full p-4 flex items-center justify-between hover:bg-muted/50 transition-colors"
          >
            <div className="flex items-center gap-3">
              <div className="w-8 h-8 rounded-full bg-amber-100 flex items-center justify-center">
                <Crown className="w-4 h-4 text-amber-600" />
              </div>
              <div>
                <p className="font-medium">家长报告</p>
                <p className="text-xs text-muted-foreground">查看成长报告</p>
              </div>
            </div>
            <ChevronRight className="w-5 h-5 text-muted-foreground" />
          </button>
          
          <button className="w-full p-4 flex items-center justify-between hover:bg-muted/50 transition-colors">
            <div className="flex items-center gap-3">
              <div className="w-8 h-8 rounded-full bg-blue-100 flex items-center justify-center">
                <FileText className="w-4 h-4 text-blue-600" />
              </div>
              <div>
                <p className="font-medium">学习记录</p>
                <p className="text-xs text-muted-foreground">查看历史学习数据</p>
              </div>
            </div>
            <ChevronRight className="w-5 h-5 text-muted-foreground" />
          </button>
          
          <button className="w-full p-4 flex items-center justify-between hover:bg-muted/50 transition-colors">
            <div className="flex items-center gap-3">
              <div className="w-8 h-8 rounded-full bg-gray-100 flex items-center justify-center">
                <Settings className="w-4 h-4 text-gray-600" />
              </div>
              <div>
                <p className="font-medium">设置</p>
                <p className="text-xs text-muted-foreground">账号、通知、隐私</p>
              </div>
            </div>
            <ChevronRight className="w-5 h-5 text-muted-foreground" />
          </button>
        </Card>
        
        {/* 退出登录 */}
        <Button
          variant="outline"
          className="w-full h-12 text-destructive border-destructive/20 hover:bg-destructive/5"
          onClick={handleLogout}
        >
          <LogOut className="w-4 h-4 mr-2" />
          退出登录
        </Button>
        
        {/* 版本信息 */}
        <p className="text-center text-xs text-muted-foreground">
          {APP_TITLE}
        </p>
      </div>
      
      {/* 底部导航 */}
      <BottomNav />
    </div>
  );
}

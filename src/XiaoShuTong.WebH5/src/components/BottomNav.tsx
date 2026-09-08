import { cn } from '@/lib/utils';
import { Link, useLocation } from '@tanstack/react-router';
import { Home, BookOpen, User, BarChart3, Trophy } from 'lucide-react';

interface NavItem {
  path: string;
  label: string;
  icon: React.ComponentType<{ className?: string }>;
}

const navItems: NavItem[] = [
  { path: '/student/home', label: '首页', icon: Home },
  { path: '/student/bank/self', label: '背诵', icon: BookOpen },
  { path: '/student/rank', label: '排名', icon: Trophy },
  { path: '/student/stats/heatmap', label: '统计', icon: BarChart3 },
  { path: '/student/user/profile', label: '我的', icon: User },
];

export function BottomNav() {
  const location = useLocation();
  
  return (
    <nav className="fixed bottom-0 left-0 right-0 bg-card border-t border-border safe-bottom z-50">
      <div className="flex justify-around items-center h-14 max-w-md mx-auto">
        {navItems.map((item) => {
          const isActive = location.pathname.startsWith(item.path);
          const Icon = item.icon;
          
          return (
            <Link
              key={item.path}
              to={item.path}
              className={cn(
                'flex flex-col items-center justify-center flex-1 h-full',
                'transition-colors duration-200',
                isActive
                  ? 'text-primary'
                  : 'text-muted-foreground hover:text-foreground'
              )}
            >
              <Icon
                className={cn(
                  'w-5 h-5 mb-0.5',
                  isActive && 'scale-110 transition-transform'
                )}
              />
              <span className="text-[10px] font-medium">{item.label}</span>
            </Link>
          );
        })}
      </div>
    </nav>
  );
}

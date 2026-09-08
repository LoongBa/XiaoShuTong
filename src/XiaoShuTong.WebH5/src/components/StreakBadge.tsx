import { cn } from '@/lib/utils';
import { Flame } from 'lucide-react';

interface StreakBadgeProps {
  days: number;
  showLabel?: boolean;
  size?: 'sm' | 'md' | 'lg';
  className?: string;
}

export function StreakBadge({
  days,
  showLabel = true,
  size = 'md',
  className,
}: StreakBadgeProps) {
  const isGold = days >= 7;
  
  const sizeClasses = {
    sm: 'text-[10px] gap-0.5',
    md: 'text-xs gap-1',
    lg: 'text-sm gap-1.5',
  };
  
  const iconSizes = {
    sm: 12,
    md: 14,
    lg: 18,
  };
  
  return (
    <span
      className={cn(
        'streak-badge',
        isGold ? 'streak-badge-gold' : 'streak-badge-normal',
        sizeClasses[size],
        className
      )}
    >
      <Flame
        size={iconSizes[size]}
        className={isGold ? 'text-amber-500' : 'text-orange-400'}
        fill={isGold ? '#f59e0b' : '#fb923c'}
      />
      {showLabel && (
        <span>
          连续{days}天
          {days >= 7 && '🔥'}
        </span>
      )}
    </span>
  );
}

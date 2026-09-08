import { cn } from '@/lib/utils';

interface TaskProgressBarProps {
  current: number;
  total: number;
  showText?: boolean;
  size?: 'sm' | 'md' | 'lg';
  className?: string;
}

export function TaskProgressBar({
  current,
  total,
  showText = true,
  size = 'md',
  className,
}: TaskProgressBarProps) {
  const percentage = total > 0 ? Math.round((current / total) * 100) : 0;
  
  const sizeClasses = {
    sm: 'h-1.5',
    md: 'h-2',
    lg: 'h-3',
  };
  
  return (
    <div className={cn('w-full', className)}>
      <div className={cn('progress-bar', sizeClasses[size])}>
        <div
          className="progress-bar-fill"
          style={{ width: `${percentage}%` }}
        />
      </div>
      {showText && (
        <div className="flex justify-between items-center mt-1.5 text-xs text-muted-foreground">
          <span>进度 {current}/{total}</span>
          <span>{percentage}%</span>
        </div>
      )}
    </div>
  );
}

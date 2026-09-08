import { cn } from '@/lib/utils';
import type { MemoryState } from '@/types';
import { MEMORY_STATE_CONFIG } from '@/types';

interface MemoryStateBadgeProps {
  state: MemoryState;
  showLabel?: boolean;
  showHelper?: boolean;
  size?: 'sm' | 'md' | 'lg';
  className?: string;
}

export function MemoryStateBadge({
  state,
  showLabel = true,
  showHelper = false,
  size = 'md',
  className,
}: MemoryStateBadgeProps) {
  const config = MEMORY_STATE_CONFIG[state];
  
  const sizeClasses = {
    sm: 'text-[10px] px-1.5 py-0.5',
    md: 'text-xs px-2.5 py-1',
    lg: 'text-sm px-3 py-1.5',
  };
  
  const stateClasses = {
    gray: 'memory-badge-gray',
    yellow: 'memory-badge-yellow',
    green: 'memory-badge-green',
    gold: 'memory-badge-gold',
  };
  
  return (
    <span
      className={cn(
        'memory-badge',
        stateClasses[state],
        sizeClasses[size],
        className
      )}
    >
      {state === 'gray' && '✕'}
      {state === 'yellow' && '△'}
      {state === 'green' && '○'}
      {state === 'gold' && '★'}
      {showLabel && <span className="ml-1">{config.label}</span>}
      {showHelper && (
        <span className="ml-1 opacity-70">({config.helper})</span>
      )}
    </span>
  );
}

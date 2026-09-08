import { cn } from '@/lib/utils';

interface HeatmapProps {
  data: { date: string; level: 0 | 1 | 2 | 3 | 4 }[];
  className?: string;
}

export function Heatmap({ data, className }: HeatmapProps) {
  // 生成最近一年的数据（简化版：显示最近12周）
  const weeks = 12;
  const daysPerWeek = 7;
  
  const getLevelClass = (level: number) => {
    switch (level) {
      case 0: return 'heatmap-level-0';
      case 1: return 'heatmap-level-1';
      case 2: return 'heatmap-level-2';
      case 3: return 'heatmap-level-3';
      case 4: return 'heatmap-level-4';
      default: return 'heatmap-level-0';
    }
  };
  
  return (
    <div className={cn('w-full overflow-x-auto', className)}>
      <div className="flex gap-1 min-w-max">
        {Array.from({ length: weeks }).map((_, weekIndex) => (
          <div key={weekIndex} className="flex flex-col gap-1">
            {Array.from({ length: daysPerWeek }).map((_, dayIndex) => {
              const dataIndex = weekIndex * daysPerWeek + dayIndex;
              const item = data[dataIndex] || { level: 0 };
              return (
                <div
                  key={dayIndex}
                  className={cn('heatmap-cell', getLevelClass(item.level))}
                  title={item.date || ''}
                />
              );
            })}
          </div>
        ))}
      </div>
      <div className="flex items-center gap-2 mt-2 text-xs text-muted-foreground">
        <span>少</span>
        <div className="flex gap-1">
          {[0, 1, 2, 3, 4].map((level) => (
            <div
              key={level}
              className={cn('heatmap-cell w-3 h-3', getLevelClass(level))}
            />
          ))}
        </div>
        <span>多</span>
      </div>
    </div>
  );
}

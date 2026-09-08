import { cn } from '@/lib/utils';
import { Button } from '@/components/ui/button';
import { FileQuestion, BookOpen, Sparkles } from 'lucide-react';

interface EmptyStateProps {
  title: string;
  description: string;
  action?: {
    label: string;
    onClick: () => void;
  };
  icon?: 'task' | 'review' | 'wrong' | 'default';
  className?: string;
}

export function EmptyState({
  title,
  description,
  action,
  icon = 'default',
  className,
}: EmptyStateProps) {
  const icons = {
    task: BookOpen,
    review: Sparkles,
    wrong: FileQuestion,
    default: FileQuestion,
  };
  
  const Icon = icons[icon];
  
  return (
    <div
      className={cn(
        'flex flex-col items-center justify-center py-12 px-4 text-center',
        className
      )}
    >
      <div className="w-16 h-16 rounded-full bg-muted flex items-center justify-center mb-4">
        <Icon className="w-8 h-8 text-muted-foreground" />
      </div>
      <h3 className="text-lg font-semibold text-foreground mb-2">{title}</h3>
      <p className="text-sm text-muted-foreground mb-4 max-w-xs">
        {description}
      </p>
      {action && (
        <Button onClick={action.onClick} variant="outline" size="sm">
          {action.label}
        </Button>
      )}
    </div>
  );
}

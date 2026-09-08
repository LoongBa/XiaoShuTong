import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { useEffect, useState } from 'react';
import { useAppStore } from '@/store/appStore';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { 
  ArrowLeft,
  Check,
  ChevronRight,
  BookOpen,
  Clock,
  Users,
  Sparkles
} from 'lucide-react';
import { cn } from '@/lib/utils';

const STEPS = ['选择内容', '配置任务', '预览发布'];

// 模拟题库
const MOCK_BANKS = [
  { id: 'bank-1', title: '唐诗三百首', count: 300, subject: '语文' },
  { id: 'bank-2', title: '宋词精选', count: 150, subject: '语文' },
  { id: 'bank-3', title: '人文地理', count: 80, subject: '地理' },
  { id: 'bank-4', title: '中国历史', count: 200, subject: '历史' },
];

export const Route = createFileRoute('/teacher/tasks/create')({
  component: CreateTaskPage,
});

function CreateTaskPage() {
  const navigate = useNavigate();
  const { isLoggedIn } = useAppStore();
  const [currentStep, setCurrentStep] = useState(0);
  const [selectedBank, setSelectedBank] = useState<string | null>(null);
  const [taskName, setTaskName] = useState('');
  const [deadline, setDeadline] = useState('');
  const [isPublishing, setIsPublishing] = useState(false);
  
  // 检查登录状态
  useEffect(() => {
    if (!isLoggedIn) {
      navigate({ to: '/auth/login' });
    }
  }, [isLoggedIn, navigate]);
  
  const handleNext = () => {
    if (currentStep < STEPS.length - 1) {
      setCurrentStep(currentStep + 1);
    }
  };
  
  const handleBack = () => {
    if (currentStep > 0) {
      setCurrentStep(currentStep - 1);
    } else {
      navigate({ to: '/teacher/dashboard' });
    }
  };
  
  const handlePublish = async () => {
    setIsPublishing(true);
    // 模拟发布
    await new Promise(resolve => setTimeout(resolve, 1500));
    setIsPublishing(false);
    navigate({ to: '/teacher/dashboard' });
  };
  
  const selectedBankData = MOCK_BANKS.find(b => b.id === selectedBank);
  
  const canProceed = () => {
    if (currentStep === 0) return selectedBank !== null;
    if (currentStep === 1) return taskName.trim() !== '' && deadline !== '';
    return true;
  };
  
  return (
    <div className="min-h-screen bg-background">
      {/* 顶部导航 */}
      <header className="sticky top-0 z-10 bg-card border-b border-border px-4 py-3">
        <div className="flex items-center gap-3">
          <button
            onClick={handleBack}
            className="p-2 -ml-2 rounded-full hover:bg-muted transition-colors"
          >
            <ArrowLeft className="w-5 h-5" />
          </button>
          <h1 className="font-semibold">布置任务</h1>
        </div>
        
        {/* 步骤指示器 */}
        <div className="mt-4 flex items-center justify-between">
          {STEPS.map((step, index) => (
            <div key={step} className="flex items-center">
              <div
                className={cn(
                  'w-8 h-8 rounded-full flex items-center justify-center text-sm font-medium transition-colors',
                  index < currentStep && 'bg-primary text-primary-foreground',
                  index === currentStep && 'bg-primary text-primary-foreground ring-2 ring-primary/30',
                  index > currentStep && 'bg-muted text-muted-foreground'
                )}
              >
                {index < currentStep ? (
                  <Check className="w-4 h-4" />
                ) : (
                  index + 1
                )}
              </div>
              <span
                className={cn(
                  'ml-2 text-xs font-medium hidden sm:block',
                  index <= currentStep ? 'text-foreground' : 'text-muted-foreground'
                )}
              >
                {step}
              </span>
              {index < STEPS.length - 1 && (
                <div
                  className={cn(
                    'w-8 h-0.5 mx-2',
                    index < currentStep ? 'bg-primary' : 'bg-muted'
                  )}
                />
              )}
            </div>
          ))}
        </div>
      </header>
      
      <div className="p-4 space-y-4">
        {/* 步骤1：选择内容 */}
        {currentStep === 0 && (
          <section>
            <h2 className="font-semibold mb-4 flex items-center gap-2">
              <BookOpen className="w-4 h-4" />
              选择背诵内容
            </h2>
            <div className="space-y-2">
              {MOCK_BANKS.map((bank) => (
                <Card
                  key={bank.id}
                  onClick={() => setSelectedBank(bank.id)}
                  className={cn(
                    'p-4 cursor-pointer transition-all',
                    selectedBank === bank.id
                      ? 'ring-2 ring-primary border-primary'
                      : 'hover:shadow-md'
                  )}
                >
                  <div className="flex items-center justify-between">
                    <div>
                      <h3 className="font-medium">{bank.title}</h3>
                      <p className="text-xs text-muted-foreground mt-1">
                        {bank.subject} · {bank.count} 个知识点
                      </p>
                    </div>
                    {selectedBank === bank.id && (
                      <div className="w-6 h-6 rounded-full bg-primary flex items-center justify-center">
                        <Check className="w-4 h-4 text-primary-foreground" />
                      </div>
                    )}
                  </div>
                </Card>
              ))}
            </div>
          </section>
        )}
        
        {/* 步骤2：配置任务 */}
        {currentStep === 1 && selectedBankData && (
          <section className="space-y-4">
            <h2 className="font-semibold flex items-center gap-2">
              <Clock className="w-4 h-4" />
              配置任务
            </h2>
            
            <Card className="p-4">
              <p className="text-sm text-muted-foreground mb-4">
                已选择：{selectedBankData.title}（{selectedBankData.count}个知识点）
              </p>
              
              <div className="space-y-4">
                <div>
                  <Label htmlFor="taskName">任务名称</Label>
                  <Input
                    id="taskName"
                    value={taskName}
                    onChange={(e) => setTaskName(e.target.value)}
                    placeholder={`背诵《${selectedBankData.title}》`}
                    className="mt-1.5"
                  />
                </div>
                
                <div>
                  <Label htmlFor="deadline">截止时间</Label>
                  <div className="flex gap-2 mt-1.5">
                    <Button
                      variant={deadline === 'tomorrow' ? 'default' : 'outline'}
                      size="sm"
                      onClick={() => setDeadline('tomorrow')}
                    >
                      明天
                    </Button>
                    <Button
                      variant={deadline === 'weekend' ? 'default' : 'outline'}
                      size="sm"
                      onClick={() => setDeadline('weekend')}
                    >
                      周末
                    </Button>
                    <Button
                      variant={deadline === 'custom' ? 'default' : 'outline'}
                      size="sm"
                      onClick={() => setDeadline('custom')}
                    >
                      自定义
                    </Button>
                  </div>
                </div>
                
                <div>
                  <Label>参与班级</Label>
                  <div className="flex items-center gap-2 mt-1.5 p-3 bg-muted rounded-lg">
                    <Users className="w-4 h-4 text-muted-foreground" />
                    <span className="text-sm">初二(3)班</span>
                    <span className="text-xs text-muted-foreground">(45人)</span>
                  </div>
                </div>
              </div>
            </Card>
          </section>
        )}
        
        {/* 步骤3：预览发布 */}
        {currentStep === 2 && selectedBankData && (
          <section className="space-y-4">
            <h2 className="font-semibold flex items-center gap-2">
              <Sparkles className="w-4 h-4" />
              预览并发布
            </h2>
            
            <Card className="p-4">
              <div className="space-y-3">
                <div className="flex justify-between">
                  <span className="text-sm text-muted-foreground">任务名称</span>
                  <span className="text-sm font-medium">
                    {taskName || `背诵《${selectedBankData.title}》`}
                  </span>
                </div>
                <div className="flex justify-between">
                  <span className="text-sm text-muted-foreground">背诵内容</span>
                  <span className="text-sm font-medium">{selectedBankData.title}</span>
                </div>
                <div className="flex justify-between">
                  <span className="text-sm text-muted-foreground">知识点数量</span>
                  <span className="text-sm font-medium">{selectedBankData.count}个</span>
                </div>
                <div className="flex justify-between">
                  <span className="text-sm text-muted-foreground">截止时间</span>
                  <span className="text-sm font-medium">
                    {deadline === 'tomorrow' ? '明天 18:00' :
                     deadline === 'weekend' ? '本周日 18:00' : '自定义时间'}
                  </span>
                </div>
                <div className="flex justify-between">
                  <span className="text-sm text-muted-foreground">参与班级</span>
                  <span className="text-sm font-medium">初二(3)班 (45人)</span>
                </div>
              </div>
            </Card>
            
            <div className="p-4 bg-amber-50 border border-amber-200 rounded-lg">
              <p className="text-sm text-amber-800">
                发布后，学生将收到任务通知。任务截止前可以随时修改截止时间。
              </p>
            </div>
          </section>
        )}
      </div>
      
      {/* 底部按钮 */}
      <div className="fixed bottom-0 left-0 right-0 bg-card border-t border-border p-4 safe-bottom">
        <div className="flex gap-3">
          {currentStep < STEPS.length - 1 ? (
            <Button
              onClick={handleNext}
              disabled={!canProceed()}
              className="flex-1 h-12"
              size="lg"
            >
              下一步
              <ChevronRight className="ml-2 h-4 w-4" />
            </Button>
          ) : (
            <Button
              onClick={handlePublish}
              disabled={isPublishing}
              className="flex-1 h-12"
              size="lg"
            >
              {isPublishing ? (
                <>
                  <Sparkles className="mr-2 h-4 w-4 animate-spin" />
                  发布中…
                </>
              ) : (
                '发布任务'
              )}
            </Button>
          )}
        </div>
      </div>
    </div>
  );
}

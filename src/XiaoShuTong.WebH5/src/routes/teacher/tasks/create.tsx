import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { useEffect, useMemo, useState } from 'react';
import { useAppStore } from '@/store/appStore';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { Tkwf } from '@tkwf/tsclient';
import { executeQuery } from '@/lib/sdk-bypass';
import type {
  Banks_ExecuteService,
  Groups_ExecuteService,
  CreateTaskResDto,
} from '@/gql/ts-client.g';
import {
  ArrowLeft,
  Check,
  ChevronRight,
  BookOpen,
  Clock,
  Users,
  Sparkles,
  Loader2,
} from 'lucide-react';
import { cn } from '@/lib/utils';

const STEPS = ['选择内容', '配置任务', '预览发布'];

// subject 中英映射（同 student/bank/self.tsx labelOf）
const SUBJECT_LABELS: Record<string, string> = {
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
const labelOf = (raw: string | null | undefined): string => {
  const r = (raw ?? '').toLowerCase();
  return SUBJECT_LABELS[r] ?? (raw ?? '综合');
};

// 截止时间快捷值 → 完整 ISO（HotChocolate DateTime 不接收纯日期，V0.6.6 教训）
function resolveDeadlineISO(shortcut: string, customDate: string): string | null {
  const at18 = (d: Date) => { d.setHours(18, 0, 0, 0); return d.toISOString(); };
  if (shortcut === 'tomorrow') {
    const d = new Date();
    d.setDate(d.getDate() + 1);
    return at18(d);
  }
  if (shortcut === 'weekend') {
    // 本周日
    const d = new Date();
    d.setDate(d.getDate() + ((7 - d.getDay()) % 7));
    return at18(d);
  }
  if (shortcut === 'custom' && customDate) {
    const d = new Date(`${customDate}T18:00:00`);
    return Number.isNaN(d.getTime()) ? null : d.toISOString();
  }
  return null;
}

// 截止时间展示文案（本地时区），与提交 ISO 一致
function deadlineDisplay(shortcut: string, customDate: string, iso: string | null): string {
  if (!iso) return '未设置';
  if (shortcut === 'tomorrow') return '明天 18:00';
  if (shortcut === 'weekend') return '本周日 18:00';
  if (shortcut === 'custom' && customDate) return `${customDate} 18:00`;
  return String(iso).slice(0, 10);
}

export const Route = createFileRoute('/teacher/tasks/create')({
  component: CreateTaskPage,
});

function CreateTaskPage() {
  const navigate = useNavigate();
  const { isLoggedIn } = useAppStore();
  const [currentStep, setCurrentStep] = useState(0);
  const [selectedBankId, setSelectedBankId] = useState<string | null>(null);
  const [taskName, setTaskName] = useState('');
  const [deadline, setDeadline] = useState('tomorrow');
  const [customDate, setCustomDate] = useState('');
  const [allowRedo, setAllowRedo] = useState(false);
  const [isPublishing, setIsPublishing] = useState(false);
  const [publishError, setPublishError] = useState('');

  // 真实数据源（listBanks_Execute / listGroups_Execute）
  const [banks, setBanks] = useState<any[]>([]);
  const [groups, setGroups] = useState<any[]>([]);
  const [selectedGroupUid, setSelectedGroupUid] = useState<string | null>(null);
  const [isLoadingBanks, setIsLoadingBanks] = useState(true);

  // 检查登录状态
  useEffect(() => {
    if (!isLoggedIn) {
      navigate({ to: '/auth/login' });
    }
  }, [isLoggedIn, navigate]);

  // 进入时拉取题库 + 群组（群组默认选第一个，同 dashboard 范式）
  useEffect(() => {
    if (!isLoggedIn) return;
    let cancelled = false;

    const load = async () => {
      setIsLoadingBanks(true);
      try {
        const [banksRes, groupsRes] = await Promise.all([
          Tkwf.User.Use<Banks_ExecuteService>().listBanks_Execute({
            request: { subject: null, purpose: null, pageIndex: 1, pageSize: 100 },
          }),
          Tkwf.User.Use<Groups_ExecuteService>().listGroups_Execute({
            request: { pageIndex: 1, pageSize: 10 },
          }),
        ]);

        if (!cancelled && banksRes.success && banksRes.items) {
          setBanks(banksRes.items);
        }
        if (!cancelled && groupsRes.success && groupsRes.items && groupsRes.items.length > 0) {
          setGroups(groupsRes.items);
          setSelectedGroupUid(groupsRes.items[0].groupUid);
        }
      } catch {
        // RPC 抛错：全局 onGlobalError 已记录，页面保留空态走查可见
      } finally {
        if (!cancelled) setIsLoadingBanks(false);
      }
    };

    void load();
    return () => { cancelled = true; };
  }, [isLoggedIn]);

  const selectedBank = banks.find((b) => b.bank?.bankId === selectedBankId);
  const selectedGroup = groups.find((g) => g.groupUid === selectedGroupUid);
  const deadlineISO = useMemo(
    () => resolveDeadlineISO(deadline, customDate),
    [deadline, customDate],
  );

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
    if (!selectedBankId || !selectedGroupUid || !deadlineISO) return;
    setIsPublishing(true);
    setPublishError('');

    try {
      // V0.7.7（G16）：SDK 前缀启发式把 create* 判 mutation 导致 400 → 走 executeQuery 强制 query（ts-client.g.ts 已标 type:'query'）
      const res = await executeQuery<CreateTaskResDto>('createTask_Execute', {
        request: {
          groupUid: selectedGroupUid,
          bankId: selectedBankId,
          title: taskName.trim() || `背诵《${selectedBank?.bank?.name ?? ''}》`,
          description: null,
          questionIds: [], // 整库布置，questionIds 空数组（V0.6.14 codegen 修复：契约 [String!]! 数组，原 JSON.stringify([]) 字符串错配）
          scenario: null,
          sessionType: null,
          allowRedo,
          deadlineAt: deadlineISO,
        },
      });

      if (res.success) {
        navigate({ to: '/teacher/dashboard' });
        return;
      }
      // 域级失败（success=false 不抛错）→ 读 errorCode
      setPublishError(res.errorCode || '发布失败，请稍后重试');
    } catch (err: any) {
      // RPC 抛错 → 读 err.code
      setPublishError(err?.code || err?.message || '发布失败，请稍后重试');
    } finally {
      setIsPublishing(false);
    }
  };

  const canProceed = () => {
    if (currentStep === 0) return selectedBankId !== null;
    if (currentStep === 1) return taskName.trim() !== '' && deadlineISO !== null;
    return true;
  };

  return (
    <div className="min-h-screen bg-background">
      {/* 顶部导航 */}
      <header className="sticky top-0 z-10 bg-card border-b border-border px-4 py-3">
        <div className="flex items-center gap-3">
          <button
            type="button"
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

            {isLoadingBanks ? (
              <div className="flex items-center justify-center py-12 text-muted-foreground">
                <Loader2 className="w-5 h-5 animate-spin mr-2" />
                题库加载中…
              </div>
            ) : banks.length === 0 ? (
              <Card className="p-6 text-center text-sm text-muted-foreground">
                暂无可用题库
              </Card>
            ) : (
              <div className="space-y-2">
                {banks.map((bank) => {
                  const bankId = bank.bank?.bankId;
                  const title = bank.bank?.name ?? '(未知题库)';
                  const subject = labelOf(bank.bank?.subject);
                  const count = bank.questionCount ?? 0;
                  return (
                    <Card
                      key={bankId}
                      onClick={() => setSelectedBankId(bankId)}
                      className={cn(
                        'p-4 cursor-pointer transition-all',
                        selectedBankId === bankId
                          ? 'ring-2 ring-primary border-primary'
                          : 'hover:shadow-md'
                      )}
                    >
                      <div className="flex items-center justify-between">
                        <div>
                          <h3 className="font-medium">{title}</h3>
                          <p className="text-xs text-muted-foreground mt-1">
                            {subject} · {count} 个知识点
                          </p>
                        </div>
                        {selectedBankId === bankId && (
                          <div className="w-6 h-6 rounded-full bg-primary flex items-center justify-center">
                            <Check className="w-4 h-4 text-primary-foreground" />
                          </div>
                        )}
                      </div>
                    </Card>
                  );
                })}
              </div>
            )}
          </section>
        )}

        {/* 步骤2：配置任务 */}
        {currentStep === 1 && selectedBank && (
          <section className="space-y-4">
            <h2 className="font-semibold flex items-center gap-2">
              <Clock className="w-4 h-4" />
              配置任务
            </h2>

            <Card className="p-4">
              <p className="text-sm text-muted-foreground mb-4">
                已选择：{selectedBank.bank?.name}（{selectedBank.questionCount ?? 0}个知识点）
              </p>

              <div className="space-y-4">
                <div>
                  <Label htmlFor="taskName">任务名称</Label>
                  <Input
                    id="taskName"
                    value={taskName}
                    onChange={(e) => setTaskName(e.target.value)}
                    placeholder={`背诵《${selectedBank.bank?.name ?? ''}》`}
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
                  {deadline === 'custom' && (
                    <Input
                      type="date"
                      value={customDate}
                      onChange={(e) => setCustomDate(e.target.value)}
                      className="mt-2"
                    />
                  )}
                </div>

                <div>
                  <Label htmlFor="allowRedo">允许重做</Label>
                  <div className="flex items-center gap-2 mt-1.5">
                    <Switch
                      id="allowRedo"
                      checked={allowRedo}
                      onCheckedChange={setAllowRedo}
                    />
                    <span className="text-xs text-muted-foreground">
                      {allowRedo ? '学生完成后可重复背诵' : '学生仅需完成一次'}
                    </span>
                  </div>
                </div>

                <div>
                  <Label>参与班级</Label>
                  {groups.length === 0 ? (
                    <div className="flex items-center gap-2 mt-1.5 p-3 bg-muted rounded-lg">
                      <Users className="w-4 h-4 text-muted-foreground" />
                      <span className="text-sm text-muted-foreground">暂无班级</span>
                    </div>
                  ) : (
                    <div className="flex flex-wrap gap-2 mt-1.5">
                      {groups.map((g) => (
                        <button
                          key={g.groupUid}
                          type="button"
                          onClick={() => setSelectedGroupUid(g.groupUid)}
                          className={cn(
                            'px-3 py-1.5 rounded-full text-sm font-medium transition-colors',
                            selectedGroupUid === g.groupUid
                              ? 'bg-primary text-primary-foreground'
                              : 'bg-muted text-muted-foreground hover:bg-muted/80'
                          )}
                        >
                          {g.name}（{g.memberCount}人）
                        </button>
                      ))}
                    </div>
                  )}
                </div>
              </div>
            </Card>
          </section>
        )}

        {/* 步骤3：预览发布 */}
        {currentStep === 2 && selectedBank && (
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
                    {taskName.trim() || `背诵《${selectedBank.bank?.name ?? ''}》`}
                  </span>
                </div>
                <div className="flex justify-between">
                  <span className="text-sm text-muted-foreground">背诵内容</span>
                  <span className="text-sm font-medium">{selectedBank.bank?.name}</span>
                </div>
                <div className="flex justify-between">
                  <span className="text-sm text-muted-foreground">知识点数量</span>
                  <span className="text-sm font-medium">{selectedBank.questionCount ?? 0}个</span>
                </div>
                <div className="flex justify-between">
                  <span className="text-sm text-muted-foreground">截止时间</span>
                  <span className="text-sm font-medium">
                    {deadlineDisplay(deadline, customDate, deadlineISO)}
                  </span>
                </div>
                <div className="flex justify-between">
                  <span className="text-sm text-muted-foreground">允许重做</span>
                  <span className="text-sm font-medium">{allowRedo ? '是' : '否'}</span>
                </div>
                <div className="flex justify-between">
                  <span className="text-sm text-muted-foreground">参与班级</span>
                  <span className="text-sm font-medium">
                    {selectedGroup ? `${selectedGroup.name}（${selectedGroup.memberCount}人）` : '未选择'}
                  </span>
                </div>
              </div>
            </Card>

            {publishError && (
              <Card className="p-3 border-destructive">
                <p className="text-sm text-destructive">{publishError}</p>
              </Card>
            )}

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
              disabled={isPublishing || !selectedGroupUid}
              className="flex-1 h-12"
              size="lg"
            >
              {isPublishing ? (
                <>
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
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

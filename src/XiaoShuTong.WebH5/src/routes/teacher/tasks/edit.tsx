import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { useEffect, useMemo, useState } from 'react';
import { useAppStore } from '@/store/appStore';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Checkbox } from '@/components/ui/checkbox';
import { Tkwf } from '@tkwf/tsclient';
import { executeQuery } from '@/lib/sdk-bypass';
import type {
  TaskDetail_ExecuteService,
  BankDetail_ExecuteService,
  UpdateTaskResDto,
  TasksDto,
} from '@/gql/ts-client.g';
import { ArrowLeft, Loader2, AlertCircle, Save } from 'lucide-react';

// 截止时间展示/提交工具（对齐 create.tsx resolveDeadlineISO 语义：HotChocolate DateTime 需完整 ISO）
function toDateInputValue(iso: string | null | undefined): string {
  if (!iso) return '';
  return String(iso).slice(0, 10);
}
function toDeadlineISO(dateInput: string): string | null {
  if (!dateInput) return null;
  const d = new Date(`${dateInput}T18:00:00`);
  return Number.isNaN(d.getTime()) ? null : d.toISOString();
}

export const Route = createFileRoute('/teacher/tasks/edit')({
  validateSearch: (search: Record<string, unknown>): { taskUid?: string } => ({
    taskUid: typeof search.taskUid === 'string' ? search.taskUid : undefined,
  }),
  component: EditTaskPage,
});

function EditTaskPage() {
  const navigate = useNavigate();
  const { isLoggedIn } = useAppStore();
  const { taskUid } = Route.useSearch();

  // 回填数据源
  const [task, setTask] = useState<TasksDto | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState('');

  // 表单字段
  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [deadlineDate, setDeadlineDate] = useState('');

  // 题集（M3：题库 Disabled——BR-26 关联 BankId 不可变更；shadcn Checkbox 多选）
  const [bankId, setBankId] = useState<string | null>(null);
  const [bankName, setBankName] = useState('');
  const [bankQuestions, setBankQuestions] = useState<string[]>([]);
  const [selectedQuestionIds, setSelectedQuestionIds] = useState<string[]>([]);
  const [isLoadingQuestions, setIsLoadingQuestions] = useState(false);

  // 保存状态
  const [isSaving, setIsSaving] = useState(false);
  const [saveError, setSaveError] = useState('');

  // 检查登录状态
  useEffect(() => {
    if (!isLoggedIn) navigate({ to: '/auth/login' });
  }, [isLoggedIn, navigate]);

  // 拉取任务详情回填
  useEffect(() => {
    if (!isLoggedIn || !taskUid) return;
    let cancelled = false;

    const load = async () => {
      setIsLoading(true);
      setLoadError('');
      try {
        const res = await Tkwf.User.Use<TaskDetail_ExecuteService>().taskDetail_Execute({
          request: { taskUid },
        });
        if (cancelled) return;
        if (!res.success || !res.task) {
          setLoadError(res.errorCode || '加载失败');
          return;
        }
        setTask(res.task);
        setTitle(res.task.title ?? '');
        setDescription(res.task.description ?? '');
        setDeadlineDate(toDateInputValue(res.task.deadlineAt));
        setBankId(res.task.bankId);
        setSelectedQuestionIds(res.task.questionIds ?? []);
      } catch (err: any) {
        if (!cancelled) setLoadError(err?.code || err?.message || '加载失败');
      } finally {
        if (!cancelled) setIsLoading(false);
      }
    };

    void load();
    return () => { cancelled = true; };
  }, [isLoggedIn, taskUid]);

  // 拉取题库题目（仅当前 BankId，Disabled 不可切换——BR-26）
  useEffect(() => {
    if (!bankId) return;
    let cancelled = false;

    const loadQuestions = async () => {
      setIsLoadingQuestions(true);
      try {
        const res = await Tkwf.User.Use<BankDetail_ExecuteService>().bankDetail_Execute({
          request: { bankId },
        });
        if (cancelled) return;
        if (res.success && res.topics) {
          // TopicNodeDto.subTopics 为 string[]（非嵌套节点）——平铺一层题目 ID 即可
          const all: string[] = [];
          for (const topic of res.topics) {
            all.push(...(topic.questionIds ?? []));
          }
          setBankName(res.bank?.name ?? '');
          setBankQuestions(all);
        }
      } catch {
        // 题目加载失败不阻塞表单（可仅保存标题/说明/截止）
      } finally {
        if (!cancelled) setIsLoadingQuestions(false);
      }
    };

    void loadQuestions();
    return () => { cancelled = true; };
  }, [bankId]);

  const deadlineISO = useMemo(() => toDeadlineISO(deadlineDate), [deadlineDate]);

  const toggleQuestion = (qid: string, checked: boolean) => {
    setSelectedQuestionIds((prev) =>
      checked ? (prev.includes(qid) ? prev : [...prev, qid]) : prev.filter((x) => x !== qid),
    );
  };

  const handleSave = async () => {
    if (!taskUid) return;
    if (!title.trim()) {
      setSaveError('请输入任务名称');
      return;
    }
    setIsSaving(true);
    setSaveError('');
    try {
      // Oracle C1 致命修正：`update` 前缀命中 SDK MUTATION_PREFIXES（sdk-bypass.ts L8）
      // → 必须走 executeQuery 强制 query（服务端 Query 根）；直调 Tkwf.User.Use 必 400
      const res = await executeQuery<UpdateTaskResDto>('updateTask_Execute', {
        request: {
          taskUid,
          title: title.trim(),
          description: description.trim() ? description : null,
          deadlineAt: deadlineISO,
          questionIds: bankId ? selectedQuestionIds : null,
        },
      });

      if (res.success) {
        // 回详情页（带时间戳防缓存刷新）
        navigate({
          to: '/teacher/tasks/detail',
          search: { taskUid, ts: Date.now() },
        });
        return;
      }
      setSaveError(res.errorCode || '保存失败，请稍后重试');
    } catch (err: any) {
      setSaveError(err?.code || err?.message || '保存失败，请稍后重试');
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <div className="min-h-screen bg-background">
      {/* 顶部导航 */}
      <header className="sticky top-0 z-10 bg-card border-b border-border px-4 py-3">
        <div className="flex items-center gap-3">
          <button
            type="button"
            onClick={() =>
              navigate({ to: '/teacher/tasks/detail', search: { taskUid } })
            }
            className="p-2 -ml-2 rounded-full hover:bg-muted transition-colors"
          >
            <ArrowLeft className="w-5 h-5" />
          </button>
          <h1 className="font-semibold">编辑任务</h1>
        </div>
      </header>

      <div className="p-4 space-y-4">
        {isLoading ? (
          <div className="flex items-center justify-center py-12 text-muted-foreground">
            <Loader2 className="w-5 h-5 animate-spin mr-2" />
            任务详情加载中…
          </div>
        ) : loadError ? (
          <Card className="p-6 text-center">
            <AlertCircle className="w-8 h-8 text-destructive mx-auto mb-2" />
            <p className="text-sm text-destructive">{loadError}</p>
          </Card>
        ) : !task ? (
          <Card className="p-6 text-center text-sm text-muted-foreground">
            任务不存在或已删除
          </Card>
        ) : (
          <>
            {/* 基本信息 */}
            <Card className="p-4 space-y-4">
              <div className="space-y-2">
                <Label htmlFor="title">任务名称</Label>
                <Input
                  id="title"
                  value={title}
                  onChange={(e) => setTitle(e.target.value)}
                  maxLength={128}
                  placeholder="请输入任务名称"
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="description">任务说明</Label>
                <Textarea
                  id="description"
                  value={description}
                  onChange={(e) => setDescription(e.target.value)}
                  rows={3}
                  placeholder="可选，补充说明"
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="deadline">截止时间</Label>
                <Input
                  id="deadline"
                  type="date"
                  value={deadlineDate}
                  onChange={(e) => setDeadlineDate(e.target.value)}
                />
                <p className="text-xs text-muted-foreground">
                  默认当天 18:00 截止；不填表示保持原截止时间
                </p>
              </div>
            </Card>

            {/* 题集（题库 Disabled——BR-26 关联题库不可变更） */}
            <Card className="p-4 space-y-3">
              <div className="space-y-2">
                <Label>题库</Label>
                <div className="text-sm text-muted-foreground border rounded-md px-3 py-2 bg-muted/40">
                  {bankName || bankId || '自由编排（无题库）'}
                </div>
                <p className="text-xs text-muted-foreground">
                  关联题库不可变更；题集变更后将重算非完成成员进度
                </p>
              </div>

              <div className="space-y-2">
                <Label>题目集</Label>
                {!bankId ? (
                  <p className="text-sm text-muted-foreground">
                    自由编排任务无题集，保存时保持原题集
                  </p>
                ) : isLoadingQuestions ? (
                  <div className="flex items-center justify-center py-6 text-muted-foreground">
                    <Loader2 className="w-4 h-4 animate-spin mr-2" />
                    题目加载中…
                  </div>
                ) : bankQuestions.length === 0 ? (
                  <p className="text-sm text-muted-foreground">该题库暂无题目</p>
                ) : (
                  <>
                    <div className="max-h-[260px] overflow-y-auto border rounded-md p-2 space-y-1">
                      {bankQuestions.map((qid) => (
                        <label
                          key={qid}
                          className="flex items-center gap-2 px-2 py-1.5 rounded hover:bg-muted cursor-pointer"
                        >
                          <Checkbox
                            checked={selectedQuestionIds.includes(qid)}
                            onCheckedChange={(checked) => toggleQuestion(qid, checked === true)}
                          />
                          <span className="text-sm flex-1">{qid}</span>
                        </label>
                      ))}
                    </div>
                    <p className="text-xs text-muted-foreground">
                      已选 {selectedQuestionIds.length} 题
                    </p>
                  </>
                )}
              </div>
            </Card>

            {saveError && (
              <Card className="p-3 border-destructive/40">
                <p className="text-sm text-destructive flex items-center gap-2">
                  <AlertCircle className="w-4 h-4" />
                  {saveError}
                </p>
              </Card>
            )}

            <Button className="w-full h-12 text-base" size="lg" onClick={handleSave} disabled={isSaving}>
              {isSaving ? (
                <>
                  <Loader2 className="w-4 h-4 animate-spin mr-2" />
                  保存中…
                </>
              ) : (
                <>
                  <Save className="w-4 h-4 mr-2" />
                  保存修改
                </>
              )}
            </Button>
          </>
        )}
      </div>
    </div>
  );
}

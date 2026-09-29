import { useState, useEffect, useRef } from 'react';
import { motion } from 'framer-motion';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { Checkbox } from '@/components/ui/checkbox';
import { RadioGroup, RadioGroupItem } from '@/components/ui/radio-group';
import { Label } from '@/components/ui/label';
import { Mic } from 'lucide-react';
import { cn } from '@/lib/utils';
import { parseQuestionContent, type ParsedQuestionContent } from '@/lib/question-content';
import { useVoiceInput } from '@/hooks/use-voice-input';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';

/**
 * QuestionCard — 按题型分发渲染（V0.5.0，对齐 UI 设计文档-v2 §3.3 + 题型注册表）
 *
 * 题型 → 交互：
 * - R1/R2（补全/逆向）：单行输入 + 语音（系统输入法主路径）
 * - R3a/R3b（段落/整篇默写）：分段输入（初始 1 段可加至多段、可删保留 1 段、每段可编辑、\n 拼接提交）+ 语音（V0.7.6：焦点跟随唤起系统输入法；Mic 按钮仅 Web Speech 兜底）
 * - O1 单选：radio-group 点选（选中即提交标识 "B"）
 * - O2 多选：checkbox 组（提交标识 "A,B,D"）
 * - O3 判断：对/错双键（swipe 手势后置，ADR-009 决策五）
 * - O5 填空：单行输入
 * - R4 知识卡片：纯展示（display_only，无提交按钮）
 * - O4 连线：后置（题库数据不足，展示 question-only）
 */
export function QuestionCard({
  type,
  content,
  onSubmit,
  submitting,
  inputRef,
}: {
  type: string | null;
  content: string;
  onSubmit: (answer: string) => void;
  submitting: boolean;
  inputRef?: React.RefObject<HTMLInputElement | null> | null;
}) {
  const parsed: ParsedQuestionContent = parseQuestionContent(content);
  const [text, setText] = useState('');
  // V0.7.6 长文分段语音：R3a/R3b 分段列表（最小 1 段）+ 当前段索引（语音兜底追加目标）
  const [segments, setSegments] = useState<string[]>(['']);
  const [currentIndex, setCurrentIndex] = useState(0);
  const [selectedSingle, setSelectedSingle] = useState<string>('');
  const [selectedMulti, setSelectedMulti] = useState<string[]>([]);
  // O4 连线：左列已选索引 → 右列索引（未选 = null）
  const [pairMap, setPairMap] = useState<Record<number, number | null>>({});
  const [selectedLeft, setSelectedLeft] = useState<number | null>(null);
  // V0.7.6：每段 textarea ref（焦点跟随语音主路径 + Web Speech 兜底聚焦）
  const segmentRefs = useRef<(HTMLTextAreaElement | null)[]>([]);
  const voice = useVoiceInput(inputRef ?? null);

  // 题型标签（对齐题型注册表 13 键；R3a/R3b 分开，不再有"段落默写"兜底误标）
  const typeLabel: Record<string, string> = {
    R1: '上句接下句',
    R2: '下句接上句',
    R3a: '段落默写',
    R3b: '整篇默写',
    R4: '知识卡片',
    O1: '单选',
    O2: '多选',
    O3: '判断',
    O4: '连线',
    O5: '填空',
  };

  const label = typeLabel[type ?? ''] ?? '默写';
  const isRText = type === 'R1' || type === 'R2' || type === 'R3a' || type === 'R3b';
  const isOSelect = type === 'O1' || type === 'O2';
  const isO3 = type === 'O3';
  const isR4 = type === 'R4';
  const showVoice = isRText || type === 'O5';

  // 题型切换时清空本地作答（V0.7.6：R3a/R3b 分段状态一并清空 + currentIndex 归零，Oracle C4）
  useEffect(() => {
    setText('');
    setSegments(['']);
    setCurrentIndex(0);
    setSelectedSingle('');
    setSelectedMulti([]);
    setPairMap({});
    setSelectedLeft(null);
  }, [type]);

  // V0.7.6 分段操作（R3a/R3b）：更新段 i / 追加段 / 删除段（保留至少 1 段，Oracle M3 + ref 数组同步）
  const updateSegment = (i: number, value: string) => {
    setSegments((prev) => prev.map((s, idx) => (idx === i ? value : s)));
  };

  const appendToSegment = (i: number, text: string) => {
    setSegments((prev) => prev.map((s, idx) => (idx === i ? s + text : s)));
  };

  const addSegment = () => setSegments((prev) => [...prev, '']);

  const removeSegment = (i: number) => {
    setSegments((prev) => {
      const next = prev.length <= 1 ? [''] : prev.filter((_, idx) => idx !== i);
      segmentRefs.current.splice(i, 1); // ref 数组索引同步（Oracle 风险补充）
      return next;
    });
  };

  const submitText = () => {
    const trimmed = text.trim();
    if (trimmed) onSubmit(trimmed);
  };

  // V0.7.6：R3a/R3b 提交 = 分段拼接（\n 换行分隔，Oracle M2；全空不提交）
  const submitSegments = () => {
    const full = segments.map((s) => s.trim()).filter(Boolean).join('\n');
    if (full) onSubmit(full);
  };

  const submitSingle = () => {
    if (selectedSingle) onSubmit(selectedSingle);
  };

  const submitMulti = () => {
    if (selectedMulti.length > 0) onSubmit(selectedMulti.join(','));
  };

  const submitJudgement = (value: string) => onSubmit(value);

  // O4 连线：点击左列项选中 → 点击右列项建立配对（替换该行已有配对）；重复点击左列取消选中
  const handleLeftClick = (leftIdx: number) => {
    setSelectedLeft((prev) => (prev === leftIdx ? null : leftIdx));
  };

  const handleRightClick = (rightIdx: number) => {
    if (selectedLeft === null) return; // 必须先选左列
    setPairMap((prev) => {
      const next = { ...prev };
      // 该左列已有配对 → 替换；否则建立新配对（同一 right 不能配多个左列）
      next[selectedLeft] = rightIdx;
      return next;
    });
    setSelectedLeft(null);
  };

  const submitPairs = () => {
    const pairs = parsed.pairs ?? [];
    const answered = Object.entries(pairMap)
      .filter(([, right]) => right !== null)
      .map(([leftIdx, rightIdx]) => ({ left: pairs[Number(leftIdx)]?.left, right: pairs[rightIdx!]?.right }))
      .filter((p) => p.left && p.right);
    if (answered.length > 0) {
      // 提交 canonical JSON（Oracle 评审闭环定案：{"pairs":{...}} 可扩展可校验）
      onSubmit(JSON.stringify({ pairs: Object.fromEntries(answered.map((p) => [p.left!, p.right!])) }));
    }
  };

  const handleMicClick = () => {
    // 主路径：唤起系统输入法语音（focus+click）；桌面兜底 Web Speech API（R1/R2/O5 单输入路径）
    const activated = voice.activate();
    if (!activated && voice.supported === true && 'startWebSpeech' in voice) {
      voice.startWebSpeech((t) => {
        setText((prev) => (prev ? `${prev}${t}` : t));
      });
    }
  };

  // V0.7.6（Oracle M1 方案 B + C1）：R3a/R3b 分段语音——焦点跟随唤起系统输入法（移动端主路径），
  // Mic 按钮仅作 Web Speech 兜底（桌面）与聚焦入口（移动端）；不调用 voice.activate()（hook 单 ref 签名不兼容）
  const handleSegmentMic = () => {
    const el = segmentRefs.current[currentIndex];
    if (el) el.focus(); // 移动端：聚焦 → 系统输入法语音跟随（主路径）
    if ('ontouchstart' in window) return; // 触摸设备主路径即聚焦，无需 Web Speech
    if (voice.supported === true && 'startWebSpeech' in voice) {
      voice.startWebSpeech((t) => appendToSegment(currentIndex, t)); // 桌面兜底：追加到当前段
    }
  };

  return (
    <div className="space-y-4">
      {/* 题型标签 */}
      <span className="text-xs text-muted-foreground px-2 py-1 bg-muted rounded-full inline-block">
        {label}
      </span>

      {/* 题干（解析后展示文本，非原始 JSON；R4 含卡片正文 answer） */}
      <p className="question-text text-foreground whitespace-pre-wrap">
        {parsed.question}
        {isR4 && parsed.answer ? `\n${parsed.answer}` : ''}
      </p>

      {/* R4 知识卡片：纯展示，无输入 */}
      {isR4 ? (
        <p className="text-xs text-muted-foreground">知识卡片 · 无作答输入</p>
      ) : (
        <div className="space-y-4">
          {/* R1/R2/R3a/R3b/O5：文本输入 + 语音 */}
          {isRText || type === 'O5' ? (
            <div className="space-y-2">
              {/* V0.7.6：R3a/R3b 分段输入（初始 1 段可加/删、每段可编辑、焦点跟随语音；Oracle M1/M2/M3 + C1-C5） */}
              {type === 'R3a' || type === 'R3b' ? (
                <>
                  {segments.map((seg, i) => (
                    <div key={i} className="relative">
                      <Textarea
                        value={seg}
                        onChange={(e) => updateSegment(i, e.target.value)}
                        onFocus={() => setCurrentIndex(i)}
                        placeholder={`第 ${i + 1} 段（可分段背诵）`}
                        rows={3}
                        className="text-base pr-12 resize-none max-h-[120px] overflow-y-auto"
                        disabled={submitting}
                        ref={(el) => {
                          segmentRefs.current[i] = el;
                        }}
                      />
                      {segments.length > 1 && (
                        <button
                          type="button"
                          onClick={() => removeSegment(i)}
                          disabled={submitting}
                          className="absolute right-3 -bottom-1 translate-y-full text-xs text-muted-foreground hover:text-destructive transition-colors"
                          aria-label="删除此段"
                        >
                          删除此段
                        </button>
                      )}
                    </div>
                  ))}
                  <div className="flex items-center gap-2">
                    <Button type="button" variant="ghost" size="sm" onClick={addSegment} disabled={submitting}>
                      ＋ 添加一段
                    </Button>
                    {showVoice && (
                      <TooltipProvider>
                        <Tooltip>
                          <TooltipTrigger asChild>
                            <button
                              type="button"
                              onClick={handleSegmentMic}
                              disabled={voice.supported === false}
                              className="p-2 text-muted-foreground hover:text-primary transition-colors disabled:opacity-40"
                              aria-label="语音输入当前段"
                            >
                              <Mic className="w-5 h-5" />
                            </button>
                          </TooltipTrigger>
                          <TooltipContent>
                            {voice.supported === false
                              ? '当前浏览器不支持语音，请键盘输入'
                              : voice.listening
                                ? '正在识别…'
                                : '语音输入当前段（点击段落聚焦后也可直接语音）'}
                          </TooltipContent>
                        </Tooltip>
                      </TooltipProvider>
                    )}
                    <span className="text-xs text-muted-foreground">
                      长文可分段背诵，提交时自动拼接
                    </span>
                  </div>
                </>
              ) : (
                <div className="relative">
                  <Input
                    ref={inputRef}
                    value={text}
                    onChange={(e) => setText(e.target.value)}
                    onKeyDown={(e) => e.key === 'Enter' && submitText()}
                    placeholder="请输入答案..."
                    className="h-14 text-base pr-12"
                    disabled={submitting}
                  />
                  {showVoice && (
                    <TooltipProvider>
                      <Tooltip>
                        <TooltipTrigger asChild>
                          <button
                            type="button"
                            onClick={handleMicClick}
                            disabled={voice.supported === false}
                            className="absolute right-3 top-1/2 -translate-y-1/2 p-2 text-muted-foreground hover:text-primary transition-colors disabled:opacity-40"
                            aria-label="语音输入"
                          >
                            <Mic className="w-5 h-5" />
                          </button>
                        </TooltipTrigger>
                        <TooltipContent>
                          {voice.supported === false
                            ? '当前浏览器不支持语音，请键盘输入'
                            : voice.listening
                              ? '正在识别…'
                              : '语音输入'}
                        </TooltipContent>
                      </Tooltip>
                    </TooltipProvider>
                  )}
                </div>
              )}
            </div>
          ) : null}

          {/* O1 单选 */}
          {type === 'O1' && parsed.options && parsed.options.length > 0 && (
            <RadioGroup value={selectedSingle} onValueChange={setSelectedSingle} className="space-y-2">
              {parsed.options.map((opt, i) => (
                <div key={i} className="flex items-center gap-3 p-3 rounded-lg border border-border">
                  <RadioGroupItem value={String.fromCharCode(65 + i)} id={`o1-${i}`} />
                  <Label htmlFor={`o1-${i}`} className="flex-1 cursor-pointer text-sm">
                    {String.fromCharCode(65 + i)}. {opt}
                  </Label>
                </div>
              ))}
            </RadioGroup>
          )}

          {/* O2 多选 */}
          {type === 'O2' && parsed.options && parsed.options.length > 0 && (
            <div className="space-y-2">
              {parsed.options.map((opt, i) => {
                const key = String.fromCharCode(65 + i);
                return (
                  <label key={i} className="flex items-center gap-3 p-3 rounded-lg border border-border cursor-pointer">
                    <Checkbox
                      checked={selectedMulti.includes(key)}
                      onCheckedChange={(checked) => {
                        setSelectedMulti((prev) =>
                          checked ? [...prev, key] : prev.filter((k) => k !== key),
                        );
                      }}
                    />
                    <span className="flex-1 text-sm">
                      {key}. {opt}
                    </span>
                  </label>
                );
              })}
            </div>
          )}

          {/* O3 判断：swipe 对/错（framer-motion：左滑=对 / 右滑=错；双键保留为降级） */}
          {type === 'O3' && (
            <div className="space-y-3">
              <motion.div
                drag="x"
                dragConstraints={{ left: 0, right: 0 }}
                dragElastic={0.3}
                onDragEnd={(_, info) => {
                  if (info.offset.x < -60) submitJudgement('对'); // 左滑 = 对
                  else if (info.offset.x > 60) submitJudgement('错'); // 右滑 = 错
                }}
                className="flex items-center justify-center py-6 rounded-xl border-2 border-dashed border-muted-foreground/30 cursor-grab active:cursor-grabbing select-none"
              >
                <span className="text-sm text-muted-foreground">左右滑动判断 · 左=对 右=错</span>
              </motion.div>
              <div className="flex gap-3">
                <Button variant={selectedSingle === '对' ? 'default' : 'outline'} className="flex-1 h-11 text-sm" onClick={() => submitJudgement('对')}>
                  对
                </Button>
                <Button variant={selectedSingle === '错' ? 'default' : 'outline'} className="flex-1 h-11 text-sm" onClick={() => submitJudgement('错')}>
                  错
                </Button>
              </div>
            </div>
          )}
        </div>
      )}

      {/* 提交按钮（R4 无输入 / O4 自带提交连线，均不渲染通用按钮） */}
      {!isR4 && type !== 'O4' && (
        <Button
          onClick={() => {
            if (isOSelect) {
              if (type === 'O1') submitSingle();
              else submitMulti();
            } else if (type === 'R3a' || type === 'R3b') {
              submitSegments(); // V0.7.6：分段拼接提交
            } else if (isRText || type === 'O5') {
              submitText();
            }
            // O3 已直接提交
          }}
          disabled={
            submitting ||
            (type === 'O1'
              ? !selectedSingle
              : type === 'O2'
                ? selectedMulti.length === 0
                : type === 'R3a' || type === 'R3b'
                  ? !segments.some((s) => s.trim()) // V0.7.6 C5：全段空禁用
                  : !text.trim())
          }
          className={cn('w-full h-12 text-base', (type === 'R3a' || type === 'R3b') && 'mt-2')}
          size="lg"
        >
          {submitting ? '判题中…' : '提交'}
        </Button>
      )}
    </div>
  );
}

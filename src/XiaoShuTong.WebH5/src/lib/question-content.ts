/**
 * parseQuestionContent — 题目展示内容解析（V0.5.0，对齐 GetNextQuestionResDto.content 形态）
 *
 * 后端 `StripAnswerFromContent`（BR-19 防爬）下发**白名单 JSON 字符串**（如 {"question":"…","options":[…]}），
 * 按题型保留展示字段、剔除 answer/keywords/correct_option。
 *
 * 兼容双形态：
 * - JSON 对象字符串（真实后端契约形态）→ 结构化解析
 * - 纯字符串（存量 mock / 历史数据兜底）→ 原样透传为 question 文本
 */

/** 解析后的题目展示内容 */
export interface ParsedQuestionContent {
  /** 题干文本（JSON 形态的 question 字段或纯字符串原文） */
  question: string;
  /** O 系列选项（O1/O2/O3 保留下发，correct_option 已剔除） */
  options?: string[];
  /** 卡片正文（R4 展示载荷特例） */
  answer?: string;
  /** 关联知识卡片 ID（R1~R3b 保留） */
  cardId?: string;
}

export function parseQuestionContent(content: string | null | undefined): ParsedQuestionContent {
  if (!content) return { question: '' };
  const trimmed = content.trim();
  if (!trimmed.startsWith('{')) return { question: trimmed }; // 纯字符串兜底

  try {
    const obj = JSON.parse(trimmed) as Record<string, unknown>;
    return {
      question: typeof obj.question === 'string' ? obj.question : trimmed,
      options: Array.isArray(obj.options)
        ? (obj.options as unknown[]).map((o) => (typeof o === 'string' ? o : JSON.stringify(o)))
        : undefined,
      answer: typeof obj.answer === 'string' ? obj.answer : undefined,
      cardId: typeof obj.cardId === 'string' ? obj.cardId : undefined,
    };
  } catch {
    return { question: trimmed }; // 非法 JSON → 原样兜底
  }
}

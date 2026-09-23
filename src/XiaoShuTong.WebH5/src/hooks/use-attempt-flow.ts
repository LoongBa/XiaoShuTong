/**
 * useAttemptFlow — 作答提交流 hook（判题契约扩展 步骤 5）
 *
 * 包一层 `Tkwf.User.Use<SubmitAttempt_ExecuteService>().submitAttempt_Execute(...)`，
 * 按方案 §4.2 决策表 + §4.3 前端决策代码，把响应降维为 UI 可直接消费的动作描述符：
 * **showAnswer（优先，达上限兜底防引导死循环）→ needsGuidance（引导）→ 其余视为 celebrate（庆祝）**。
 * 同时把原始 `SubmitAttemptResDto` 一并透出，调用方可读取 `postState`（记忆状态徽章）、
 * `isDegraded`（LLM 降级标记，UI 可选提示「判题可能不精确」）、`preState`。
 *
 * 用法（决策权威：docs/草稿/判题契约扩展-needsGuidance-attemptCount-方案.md §4.3）：
 *
 * ```tsx
 * const { submitAttempt, isSubmitting, error } = useAttemptFlow();
 *
 * const { action, response } = await submitAttempt({
 *   sessionUid,
 *   questionId,
 *   userAnswer,
 *   hintLevel: 'None',
 *   timeCostMs: 1200,
 * });
 *
 * switch (action.type) {
 *   case 'answer-sheet':   // showAnswer=true → 展示答案要点后必进下一题
 *     showAnswerSheet(action.missingKeywords, action.nextReviewAt);
 *     break;
 *   case 'guidance':       // needsGuidance=true → 停当前题，提供再试一次 / 求助升级
 *     showGuidance(action.hint, action.attemptCount, action.maxAttempts);
 *     break;
 *   case 'celebrate':      // correct → 庆祝 + 状态徽章 preState→postState → 下一题
 *     celebrate(action.result, response?.postState, action.matchedKeywords);
 *     break;
 *   case 'error':          // 域级失败（success=false）或 RPC 抛错 → 提示后停留当前题
 *     showError(action.code, action.message);
 *     break;
 * }
 * ```
 *
 * 注意：
 * - `needsGuidance` / `showAnswer` 由**服务端**基于 result + attemptCount + hintLevel 计算
 *   （§4.2 关键规则），本 hook 不做任何再推导、不重新判断对错。
 * - 会话过期 / 全局错误已由 main.tsx `Tkwf.configure` 的 onUnauthorized / onGlobalError 统一处理，
 *   本 hook 不重复实现跳转，仅把 `DomainClientError.code` 透出到 error 状态。
 * - `sessionUid` 由调用方传入（createStudySession 接线为后续项，不在本 hook 内创建会话）。
 * - 本 hook 只产动作描述符，不做 UI 渲染 / 跳转 / 定时器（delay/nextQuestion 归调用方）。
 */
import { useCallback, useState } from 'react';
import { DomainClientError, Tkwf } from '@tkwf/tsclient';
import type { SubmitAttempt_ExecuteService, SubmitAttemptResDto } from '@/gql/ts-client.g';

/** 提交判题的入参（sessionUid 由调用方提供，hook 不创建会话） */
export interface SubmitAttemptParams {
  /** 学习会话 UID（由 createStudySession 产出，接线为后续项） */
  sessionUid: string;
  /** 题目 UID */
  questionId: string;
  /** 学生作答文本 */
  userAnswer: string;
  /** 求助/提示等级（默认 'None'） */
  hintLevel?: 'None' | 'Partial' | 'Full';
  /** 作答耗时（毫秒，可空） */
  timeCostMs?: number | null;
}

/**
 * 提交判题后的动作描述符（决策表 §4.2 + §4.3 三分支；两条错误路径共用 `error` 判别值）。
 *
 * - `answer-sheet`：showAnswer=true（达上限兜底 / hintLevel=Full 已看答案）→ UI 展示答案后必进下一题
 * - `guidance`：needsGuidance=true → UI 停当前题，提供「再试一次 / 求助升级」
 * - `celebrate`：correct → UI 庆祝 + 状态徽章 preState→postState → 下一题
 * - `error`：域级失败（res.success=false，code=errorCode）或 RPC 抛错（code/message）
 */
export type AttemptFlowAction =
  | { type: 'answer-sheet'; result: string; missingKeywords: string[]; nextReviewAt: string; attemptCount: number }
  | { type: 'guidance'; result: string; hint: string; attemptCount: number; maxAttempts: number }
  | { type: 'celebrate'; result: string; postState: string; matchedKeywords: string[]; nextReviewAt: string }
  | { type: 'error'; code?: string | null; message?: string };

/** 提交结果：动作描述符 + 原始响应（调用方可读 postState / isDegraded / preState） */
export interface AttemptFlowState {
  action: AttemptFlowAction;
  /** 原始 SubmitAttemptResDto；仅 RPC 抛错时为 null */
  response: SubmitAttemptResDto | null;
}

/** useAttemptFlow 返回值 */
export interface UseAttemptFlowResult {
  /** 提交作答并返回决策动作描述符（isSubmitting 置 true/false、error 置 null/值） */
  submitAttempt: (params: SubmitAttemptParams) => Promise<AttemptFlowState>;
  /** 提交中（用于防重入、按钮 loading） */
  isSubmitting: boolean;
  /** 错误状态：域级失败为 errorCode，RPC 抛错为 err.code / 通用 message；提交开始时清空 */
  error: string | null;
}

export function useAttemptFlow(): UseAttemptFlowResult {
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const submitAttempt = useCallback(
    async (params: SubmitAttemptParams): Promise<AttemptFlowState> => {
      setError(null);
      setIsSubmitting(true);
      try {
        const res = await Tkwf.User.Use<SubmitAttempt_ExecuteService>().submitAttempt_Execute({
          request: {
            sessionUid: params.sessionUid,
            questionId: params.questionId,
            userAnswer: params.userAnswer,
            hintLevel: params.hintLevel ?? 'None',
            timeCostMs: params.timeCostMs ?? null,
          },
        });

        // 域级失败：不抛错（业务错误由 errorCode 表达），产 error action
        if (res.success === false) {
          const code = res.errorCode ?? null;
          setError(code ?? '提交失败，请稍后重试');
          return { action: { type: 'error', code }, response: res };
        }

        // §4.3 决策顺序：showAnswer 优先（达上限兜底，展示答案后必进下一题，防引导死循环）→ needsGuidance → celebrate
        let action: AttemptFlowAction;
        if (res.showAnswer) {
          action = {
            type: 'answer-sheet',
            result: res.result,
            missingKeywords: res.missingKeywords,
            nextReviewAt: res.nextReviewAt,
            attemptCount: res.attemptCount,
          };
        } else if (res.needsGuidance) {
          action = {
            type: 'guidance',
            result: res.result,
            hint: res.hint,
            attemptCount: res.attemptCount,
            maxAttempts: res.maxAttempts,
          };
        } else {
          action = {
            type: 'celebrate',
            result: res.result,
            postState: res.postState,
            matchedKeywords: res.matchedKeywords,
            nextReviewAt: res.nextReviewAt,
          };
        }
        return { action, response: res };
      } catch (err) {
        // RPC 抛错：DomainClientError 捕获 code，其余取通用 message；
        // 会话过期/全局错误已由 main.tsx onUnauthorized / onGlobalError 兜底，这里不重复跳转
        const thrownCode = err instanceof DomainClientError ? err.code : null;
        const message = err instanceof Error ? err.message : '网络异常，请稍后重试';
        setError(thrownCode ?? message);
        return { action: { type: 'error', code: thrownCode, message }, response: null };
      } finally {
        setIsSubmitting(false);
      }
    },
    []
  );

  return { submitAttempt, isSubmitting, error };
}

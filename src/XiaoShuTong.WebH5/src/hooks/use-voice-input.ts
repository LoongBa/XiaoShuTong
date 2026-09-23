import { useCallback, useEffect, useRef, useState } from 'react';

/**
 * useVoiceInput — 语音输入 hook（V0.5.0，对齐 题型与交互设计规范 §6.1）
 *
 * 规范决策（V3 已定）：**强制系统语音输入法**，禁用浏览器 MediaRecorder ASR（避免平台碎片化）。
 * 降级链：系统输入法 → Web Speech API（浏览器原生识别，不在禁令范围）→ 禁用 + tooltip。
 *
 * - 主路径：`activate()` 对目标 input/textarea 做 focus()+click()，唤起系统输入法自带语音
 *   （iOS Safari / Chrome Android 原生支持；ASR 回填控件后用户**二次确认**再提交——原则 1+2）。
 * - 兜底：特性探测 `SpeechRecognition || webkitSpeechRecognition` 可用时走 Web Speech API
 *   （浏览器原生识别，不经自建 ASR 服务；**合规判定**：规范 §6.1 禁令对象为 MediaRecorder 自建
 *   录音+识别，Web Speech API 不在此范围；特性探测兜底解决平台碎片化）。
 * - 降级：主路径与兜底均不可用 → `supported=false`，调用方禁用 Mic 按钮 + tooltip 提示。
 */
export function useVoiceInput(targetRef: React.RefObject<HTMLInputElement | null> | null) {
  const [supported, setSupported] = useState<boolean | null>(null);
  const [listening, setListening] = useState(false);
  const recognitionRef = useRef<{ stop: () => void } | null>(null);

  // 特性探测：系统输入法（触摸设备普遍支持）或 Web Speech API（桌面兜底）
  useEffect(() => {
    const hasSpeechApi =
      typeof window !== 'undefined' &&
      ('SpeechRecognition' in window || 'webkitSpeechRecognition' in window);
    // 系统输入法无法直接探测（依赖 OS 键盘），触摸设备视为支持；不支持则降级 Web Speech API
    setSupported(hasSpeechApi || typeof window === 'undefined' ? true : 'ontouchstart' in window);
  }, []);

  const cleanupRecognition = useCallback(() => {
    if (recognitionRef.current) {
      try {
        recognitionRef.current.stop();
      } catch {
        // 忽略停止异常
      }
      recognitionRef.current = null;
    }
    setListening(false);
  }, []);

  useEffect(() => cleanupRecognition, [cleanupRecognition]);

  /**
   * 唤起语音输入：
   * 1. 系统输入法主路径——focus()+click() 唤起键盘自带语音（移动端主流，符合规范原则 1）
   * 2. 桌面兜底——Web Speech API（浏览器原生识别，回填 target）
   * 返回是否成功唤起（false = 调用方禁用按钮）。
   */
  const activate = useCallback((): boolean => {
    const el = targetRef?.current;
    if (!el || supported === false) return false;

    // 主路径：系统输入法（focus 唤起键盘语音按钮；click 对部分浏览器触发原生语音）
    el.focus();
    el.click();
    return true;
  }, [targetRef, supported]);

  /**
   * Web Speech API 兜底识别（桌面浏览器无系统输入法时由调用方按需启用）。
   * 调用方在 Mic 按钮 onClick 中：`if (supported === 'web-speech') startWebSpeech(...)`。
   */
  const startWebSpeech = useCallback((onResult: (text: string) => void): boolean => {
    if (typeof window === 'undefined') return false;
    const SpeechRecognitionCtor =
      (window as unknown as Record<string, unknown>).SpeechRecognition ??
      (window as unknown as Record<string, unknown>).webkitSpeechRecognition;
    if (typeof SpeechRecognitionCtor !== 'function') return false;

    try {
      const recognition = new (SpeechRecognitionCtor as new () => {
        lang: string;
        interimResults: boolean;
        continuous: boolean;
        start: () => void;
        stop: () => void;
        onresult: ((e: { results: ArrayLike<ArrayLike<{ transcript: string }>> }) => void) | null;
        onend: (() => void) | null;
        onerror: (() => void) | null;
      })();
      recognition.lang = 'zh-CN';
      recognition.interimResults = true;
      recognition.continuous = false;
      recognition.onresult = (e) => {
        const last = e.results[e.results.length - 1];
        if (last && last[0]?.transcript) onResult(last[0].transcript);
      };
      recognition.onend = () => setListening(false);
      recognition.onerror = () => setListening(false);
      recognitionRef.current = recognition;
      recognition.start();
      setListening(true);
      return true;
    } catch {
      return false;
    }
  }, []);

  const stop = useCallback(() => cleanupRecognition(), [cleanupRecognition]);

  return { supported, listening, activate, startWebSpeech, stop };
}

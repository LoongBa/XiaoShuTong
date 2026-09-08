// ── 主题管理：深色 | 浅色 | 随系统 ──
// 机制：styles.css 用 @custom-variant dark (&:is(.dark *))，故在 <html> 上增删 .dark class
// 持久化：localStorage["beishu-theme-mode"]（light | dark | system），默认 system
// 防闪烁：main.tsx 最早执行 initTheme()（HTML 渲染前应用 class）

export type ThemeMode = "light" | "dark" | "system";

const STORAGE_KEY = "beishu-theme-mode";

/** 读取持久化主题模式，非法值回退 system */
export function getStoredTheme(): ThemeMode {
  try {
    const v = localStorage.getItem(STORAGE_KEY);
    if (v === "light" || v === "dark" || v === "system") return v;
  } catch { /* SSR/隐私模式安全兜底 */ }
  return "system";
}

/** 系统是否偏好深色 */
export function systemPrefersDark(): boolean {
  return typeof window !== "undefined"
    && window.matchMedia("(prefers-color-scheme: dark)").matches;
}

/** 根据模式计算是否深色 */
export function resolveDark(mode: ThemeMode): boolean {
  return mode === "dark" || (mode === "system" && systemPrefersDark());
}

/** 应用主题到 <html>（class + color-scheme） */
export function applyTheme(mode: ThemeMode): void {
  const dark = resolveDark(mode);
  const root = document.documentElement;
  root.classList.toggle("dark", dark);
  root.style.colorScheme = dark ? "dark" : "light";
}

/** 设置主题并持久化 */
export function setTheme(mode: ThemeMode): void {
  try {
    localStorage.setItem(STORAGE_KEY, mode);
  } catch { /* ignore */ }
  applyTheme(mode);
}

/** App 启动时调用一次（main.tsx）：应用持久化主题 + 监听系统变化 */
export function initTheme(): void {
  applyTheme(getStoredTheme());
  window.matchMedia("(prefers-color-scheme: dark)").addEventListener("change", (e) => {
    // 仅"随系统"模式下跟随系统切换
    if (getStoredTheme() === "system") {
      const root = document.documentElement;
      root.classList.toggle("dark", e.matches);
      root.style.colorScheme = e.matches ? "dark" : "light";
    }
  });
}

/** 当前生效（含系统推导）是否深色——供图标/状态显示用 */
export function isDarkNow(): boolean {
  return document.documentElement.classList.contains("dark");
}
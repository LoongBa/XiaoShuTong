// ── 应用公共配置：标题/版本等全局常量（唯一来源） ──
// 页面标题、index.html <title>、版本号等统一引用此处，保证一致性。
// 修改后：vite 构建自动注入 <title>，页面直接 import 引用。

export const APP_NAME = "小书童-背书伴侣";
export const APP_VERSION = "1.0.0";
export const APP_TITLE = `${APP_NAME} v${APP_VERSION}`;
export const PRIVACY_TITLE = `${APP_NAME}隐私政策`;
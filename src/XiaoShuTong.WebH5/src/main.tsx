// 应用入口：样式在 ./styles.css（Tailwind v4 + design token），路由见 ./router.tsx
import React from "react";
import ReactDOM from "react-dom/client";
import { RouterProvider } from "@tanstack/react-router";
import { getRouter } from "./router";
import { initRevealEngine } from "./lib/reveal-engine";
import { Tkwf } from "@tkwf/tsclient";
import { initTheme } from "./lib/theme";
import "./styles.css";

// 主题初始化（最早执行，防闪烁）：应用持久化主题 + 监听系统变化
initTheme();

// 全局滚动渐入引擎：业务元素只需加 class="reveal"（详见 lib/reveal-engine.ts），勿删
initRevealEngine();

// 初始化 tkwf-tsclient（V1.0.4 门面工厂）
Tkwf.configure("default", {
  endpoint: "/graphql",
  storage: localStorage,
  retry: { maxAttempts: 3, retryOn: ["NETWORK_ERROR", "SERVER_ERROR"] },
  onUnauthorized: () => {
    // 会话过期 → 清除本地状态并跳登录
    // 已在登录页则不重复跳转（防死循环）
    const isLoginPage = window.location.pathname.startsWith("/auth/");
    if (!isLoginPage) {
      localStorage.removeItem("beishu-app-storage");
      window.location.href = "/auth/login";
    }
  },
  onGlobalError: (err) => {
    console.error(`[GlobalError] ${err.code}: ${err.message}`);
  },
});

const router = getRouter();

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <RouterProvider router={router} />
  </React.StrictMode>
);

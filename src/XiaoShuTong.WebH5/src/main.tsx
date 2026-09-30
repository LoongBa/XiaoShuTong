// 应用入口：样式在 ./styles.css（Tailwind v4 + design token），路由见 ./router.tsx
import React from "react";
import ReactDOM from "react-dom/client";
import { RouterProvider } from "@tanstack/react-router";
import { getRouter } from "./router";
import { initRevealEngine } from "./lib/reveal-engine";
import { Tkwf } from "@tkwf/tsclient";
import { operationSelection, operationVariableTypes } from "./gql/ts-client.g";
import { initTheme } from "./lib/theme";
import "./styles.css";

// 主题初始化（最早执行，防闪烁）：应用持久化主题 + 监听系统变化
initTheme();

// 全局滚动渐入引擎：业务元素只需加 class="reveal"（详见 lib/reveal-engine.ts），勿删
initRevealEngine();

// 初始化 tkwf-tsclient（V1.0.4 门面工厂）
// selectionMap：注册 codegen 产物的 operationSelection（ts-client.g.ts）——Use<T>() 代理
// 据此为对象返回值附加 GraphQL 子字段选择（HotChocolate 合规必需，缺失会 400
// "must have a selection of subfields"）。调代码前先运行 npm run gen-ts-client 保持同步。
// variableTypesMap（V1.0.10）：注册 codegen 产物的 operationVariableTypes——Use<T>() 代理
// 据此为复杂 DTO 入参声明 GraphQL 变量类型（缺失时 inferGraphQLType 退化为 JSON → 400
// "The variable ... is not compatible"）。
Tkwf.configure("default", {
  endpoint: "/graphql",
  storage: localStorage,
  selectionMap: operationSelection,
  variableTypesMap: operationVariableTypes,
  retry: { maxAttempts: 3, retryOn: ["NETWORK_ERROR", "SERVER_ERROR"] },
  onUnauthorized: () => {
    // 会话过期 → 先清 tsclient session（不清则过期 sessionKey 残留 localStorage，
    // 跳登录后所有后续请求仍带过期 session → 服务端持续 Guest 拦截 warn，
    // 直到重新登录覆盖为止——V0.7.7 走查实证"多次触发匿名拦截"的根因）。
    // logout() best-effort：过期时 logout mutation 可能也 401，被内部 catch 忽略，本地状态必清。
    void Tkwf.User.logout().catch(() => {});
    // 再清应用状态并跳登录（已在登录页则不重复跳转，防死循环）
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

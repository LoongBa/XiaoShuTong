import { createFileRoute, Navigate } from "@tanstack/react-router";

export const Route = createFileRoute("/")({
  component: Index,
});

function Index() {
  // 首页重定向到登录页
  return <Navigate to="/auth/login" />;
}

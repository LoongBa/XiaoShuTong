// 根布局：Provider 放这里；页面路由在 src/routes/ 下单独建文件，勿堆进 index.tsx
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import {
  Outlet,
  Navigate,
  redirect,
  createRootRouteWithContext,
  useRouterState,
} from '@tanstack/react-router';
import { useAppStore } from '@/store/appStore';

function NotFoundComponent() {
  const pathname = useRouterState({ select: (s) => s.location.pathname });
  if (pathname === '/') return null;
  return <Navigate to="/" replace />;
}

function ErrorComponent({ error }: { error: unknown; reset: () => void }) {
  console.error(error);
  const pathname = useRouterState({ select: (s) => s.location.pathname });
  // 已在首页仍报错时不再 redirect，避免 / → / 死循环
  if (pathname === '/') return null;
  return <Navigate to="/" replace />;
}

export const Route = createRootRouteWithContext<{ queryClient: QueryClient }>()({
  // V0.6.17（Oracle M1）：root beforeLoad 路由原生 role 门禁——路由解析前同步拦截，无闪跳/无限次
  beforeLoad: ({ location }) => {
    const { currentUser, isLoggedIn } = useAppStore.getState();
    const path = location.pathname;
    // 未登录 → 非 /auth/* 去 login（根路径 / 一并跳转，Oracle 补登）
    if (!isLoggedIn) {
      if (!path.startsWith('/auth/')) throw redirect({ to: '/auth/login' });
      return;
    }
    // role 门禁：按路径前缀校验，不匹配 → 对应端首页（login 分跳 homeByRole 同款映射）
    const role = currentUser?.role;
    if (path.startsWith('/teacher') && role !== 'teacher')
      throw redirect({ to: role === 'parent' ? '/parent/dashboard' : '/student/home' });
    if (path.startsWith('/parent') && role !== 'parent')
      throw redirect({ to: role === 'teacher' ? '/teacher/dashboard' : '/student/home' });
    if (path.startsWith('/student') && role !== 'student')
      throw redirect({ to: role === 'teacher' ? '/teacher/dashboard' : '/parent/dashboard' });
  },
  component: RootComponent,
  notFoundComponent: NotFoundComponent,
  errorComponent: ErrorComponent,
});

function RootComponent() {
  const { queryClient } = Route.useRouteContext();

  return (
    <QueryClientProvider client={queryClient}>
      <Outlet />
    </QueryClientProvider>
  );
}

import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { useState, useEffect } from 'react';
import { useAppStore } from '@/store/appStore';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { MessageSquare, Loader2 } from 'lucide-react';
import { Tkwf } from '@tkwf/tsclient';
import { APP_NAME } from '@/config/app';
import type { User, UserRole } from '@/types';
import { cn } from '@/lib/utils';

// ── 联调账号（V0.6.11：三端走查解锁；生产走真实微信授权由后端判身份，此区块废弃移除）──
interface DevAccount {
  userName: string;
  label: string;
}
const DEV_ACCOUNTS: DevAccount[] = [
  { userName: 'xiaoming', label: '学生 · 小明' },
  { userName: 'owner01', label: '群主 · 群主老师' },
  { userName: 'parent01', label: '家长 · 家长' },
];

// role 映射（M1：后端业务身份 → 前端路由语义的永久适配层；生产 extensions 透出后输入源改 extensions.role，映射逻辑保留）
// 对齐后端白名单 Roles（XiaoShuTongUserHelper.cs:46-76）：xiaoming→student / owner01→owner / parent01→parent
// owner→teacher：前端 UserRole 无 owner，owner01="群主老师" 语义即 teacher 端（路由 /teacher/dashboard）
function resolveRole(userName: string): UserRole {
  switch (userName) {
    case 'owner01': return 'teacher';
    case 'parent01': return 'parent';
    default: return 'student';
  }
}

export const Route = createFileRoute('/auth/login')({
  component: LoginPage,
});

function LoginPage() {
  const navigate = useNavigate();
  const { isLoggedIn, hasAgreedPrivacy, login } = useAppStore();
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState('');
  const [devUser, setDevUser] = useState<string>('xiaoming'); // 联调默认学生（走查工具，生产废弃）

  // 已登录分跳（V0.6.11：role 由 resolveRole 派生后此分支生效；V0.6.8 P3 解除）
  // 按 role 分跳：student→/student/home、teacher→/teacher/dashboard、parent→/parent/dashboard
  const homeByRole = (role: UserRole | undefined): string =>
    role === 'teacher' ? '/teacher/dashboard' : role === 'parent' ? '/parent/dashboard' : '/student/home';

  // 如果已登录，跳转到对应首页（role 分跳）
  useEffect(() => {
    if (isLoggedIn) {
      const user = useAppStore.getState().currentUser;
      navigate({ to: homeByRole(user?.role) });
    }
  }, [isLoggedIn, navigate]);

  const handleWechatLogin = async () => {
    setIsLoading(true);
    setError('');

    try {
      // 一键登录：不预验证（允许匿名，获取微信 ID 后走"登录验证"分配 session）
      // 使用 SDK 内置 loginByContext —— 成功后自动 persist session（IsAuthenticated=true）
      // V0.6.11：userName 用当前选中的联调账号（真实 WebApi 按 userName 白名单返回身份）
      const payload = await Tkwf.Guest.loginByContext(
        devUser,                     // userName（联调：选中的账号；生产：微信绑定身份）
        'mock-credential',           // credential（Mock：微信授权码）
        {
          loginFrom: 'MOBILE_WEB',
          authType: 'WE_CHAT_APPLET',
          authInfo: 'mock-wechat-openid',
          deviceId: 'web-001',
        }
      );

      if (payload.success) {
        // session 已由 SDK 持久化到 localStorage（Tkwf.User 后续可用）

        // 创建本地用户对象（M2：id mock 占位，注释标注非对齐后端 userId；role 由 userName 映射）
        const role = resolveRole(payload.userName ?? devUser);
        const mockUser: User = {
          id: role === 'teacher' ? 'owner-1' : role === 'parent' ? 'parent-1' : 'student-1', // mock 占位，非后端 userId(10001/2/3)
          name: payload.displayName || DEV_ACCOUNTS.find(a => a.userName === (payload.userName ?? devUser))?.label.split(' · ')[1] || '用户',
          avatar: '',
          role,
          streakDays: 0,
          classId: ''
        };

        // 登录（zustand 状态）
        login(mockUser);

        setIsLoading(false);

        // 检查是否需要同意隐私协议（同意后按 role 分跳，V0.6.11 C3）
        if (!hasAgreedPrivacy) {
          navigate({ to: '/auth/privacy' });
        } else {
          navigate({ to: homeByRole(role) });
        }
      } else {
        setError('登录失败');
        setIsLoading(false);
      }
    } catch (err: any) {
      setError(err.message || '登录失败，请重试');
      setIsLoading(false);
    }
  };

  return (
    <div className="min-h-screen bg-gradient-to-b from-primary/5 to-background flex flex-col items-center justify-center px-6 py-12">
      {/* Logo区域 */}
      <div className="flex-1 flex flex-col items-center justify-center w-full max-w-sm">
        <div className="w-24 h-24 rounded-3xl bg-primary/10 flex items-center justify-center mb-6">
          <span className="text-5xl">📚</span>
        </div>

        <h1 className="text-2xl font-bold text-foreground mb-2">
          {APP_NAME}
        </h1>

        <p className="text-sm text-muted-foreground text-center mb-8">
          群主布置的背书任务，在这里完成
        </p>

        {/* 登录卡片 */}
        <Card className="w-full p-6 shadow-lg border-0 bg-card/80 backdrop-blur">
          <div className="space-y-4">
            <Button
              onClick={handleWechatLogin}
              disabled={isLoading}
              className="w-full h-12 text-base font-medium"
              size="lg"
            >
              {isLoading ? (
                <>
                  <Loader2 className="mr-2 h-5 w-5 animate-spin" />
                  授权中…
                </>
              ) : (
                <>
                  <MessageSquare className="mr-2 h-5 w-5" />
                  微信一键登录
                </>
              )}
            </Button>

            {/* 联调账号切换（V0.6.11：三端走查入口；生产真实微信授权移除） */}
            <div className="flex gap-2 justify-center">
              {DEV_ACCOUNTS.map((acc) => (
                <button
                  key={acc.userName}
                  type="button"
                  onClick={() => { setDevUser(acc.userName); setError(''); }}
                  className={cn(
                    'px-3 py-1.5 rounded-full text-xs font-medium transition-colors',
                    devUser === acc.userName
                      ? 'bg-primary text-primary-foreground'
                      : 'bg-muted text-muted-foreground hover:bg-muted/80'
                  )}
                >
                  {acc.label}
                </button>
              ))}
            </div>

            {error && (
              <p className="text-sm text-destructive text-center">
                {error}
              </p>
            )}
          </div>
        </Card>
      </div>

      {/* 底部协议 */}
      <div className="py-6 text-center">
        <p className="text-xs text-muted-foreground">
          登录即表示您同意
          <button
            onClick={() => navigate({ to: '/auth/privacy' })}
            className="text-primary hover:underline mx-1"
          >
            隐私协议
          </button>
          和
          <button className="text-primary hover:underline mx-1">
            用户协议
          </button>
        </p>
      </div>
    </div>
  );
}

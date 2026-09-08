import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { useState, useEffect } from 'react';
import { useAppStore } from '@/store/appStore';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { MessageSquare, Loader2 } from 'lucide-react';
import { Tkwf } from '@tkwf/tsclient';
import { APP_NAME } from '@/config/app';
import type { User } from '@/types';

export const Route = createFileRoute('/auth/login')({
  component: LoginPage,
});

function LoginPage() {
  const navigate = useNavigate();
  const { isLoggedIn, hasAgreedPrivacy, login } = useAppStore();
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState('');
  
  // 如果已登录，跳转到对应首页
  useEffect(() => {
    if (isLoggedIn) {
      const user = useAppStore.getState().currentUser;
      if (user?.role === 'student') {
        navigate({ to: '/student/home' });
      } else if (user?.role === 'teacher') {
        navigate({ to: '/teacher/dashboard' });
      } else if (user?.role === 'parent') {
        navigate({ to: '/parent/dashboard' });
      }
    }
  }, [isLoggedIn, navigate]);
  
  const handleWechatLogin = async () => {
    setIsLoading(true);
    setError('');
    
    try {
      // 一键登录：不预验证（允许匿名，获取微信 ID 后走"登录验证"分配 session）
      // 使用 SDK 内置 loginByContext —— 成功后自动 persist session（IsAuthenticated=true）
      const payload = await Tkwf.Guest.loginByContext(
        'xiaoming',              // userName（Mock：微信绑定的小明）
        'mock-credential',       // credential（Mock：微信授权码）
        {
          loginFrom: 'wechat',
          authType: 'wechat',
          authInfo: 'mock-wechat-openid',
          deviceId: 'web-001',
        }
      );
      
      if (payload.success) {
        // session 已由 SDK 持久化到 localStorage（Tkwf.User 后续可用）
        
        // 创建本地用户对象
        const mockUser: User = {
          id: 'student-1',
          name: payload.displayName || '小明',
          avatar: '',
          role: 'student',
          streakDays: 0,
          classId: ''
        };
        
        // 登录（zustand 状态）
        login(mockUser);
        
        setIsLoading(false);
        
        // 检查是否需要同意隐私协议
        if (!hasAgreedPrivacy) {
          navigate({ to: '/auth/privacy' });
        } else {
          navigate({ to: '/student/home' });
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
          老师布置的背书任务，在这里完成
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

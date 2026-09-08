import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { useState, useRef, useEffect } from 'react';
import { useAppStore } from '@/store/appStore';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { ScrollArea } from '@/components/ui/scroll-area';
import { Checkbox } from '@/components/ui/checkbox';
import { ArrowLeft, Shield } from 'lucide-react';
import { APP_NAME } from '@/config/app';

export const Route = createFileRoute('/auth/privacy')({
  component: PrivacyPage,
});

function PrivacyPage() {
  const navigate = useNavigate();
  const { agreePrivacy, hasAgreedPrivacy } = useAppStore();
  const [isScrolledToBottom, setIsScrolledToBottom] = useState(false);
  const [isAgreed, setIsAgreed] = useState(false);
  const [showGuardianConfirm, setShowGuardianConfirm] = useState(false);
  const scrollRef = useRef<HTMLDivElement>(null);
  
  // 检查是否已同意
  useEffect(() => {
    if (hasAgreedPrivacy) {
      navigate({ to: '/student/home' });
    }
  }, [hasAgreedPrivacy, navigate]);
  
  const handleScroll = (e: React.UIEvent<HTMLDivElement>) => {
    const { scrollTop, scrollHeight, clientHeight } = e.currentTarget;
    if (scrollTop + clientHeight >= scrollHeight - 20) {
      setIsScrolledToBottom(true);
    }
  };
  
  const handleAgree = () => {
    if (!isAgreed) return;
    
    // 模拟未成年人检查（<14岁）
    const isMinor = false; // 实际应根据用户信息判断
    
    if (isMinor) {
      setShowGuardianConfirm(true);
    } else {
      agreePrivacy();
      navigate({ to: '/student/home' });
    }
  };
  
  const handleGuardianConfirm = () => {
    agreePrivacy();
    navigate({ to: '/student/home' });
  };
  
  return (
    <div className="min-h-screen bg-background">
      {/* 顶部导航 */}
      <header className="sticky top-0 z-10 bg-card border-b border-border px-4 py-3 flex items-center">
        <button
          onClick={() => navigate({ to: '/auth/login' })}
          className="p-2 -ml-2 rounded-full hover:bg-muted transition-colors"
        >
          <ArrowLeft className="w-5 h-5" />
        </button>
        <h1 className="flex-1 text-center font-semibold">隐私协议</h1>
        <div className="w-9" />
      </header>
      
      {/* 协议内容 */}
      <div className="p-4">
        <Card className="border-0 shadow-sm">
          <ScrollArea
            ref={scrollRef}
            onScroll={handleScroll}
            className="h-[50vh] p-4"
          >
            <div className="space-y-4 text-sm text-foreground">
              <div className="flex items-center gap-2 mb-4">
                <Shield className="w-5 h-5 text-primary" />
                <h2 className="font-semibold text-base">{APP_NAME}隐私政策</h2>
              </div>
              
              <p>欢迎使用{APP_NAME}！我们非常重视您的隐私保护。本政策将说明我们如何收集、使用和保护您的个人信息。</p>
              
              <h3 className="font-semibold mt-4">1. 信息收集</h3>
              <p>我们收集的信息包括：</p>
              <ul className="list-disc pl-5 space-y-1 text-muted-foreground">
                <li>账号信息：微信授权信息、昵称、头像</li>
                <li>学习数据：背诵记录、答题情况、学习进度</li>
                <li>设备信息：设备型号、操作系统版本</li>
              </ul>
              
              <h3 className="font-semibold mt-4">2. 信息使用</h3>
              <p>我们使用您的信息用于：</p>
              <ul className="list-disc pl-5 space-y-1 text-muted-foreground">
                <li>提供背诵任务管理和学习进度跟踪服务</li>
                <li>生成学习报告和记忆曲线分析</li>
                <li>改进产品功能和用户体验</li>
              </ul>
              
              <h3 className="font-semibold mt-4">3. 信息保护</h3>
              <p>我们采取严格的安全措施保护您的信息，包括数据加密、访问控制等。我们不会将您的个人信息出售给第三方。</p>
              
              <h3 className="font-semibold mt-4">4. 未成年人保护</h3>
              <p>未满14周岁的用户在使用本服务前，需获得监护人的同意。我们会限制对未成年人个人信息的收集和使用。</p>
              
              <h3 className="font-semibold mt-4">5. 您的权利</h3>
              <p>您有权访问、更正、删除您的个人信息，也可以随时注销账号。</p>
              
              <p className="text-muted-foreground mt-4">最后更新日期：2026年9月</p>
            </div>
          </ScrollArea>
        </Card>
      </div>
      
      {/* 同意按钮区域 */}
      <div className="fixed bottom-0 left-0 right-0 bg-card border-t border-border p-4 safe-bottom">
        <div className="max-w-md mx-auto space-y-3">
          <div className="flex items-start gap-3">
            <Checkbox
              id="agree"
              checked={isAgreed}
              onCheckedChange={(checked) => setIsAgreed(checked as boolean)}
              disabled={!isScrolledToBottom}
            />
            <label
              htmlFor="agree"
              className={`text-sm leading-tight ${
                isScrolledToBottom ? 'text-foreground' : 'text-muted-foreground'
              }`}
            >
              我已阅读并同意《隐私政策》和《用户协议》
              {!isScrolledToBottom && (
                <span className="text-xs text-muted-foreground block mt-1">
                  （请滚动阅读完整内容）
                </span>
              )}
            </label>
          </div>
          
          <Button
            onClick={handleAgree}
            disabled={!isAgreed}
            className="w-full"
            size="lg"
          >
            同意并继续
          </Button>
        </div>
      </div>
      
      {/* 监护人确认弹窗 */}
      {showGuardianConfirm && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center p-4 z-50">
          <Card className="w-full max-w-sm p-6">
            <h3 className="font-semibold text-lg mb-2">监护人确认</h3>
            <p className="text-sm text-muted-foreground mb-4">
              检测到您未满14周岁，请确认已获得监护人的同意使用本服务。
            </p>
            <div className="flex gap-3">
              <Button
                variant="outline"
                className="flex-1"
                onClick={() => setShowGuardianConfirm(false)}
              >
                取消
              </Button>
              <Button
                className="flex-1"
                onClick={handleGuardianConfirm}
              >
                监护人已同意
              </Button>
            </div>
          </Card>
        </div>
      )}
    </div>
  );
}

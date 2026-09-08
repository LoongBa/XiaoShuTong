import { useState, useRef, useEffect } from 'react';
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { cn } from '@/lib/utils';
import { 
  Share2, 
  Download, 
  Copy, 
  Link as LinkIcon,
  MessageCircle,
  Users,
  ChevronLeft,
  ChevronRight,
  X,
  Check
} from 'lucide-react';

interface ShareReportModalProps {
  isOpen: boolean;
  onClose: () => void;
  stats: {
    totalDays: number;
    totalQuestions: number;
    accuracy: number;
    masteryData: {
      total: number;
      gray: number;
      yellow: number;
      green: number;
      gold: number;
    };
  };
  userName: string;
}

const REPORT_STYLES = [
  { id: 'minimal', label: '简约风', bg: 'bg-gradient-to-br from-slate-50 to-slate-100', accent: 'text-slate-700' },
  { id: 'vibrant', label: '活力风', bg: 'bg-gradient-to-br from-orange-50 via-amber-50 to-yellow-50', accent: 'text-orange-600' },
  { id: 'fresh', label: '清新风', bg: 'bg-gradient-to-br from-emerald-50 via-teal-50 to-cyan-50', accent: 'text-emerald-600' },
  { id: 'elegant', label: '优雅风', bg: 'bg-gradient-to-br from-violet-50 via-purple-50 to-pink-50', accent: 'text-violet-600' },
];

export function ShareReportModal({ isOpen, onClose, stats, userName }: ShareReportModalProps) {
  const [currentStyle, setCurrentStyle] = useState(0);
  const [copied, setCopied] = useState(false);
  const [saved, setSaved] = useState(false);
  const reportRef = useRef<HTMLDivElement>(null);

  const currentStyleData = REPORT_STYLES[currentStyle];

  const handlePrevStyle = () => {
    setCurrentStyle((prev) => (prev === 0 ? REPORT_STYLES.length - 1 : prev - 1));
  };

  const handleNextStyle = () => {
    setCurrentStyle((prev) => (prev === REPORT_STYLES.length - 1 ? 0 : prev + 1));
  };

  const handleCopyText = () => {
    const text = `${userName}的学习战报\n📚 学习天数：${stats.totalDays}天\n📝 背诵题数：${stats.totalQuestions}题\n🎯 正确率：${stats.accuracy}%\n💡 掌握知识点：${stats.masteryData.total}条\n\n快来和我一起学习吧！`;
    navigator.clipboard.writeText(text);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  const handleCopyLink = () => {
    navigator.clipboard.writeText(window.location.href);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  const handleSaveImage = () => {
    // 模拟保存图片
    setSaved(true);
    setTimeout(() => setSaved(false), 2000);
  };

  const handleShareWechat = (type: 'moment' | 'group') => {
    // 模拟分享到微信
    alert(type === 'moment' ? '已生成朋友圈分享图' : '已生成群聊分享图');
  };

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="max-w-sm max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle className="text-center flex items-center justify-center gap-2">
            <Share2 className="w-5 h-5" />
            分享战报
          </DialogTitle>
        </DialogHeader>

        {/* 战报预览 */}
        <div className="relative">
          {/* 风格切换按钮 */}
          <button
            onClick={handlePrevStyle}
            className="absolute left-0 top-1/2 -translate-y-1/2 -translate-x-2 z-10 w-8 h-8 rounded-full bg-white shadow-md flex items-center justify-center hover:bg-gray-50"
          >
            <ChevronLeft className="w-4 h-4" />
          </button>
          <button
            onClick={handleNextStyle}
            className="absolute right-0 top-1/2 -translate-y-1/2 translate-x-2 z-10 w-8 h-8 rounded-full bg-white shadow-md flex items-center justify-center hover:bg-gray-50"
          >
            <ChevronRight className="w-4 h-4" />
          </button>

          {/* 战报卡片 */}
          <div
            ref={reportRef}
            className={cn(
              'rounded-2xl p-6 shadow-lg transition-all duration-300',
              currentStyleData.bg
            )}
          >
            {/* 标题 */}
            <div className="text-center mb-6">
              <h3 className={cn('text-xl font-bold', currentStyleData.accent)}>
                {userName}的学习战报
              </h3>
              <p className="text-sm text-gray-500 mt-1">{new Date().toLocaleDateString('zh-CN')}</p>
            </div>

            {/* 核心数据 */}
            <div className="grid grid-cols-3 gap-4 mb-6">
              <div className="text-center">
                <p className={cn('text-2xl font-bold', currentStyleData.accent)}>{stats.totalDays}</p>
                <p className="text-xs text-gray-500">学习天数</p>
              </div>
              <div className="text-center">
                <p className={cn('text-2xl font-bold', currentStyleData.accent)}>{stats.totalQuestions}</p>
                <p className="text-xs text-gray-500">背诵题数</p>
              </div>
              <div className="text-center">
                <p className={cn('text-2xl font-bold', currentStyleData.accent)}>{stats.accuracy}%</p>
                <p className="text-xs text-gray-500">正确率</p>
              </div>
            </div>

            {/* 掌握情况 */}
            <div className="bg-white/60 rounded-xl p-4 mb-4">
              <p className="text-sm text-gray-600 mb-2">掌握情况</p>
              <div className="h-3 bg-gray-200 rounded-full overflow-hidden flex">
                <div className="h-full bg-gray-400" style={{ width: `${(stats.masteryData.gray / stats.masteryData.total) * 100}%` }} />
                <div className="h-full bg-orange-400" style={{ width: `${(stats.masteryData.yellow / stats.masteryData.total) * 100}%` }} />
                <div className="h-full bg-green-500" style={{ width: `${(stats.masteryData.green / stats.masteryData.total) * 100}%` }} />
                <div className="h-full bg-yellow-400" style={{ width: `${(stats.masteryData.gold / stats.masteryData.total) * 100}%` }} />
              </div>
              <p className="text-xs text-gray-500 mt-2 text-center">
                共掌握 {stats.masteryData.total} 条知识点
              </p>
            </div>

            {/* 底部标语 */}
            <div className="text-center">
              <p className="text-xs text-gray-400">坚持学习，每天进步一点点 💪</p>
            </div>
          </div>

          {/* 风格指示器 */}
          <div className="flex justify-center gap-2 mt-3">
            {REPORT_STYLES.map((_, index) => (
              <button
                key={index}
                onClick={() => setCurrentStyle(index)}
                className={cn(
                  'w-2 h-2 rounded-full transition-all',
                  index === currentStyle ? 'bg-primary w-4' : 'bg-gray-300'
                )}
              />
            ))}
          </div>
          <p className="text-xs text-center text-muted-foreground mt-1">
            {currentStyleData.label}
          </p>
        </div>

        {/* 分享选项 */}
        <div className="grid grid-cols-2 gap-3 mt-6">
          <Button
            variant="outline"
            onClick={handleSaveImage}
            className="flex items-center gap-2"
          >
            {saved ? <Check className="w-4 h-4 text-green-500" /> : <Download className="w-4 h-4" />}
            {saved ? '已保存' : '保存图片'}
          </Button>
          <Button
            variant="outline"
            onClick={handleCopyText}
            className="flex items-center gap-2"
          >
            {copied ? <Check className="w-4 h-4 text-green-500" /> : <Copy className="w-4 h-4" />}
            {copied ? '已复制' : '复制文字'}
          </Button>
        </div>

        <div className="grid grid-cols-2 gap-3">
          <Button
            variant="outline"
            onClick={handleCopyLink}
            className="flex items-center gap-2"
          >
            <LinkIcon className="w-4 h-4" />
            复制链接
          </Button>
          <Button
            variant="outline"
            onClick={() => handleShareWechat('moment')}
            className="flex items-center gap-2"
          >
            <MessageCircle className="w-4 h-4" />
            朋友圈
          </Button>
        </div>

        <Button
          onClick={() => handleShareWechat('group')}
          className="w-full flex items-center justify-center gap-2"
        >
          <Users className="w-4 h-4" />
          分享到微信群
        </Button>
      </DialogContent>
    </Dialog>
  );
}

import { useState } from 'react';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { cn } from '@/lib/utils';
import { Plus, Users, Share2, Download, X } from 'lucide-react';

interface Buddy {
  id: string;
  name: string;
  avatar?: string;
  streakDays: number;
}

interface StudyBuddySectionProps {
  buddies: Buddy[];
  onInvite: () => void;
}

const MAX_BUDDIES = 5;

export function StudyBuddySection({ buddies, onInvite }: StudyBuddySectionProps) {
  const [showInviteModal, setShowInviteModal] = useState(false);
  const [showPoster, setShowPoster] = useState(false);

  const emptySlots = MAX_BUDDIES - buddies.length;

  const handleGeneratePoster = () => {
    setShowInviteModal(false);
    setShowPoster(true);
  };

  const handleSavePoster = () => {
    alert('邀请海报已保存');
    setShowPoster(false);
  };

  return (
    <>
      <Card className="p-4">
        <div className="flex items-center justify-between mb-4">
          <h2 className="font-semibold flex items-center gap-2">
            <Users className="w-4 h-4" />
            学习搭子
          </h2>
          <span className="text-xs text-muted-foreground">
            {buddies.length}/{MAX_BUDDIES}
          </span>
        </div>

        {/* 搭子头像列表 */}
        <div className="flex items-center gap-3">
          {buddies.map((buddy) => (
            <div key={buddy.id} className="flex flex-col items-center">
              <div className="w-12 h-12 rounded-full bg-gradient-to-br from-primary/20 to-primary/10 flex items-center justify-center text-sm font-medium border-2 border-primary/20">
                {buddy.avatar ? (
                  <img src={buddy.avatar} alt={buddy.name} className="w-full h-full rounded-full" />
                ) : (
                  buddy.name.charAt(0)
                )}
              </div>
              <span className="text-xs text-muted-foreground mt-1 truncate w-12 text-center">
                {buddy.name}
              </span>
            </div>
          ))}

          {/* 空位 + 按钮 */}
          {Array.from({ length: emptySlots }).map((_, index) => (
            <button
              key={`empty-${index}`}
              onClick={() => setShowInviteModal(true)}
              className="flex flex-col items-center"
            >
              <div className="w-12 h-12 rounded-full bg-muted flex items-center justify-center border-2 border-dashed border-muted-foreground/30 hover:border-primary/50 hover:bg-primary/5 transition-colors">
                <Plus className="w-5 h-5 text-muted-foreground" />
              </div>
              <span className="text-xs text-muted-foreground mt-1">邀请</span>
            </button>
          ))}
        </div>

        {buddies.length === 0 && (
          <p className="text-sm text-muted-foreground text-center py-2">
            还没有学习搭子，快邀请好友一起学习吧！
          </p>
        )}
      </Card>

      {/* 邀请弹窗 */}
      <Dialog open={showInviteModal} onOpenChange={setShowInviteModal}>
        <DialogContent className="max-w-sm">
          <DialogHeader>
            <DialogTitle className="text-center">邀请学习搭子</DialogTitle>
          </DialogHeader>
          <div className="space-y-4">
            <p className="text-sm text-muted-foreground text-center">
              邀请好友成为你的学习搭子，互相监督，共同进步！
            </p>
            <div className="grid grid-cols-2 gap-3">
              <Button variant="outline" onClick={() => alert('已复制邀请链接')}>
                复制链接
              </Button>
              <Button variant="outline" onClick={() => alert('已生成微信邀请')}>
                微信邀请
              </Button>
            </div>
            <Button onClick={handleGeneratePoster} className="w-full">
              <Share2 className="w-4 h-4 mr-2" />
              生成邀请海报
            </Button>
          </div>
        </DialogContent>
      </Dialog>

      {/* 邀请海报弹窗 */}
      <Dialog open={showPoster} onOpenChange={setShowPoster}>
        <DialogContent className="max-w-sm">
          <DialogHeader>
            <DialogTitle className="text-center">邀请海报</DialogTitle>
          </DialogHeader>
          <div className="space-y-4">
            {/* 海报预览 */}
            <div className="bg-gradient-to-br from-primary/10 via-amber-50 to-orange-50 rounded-2xl p-6 shadow-lg">
              <div className="text-center mb-4">
                <h3 className="text-xl font-bold text-primary">一起来学习吧！</h3>
                <p className="text-sm text-muted-foreground mt-1">成为我的学习搭子</p>
              </div>
              
              <div className="bg-white/80 rounded-xl p-4 mb-4">
                <div className="flex items-center gap-3">
                  <div className="w-12 h-12 rounded-full bg-primary/10 flex items-center justify-center text-lg font-medium">
                    我
                  </div>
                  <div>
                    <p className="font-medium">邀请你一起学习</p>
                    <p className="text-xs text-muted-foreground">互相监督 · 共同进步</p>
                  </div>
                </div>
              </div>

              <div className="text-center">
                <div className="inline-block bg-white p-2 rounded-lg shadow-sm">
                  <div className="w-24 h-24 bg-gray-200 rounded flex items-center justify-center">
                    <span className="text-xs text-gray-400">二维码</span>
                  </div>
                </div>
                <p className="text-xs text-muted-foreground mt-2">扫码加入</p>
              </div>
            </div>

            <Button onClick={handleSavePoster} className="w-full">
              <Download className="w-4 h-4 mr-2" />
              保存海报
            </Button>
          </div>
        </DialogContent>
      </Dialog>
    </>
  );
}

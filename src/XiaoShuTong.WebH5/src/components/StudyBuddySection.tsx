import { useCallback, useEffect, useState } from 'react';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { cn } from '@/lib/utils';
import { Tkwf } from '@tkwf/tsclient';
import type {
  BuddyCandidates_ExecuteService,
  InviteBuddy_ExecuteService,
  BuddyCandidateItemDto,
} from '@/gql/ts-client.g';
import { Plus, Users, Share2, Download, Loader2 } from 'lucide-react';

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

// 错误码 → 文案（V0.6.16：6003/6005/6006/6002；6001 在 accept/reject 端不在此映射——Oracle C3）
const INVITE_ERROR_MESSAGES: Record<string, string> = {
  '6003': '学习搭子已满（最多 5 个）',
  '6005': '今日邀请次数已达上限',
  '6006': '仅可邀请同班同学',
  '6002': '已发送过邀请，请等待对方回应',
};

export function StudyBuddySection({ buddies, onInvite }: StudyBuddySectionProps) {
  const [showInviteModal, setShowInviteModal] = useState(false);
  const [showPoster, setShowPoster] = useState(false);
  // V0.6.16：可邀候选（好友发现——同群学生成员；打开弹窗时拉取，Oracle C2 关闭后再开重拉）
  const [candidates, setCandidates] = useState<BuddyCandidateItemDto[]>([]);
  const [candidatesLoading, setCandidatesLoading] = useState(false);
  const [invitingId, setInvitingId] = useState<number | null>(null);
  const [inviteError, setInviteError] = useState('');

  const emptySlots = MAX_BUDDIES - buddies.length;

  // V0.6.16（F1）：打开邀请弹窗 → 拉候选（listBuddyCandidates_Execute）
  const loadCandidates = useCallback(async () => {
    setCandidatesLoading(true);
    setInviteError('');
    try {
      const res = await Tkwf.User.Use<BuddyCandidates_ExecuteService>().listBuddyCandidates_Execute();
      setCandidates(res.success ? (res.items ?? []) : []);
    } catch {
      setCandidates([]);
    } finally {
      setCandidatesLoading(false);
    }
  }, []);

  useEffect(() => {
    if (showInviteModal) void loadCandidates();
  }, [showInviteModal, loadCandidates]);

  // V0.6.16（F1）：发起邀请（inviteBuddy_Execute；成功关弹窗——Oracle C2 再次打开自动重拉候选）
  const handleInvite = async (candidate: BuddyCandidateItemDto) => {
    setInvitingId(candidate.userId);
    setInviteError('');
    try {
      const res = await Tkwf.User.Use<InviteBuddy_ExecuteService>().inviteBuddy_Execute({
        request: { inviteeUserId: candidate.userId },
      });
      if (res.success) {
        setShowInviteModal(false);
        return;
      }
      setInviteError(INVITE_ERROR_MESSAGES[res.errorCode ?? ''] ?? '邀请失败，请稍后重试');
    } catch (err: any) {
      setInviteError(err?.code ? (INVITE_ERROR_MESSAGES[err.code] ?? err.message ?? '邀请失败') : '邀请失败，请稍后重试');
    } finally {
      setInvitingId(null);
    }
  };

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

      {/* 邀请弹窗（V0.6.16：候选列表 + 真实邀请，替代纯 alert 模拟） */}
      <Dialog open={showInviteModal} onOpenChange={setShowInviteModal}>
        <DialogContent className="max-w-sm">
          <DialogHeader>
            <DialogTitle className="text-center">邀请学习搭子</DialogTitle>
          </DialogHeader>
          <div className="space-y-4">
            <p className="text-sm text-muted-foreground text-center">
              邀请同班同学成为学习搭子，互相监督共同进步！
            </p>

            {/* 候选列表（好友发现——listBuddyCandidates_Execute） */}
            {candidatesLoading ? (
              <div className="flex items-center justify-center py-8 text-muted-foreground">
                <Loader2 className="w-4 h-4 animate-spin mr-2" />
                加载同学…
              </div>
            ) : candidates.length === 0 ? (
              // Oracle C1：候选空态——暂无同班同学可邀
              <div className="text-center py-6">
                <p className="text-sm text-muted-foreground mb-3">暂无可邀请的同班同学</p>
                <p className="text-xs text-muted-foreground/70 mb-3">
                  也可通过链接或海报邀请好友加入学习
                </p>
                <div className="grid grid-cols-2 gap-3">
                  <Button variant="outline" size="sm" onClick={() => alert('已复制邀请链接')}>
                    复制链接
                  </Button>
                  <Button variant="outline" size="sm" onClick={() => alert('已生成微信邀请')}>
                    微信邀请
                  </Button>
                </div>
              </div>
            ) : (
              <div className="space-y-2">
                {candidates.map((c) => (
                  <div
                    key={c.userId}
                    className="flex items-center justify-between p-3 rounded-lg bg-muted/50"
                  >
                    <div className="min-w-0">
                      <p className="text-sm font-medium truncate">{c.nickname}</p>
                      <p className="text-xs text-muted-foreground truncate">{c.groupName}</p>
                    </div>
                    <Button
                      size="sm"
                      variant="outline"
                      disabled={invitingId === c.userId}
                      onClick={() => handleInvite(c)}
                    >
                      {invitingId === c.userId ? (
                        <Loader2 className="w-3 h-3 animate-spin" />
                      ) : (
                        '邀请'
                      )}
                    </Button>
                  </div>
                ))}
                {inviteError && (
                  <p className="text-xs text-destructive text-center pt-1">{inviteError}</p>
                )}
              </div>
            )}
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
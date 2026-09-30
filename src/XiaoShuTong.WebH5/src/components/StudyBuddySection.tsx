import { useCallback, useEffect, useState } from 'react';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { cn } from '@/lib/utils';
import { DomainClientError, Tkwf } from '@tkwf/tsclient';
import { executeQuery } from '@/lib/sdk-bypass';
import type {
  BuddyCandidates_ExecuteService,
  InviteBuddy_ExecuteService,
  BuddyCandidateItemDto,
  PendingBuddyInvites_ExecuteService,
  AcceptBuddyInvite_ExecuteService,
  RejectBuddyInvite_ExecuteService,
  PendingBuddyInviteItemDto,
  RemoveBuddyResDto,
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
  // V0.6.18（Oracle M2）：accept 成功后刷新 rank.tsx 搭子列表（loadBuddies useCallback 复用）
  onBuddyChanged?: () => void;
}

const MAX_BUDDIES = 5;

// 错误码 → 文案（V0.6.16：6003/6005/6006/6002；6001 在 accept/reject 端不在此映射——Oracle C3）
const INVITE_ERROR_MESSAGES: Record<string, string> = {
  '6003': '学习搭子已满（最多 5 个）',
  '6005': '今日邀请次数已达上限',
  '6006': '仅可邀请同班或同年级同学',
  '6002': '已发送过邀请，请等待对方回应',
};

// V0.6.18（Oracle C6）：accept/reject 端错误码文案（6001=不存在/非本人权限伪装；6003 仅 accept 端触发）
const ACCEPT_REJECT_ERROR_MESSAGES: Record<string, string> = {
  '6001': '邀请已失效',
  '6002': '邀请已过期或已处理',
  '6003': '学习搭子已满（最多 5 个）',
};

// 任务5：removeBuddy_Execute 端错误码文案（BR-30/31/32 均返 6001=关系不存在/已解除）
const REMOVE_ERROR_MESSAGES: Record<string, string> = {
  '6001': '搭子关系不存在或已解除',
};

// V0.6.18：相对时间"X 天前"（当天显示"今天"）
function daysAgo(iso: string): string {
  const diff = Date.now() - new Date(iso).getTime();
  const days = Math.floor(diff / 86_400_000);
  if (days <= 0) return '今天';
  return `${days} 天前`;
}

export function StudyBuddySection({ buddies, onBuddyChanged }: StudyBuddySectionProps) {
  const [showInviteModal, setShowInviteModal] = useState(false);
  const [showPoster, setShowPoster] = useState(false);
  // V0.6.16：可邀候选（好友发现——同群学生成员；打开弹窗时拉取，Oracle C2 关闭后再开重拉）
  const [candidates, setCandidates] = useState<BuddyCandidateItemDto[]>([]);
  const [candidatesLoading, setCandidatesLoading] = useState(false);
  const [invitingId, setInvitingId] = useState<number | null>(null);
  const [inviteError, setInviteError] = useState('');

  // V0.6.18：待收邀请（listPendingBuddyInvites_Execute；挂载拉取 + accept/reject 成功后重拉）
  const [pendingInvites, setPendingInvites] = useState<PendingBuddyInviteItemDto[]>([]);
  const [pendingLoading, setPendingLoading] = useState(false);
  const [processingId, setProcessingId] = useState<string | null>(null);
  const [pendingError, setPendingError] = useState('');

  // 任务5：解除搭子（removeBuddy_Execute）——确认弹窗目标 + 处理中禁点 + 错误提示
  const [removeTarget, setRemoveTarget] = useState<Buddy | null>(null);
  const [removingId, setRemovingId] = useState<string | null>(null);
  const [removeError, setRemoveError] = useState('');

  const emptySlots = MAX_BUDDIES - buddies.length;

  // V0.6.18（F1）：拉取待收邀请（失败静默空态——辅助区非阻塞）
  const loadPendingInvites = useCallback(async () => {
    setPendingLoading(true);
    setPendingError('');
    try {
      const res = await Tkwf.User.Use<PendingBuddyInvites_ExecuteService>().listPendingBuddyInvites_Execute();
      setPendingInvites(res.success ? (res.items ?? []) : []);
    } catch {
      setPendingInvites([]);
    } finally {
      setPendingLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadPendingInvites();
  }, [loadPendingInvites]);

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

  // V0.6.18（F1）：接受邀请（acceptBuddyInvite_Execute；成功 → 重拉待收 + 刷新搭子列表）
  const handleAccept = async (inv: PendingBuddyInviteItemDto) => {
    setProcessingId(inv.inviteId);
    setPendingError('');
    try {
      const res = await Tkwf.User.Use<AcceptBuddyInvite_ExecuteService>().acceptBuddyInvite_Execute({
        request: { inviteId: inv.inviteId },
      });
      if (res.success) {
        await loadPendingInvites(); // 重拉待收（该邀请移除）
        onBuddyChanged?.();         // M2：刷新 rank.tsx 搭子列表（accepted 立即可见）
        return;
      }
      const code = res.errorCode ?? '';
      if (code === '6001' || code === '6002') {
        setPendingError(ACCEPT_REJECT_ERROR_MESSAGES[code] ?? '邀请已失效');
        await loadPendingInvites(); // 失效项从列表移除（重拉权威）
        return;
      }
      setPendingError(ACCEPT_REJECT_ERROR_MESSAGES[code] ?? '处理失败，请稍后重试'); // 6003 保留列表（仍可稍后处理）
    } catch (err: any) {
      // Oracle C4：网络异常 → 提示 + 不重拉（避免重拉失败雪崩）
      setPendingError(err?.message ?? '网络错误，请稍后重试');
    } finally {
      setProcessingId(null); // Oracle C5：任何分支清空禁点状态
    }
  };

  // V0.6.18（F1）：拒绝邀请（rejectBuddyInvite_Execute；成功 → 仅重拉待收，不触发 onBuddyChanged——Oracle C10）
  const handleReject = async (inv: PendingBuddyInviteItemDto) => {
    setProcessingId(inv.inviteId);
    setPendingError('');
    try {
      const res = await Tkwf.User.Use<RejectBuddyInvite_ExecuteService>().rejectBuddyInvite_Execute({
        request: { inviteId: inv.inviteId },
      });
      if (res.success) {
        await loadPendingInvites();
        return;
      }
      const code = res.errorCode ?? '';
      if (code === '6001' || code === '6002') {
        setPendingError(ACCEPT_REJECT_ERROR_MESSAGES[code] ?? '邀请已失效');
        await loadPendingInvites();
        return;
      }
      setPendingError('处理失败，请稍后重试');
    } catch (err: any) {
      setPendingError(err?.message ?? '网络错误，请稍后重试');
    } finally {
      setProcessingId(null);
    }
  };

  // 任务5（F1）：解除搭子（removeBuddy_Execute；确认弹窗 → 成功关弹窗 + 刷新搭子列表，
  //  6001 关系不存在/已解除 → 提示 + 列表权威重拉；网络异常 → 提示 + 不重拉）
  //  V0.7.7（G16）：SDK 前缀启发式把 remove* 判 mutation 导致 400 → 走 executeQuery 强制 query（ts-client.g.ts 已标 type:'query'）
  const handleRemove = async (buddy: Buddy) => {
    setRemovingId(buddy.id);
    setRemoveError('');
    try {
      const res = await executeQuery<RemoveBuddyResDto>('removeBuddy_Execute', {
        request: { buddyId: buddy.id },
      });
      if (res.success) {
        setRemoveTarget(null); // 成功关确认弹窗
        onBuddyChanged?.();    // 刷新 rank.tsx 搭子列表（解除立即可见）
        return;
      }
      const code = res.errorCode ?? '';
      if (code === '6001') {
        setRemoveError(REMOVE_ERROR_MESSAGES[code] ?? '搭子关系不存在或已解除');
        onBuddyChanged?.(); // 列表权威重拉（该搭子可能已被移除）
        return;
      }
      setRemoveError('解除失败，请稍后重试');
    } catch (err: unknown) {
      // 网络异常 → 提示 + 不重拉（对齐 V0.6.18 C4，避免重拉失败雪崩）
      setRemoveError(
        err instanceof DomainClientError && err.message
          ? err.message
          : '网络错误，请稍后重试',
      );
    } finally {
      setRemovingId(null); // 任何分支清空禁点状态
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
              <div className="relative">
                <div className="w-12 h-12 rounded-full bg-gradient-to-br from-primary/20 to-primary/10 flex items-center justify-center text-sm font-medium border-2 border-primary/20">
                  {buddy.avatar ? (
                    <img src={buddy.avatar} alt={buddy.name} className="w-full h-full rounded-full" />
                  ) : (
                    buddy.name.charAt(0)
                  )}
                </div>
                {/* 任务5：解除搭子入口（头像右上小 ×，点击弹确认 Dialog） */}
                <button
                  type="button"
                  onClick={() => {
                    setRemoveTarget(buddy);
                    setRemoveError('');
                  }}
                  aria-label={`解除与 ${buddy.name} 的学习搭子关系`}
                  className="absolute -top-1.5 -right-1.5 w-4 h-4 rounded-full bg-destructive text-destructive-foreground text-[10px] leading-none flex items-center justify-center hover:opacity-80 transition-opacity"
                >
                  ×
                </button>
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

        {/* 待收邀请区块（V0.6.18：listPendingBuddyInvites_Execute + accept/reject 真实接线）
            Oracle M1：空态隐藏区块；有内容时标题带 N + 红点视觉强调（被动触发，唯一发现入口） */}
        {!pendingLoading && pendingInvites.length > 0 && (
          <div className="mt-4 border-t pt-3">
            <h3 className="text-sm font-semibold flex items-center gap-2 text-primary mb-2">
              <span className="w-2 h-2 rounded-full bg-red-500" />
              待处理邀请 ({pendingInvites.length})
            </h3>
            <div className="space-y-2">
              {pendingInvites.map((inv) => (
                <div
                  key={inv.inviteId}
                  className="flex items-center justify-between p-3 rounded-lg bg-muted/50"
                >
                  <div className="min-w-0">
                    <p className="text-sm font-medium truncate">
                      {inv.nickname || `学生${inv.inviterUserId}`}
                    </p>
                    <p className="text-xs text-muted-foreground">{daysAgo(inv.invitedAt)}邀请</p>
                  </div>
                  <div className="flex items-center gap-2 shrink-0">
                    <Button
                      size="sm"
                      disabled={processingId === inv.inviteId}
                      onClick={() => handleAccept(inv)}
                    >
                      {processingId === inv.inviteId ? (
                        <Loader2 className="w-3 h-3 animate-spin" />
                      ) : (
                        '接受'
                      )}
                    </Button>
                    <Button
                      size="sm"
                      variant="outline"
                      disabled={processingId === inv.inviteId}
                      onClick={() => handleReject(inv)}
                    >
                      {processingId === inv.inviteId ? (
                        <Loader2 className="w-3 h-3 animate-spin" />
                      ) : (
                        '拒绝'
                      )}
                    </Button>
                  </div>
                </div>
              ))}
              {pendingError && (
                <p className="text-xs text-destructive text-center pt-1">{pendingError}</p>
              )}
            </div>
          </div>
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
              邀请同班或同年级同学成为学习搭子，互相监督共同进步！
            </p>

            {/* 候选列表（好友发现——listBuddyCandidates_Execute） */}
            {candidatesLoading ? (
              <div className="flex items-center justify-center py-8 text-muted-foreground">
                <Loader2 className="w-4 h-4 animate-spin mr-2" />
                加载同学…
              </div>
            ) : candidates.length === 0 ? (
              // Oracle C1：候选空态——暂无同班/同年级同学可邀（V0.7.0 BR-33 同群 OR 同年级）
              <div className="text-center py-6">
                <p className="text-sm text-muted-foreground mb-3">暂无可邀请的同班或同年级同学</p>
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

      {/* 解除搭子确认弹窗（任务5：removeBuddy_Execute；Oracle M-4/C4：Dialog 而非原生 confirm） */}
      <Dialog open={!!removeTarget} onOpenChange={(open) => !open && setRemoveTarget(null)}>
        <DialogContent className="max-w-sm">
          <DialogHeader>
            <DialogTitle className="text-center">解除学习搭子</DialogTitle>
          </DialogHeader>
          <div className="space-y-4">
            <p className="text-sm text-muted-foreground text-center">
              确定解除与 {removeTarget?.name ?? ''} 的学习搭子关系？
            </p>
            {removeError && (
              <p className="text-xs text-destructive text-center">{removeError}</p>
            )}
            <div className="flex items-center gap-3">
              <Button
                variant="outline"
                className="flex-1"
                disabled={removingId !== null}
                onClick={() => setRemoveTarget(null)}
              >
                取消
              </Button>
              <Button
                className="flex-1"
                disabled={removingId !== null}
                onClick={() => removeTarget && handleRemove(removeTarget)}
              >
                {removingId !== null ? (
                  <Loader2 className="w-3 h-3 animate-spin mr-1" />
                ) : null}
                确认解除
              </Button>
            </div>
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
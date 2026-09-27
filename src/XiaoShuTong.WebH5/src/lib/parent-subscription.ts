import type { SubscriptionBriefDto } from '@/gql/ts-client.g';

// V0.6.15（Oracle C2）：订阅状态派生纯函数——dashboard/progress/weakness 共用，避免判定逻辑复制漂移
// 对齐 ParentReportGate 有效权益：Trialing（试用中）/ Active（已订阅）/ Cancelled（周期内仍有权）

export interface ParentSubscriptionState {
  /** 是否享有订阅权益（Trialing/Active/Cancelled） */
  isSubscribed: boolean;
  /** 试用剩余天数（Trialing 且未过期；0=非试用或已过期） */
  trialDaysLeft: number;
}

/** 从 dashboardReport.subscription 契约派生订阅状态（纯函数，无副作用） */
export function deriveSubscription(sub: SubscriptionBriefDto | null): ParentSubscriptionState {
  if (!sub) return { isSubscribed: false, trialDaysLeft: 0 };

  const isSubscribed =
    sub.status === 'Active' || sub.status === 'Trialing' || sub.status === 'Cancelled';

  const trialDaysLeft =
    sub.status === 'Trialing' && sub.trialEndAt
      ? Math.max(0, Math.ceil((new Date(sub.trialEndAt).getTime() - Date.now()) / 86400000))
      : 0;

  return { isSubscribed, trialDaysLeft };
}
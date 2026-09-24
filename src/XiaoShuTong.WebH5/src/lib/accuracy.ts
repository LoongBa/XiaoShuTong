import type { MemoryState } from '@/types';

// ── 掌握度（0~1）→ 四阶状态 阈值映射 ──
// V0.6.3 联调决策（V0.6.2 方案 L122 用户裁决：透出原始 0~1，前端映射"掌握 70%"展示语义）
// 对齐 UI 文档-v2 状态色四阶口径（R01 家长报告 L120：✕灰-未掌握 / △橙-需巩固 / ○绿-已掌握 / ★金-熟练，Oracle 闭环#4）
const ACCURACY_THRESHOLDS = [
  { min: 0.0, state: 'gray' as const },    // <0.3 未掌握
  { min: 0.3, state: 'yellow' as const },  // 0.3~0.6 需巩固
  { min: 0.6, state: 'green' as const },   // 0.6~0.85 已掌握
  { min: 0.85, state: 'gold' as const },   // ≥0.85 熟练
];

/** 掌握度百分比（0~1 → 0-100 整数，展示用，如 0.7 → 70） */
export function accuracyToPercent(accuracy: number | null | undefined): number {
  if (accuracy == null || Number.isNaN(accuracy)) return 0;
  return Math.round(Math.max(0, Math.min(1, accuracy)) * 100);
}

/** 掌握度（0~1）→ 四阶记忆状态（✕△○★，阈值表集中定义） */
export function accuracyToState(accuracy: number | null | undefined): MemoryState {
  const value = accuracy ?? 0;
  for (const t of ACCURACY_THRESHOLDS) {
    if (value >= t.min) return t.state;
  }
  return 'gray';
}
import type { MemoryState } from '@/types';

// 服务端四阶记忆状态 → 前端 MemoryState 枚举
// 由 SubmitAttempt 响应 preState/postState 与 SessionResult blockedPoints.state 驱动，不再本地推导
// 真实后端下发 PascalCase 枚举名（Enums.cs：NotMastered=0 / Fuzzy=1 / Mastered=2 / Proficient=3，存储 string）
export function serverStateToMemoryState(state: string | null | undefined): MemoryState {
  switch (state) {
    // PascalCase 枚举名（真实后端下发）
    case 'NotMastered': return 'gray';
    case 'Fuzzy': return 'yellow';
    case 'Mastered': return 'green';
    case 'Proficient': return 'gold';
    // 显示符号兼容（旧 mock / 历史数据）
    case '✕': return 'gray';
    case '△': return 'yellow';
    case '○': return 'green';
    case '★': return 'gold';
    default: return 'gray';
  }
}

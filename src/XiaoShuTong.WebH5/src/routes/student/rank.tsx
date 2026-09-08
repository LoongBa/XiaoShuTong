import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { useEffect, useState } from 'react';
import { useAppStore } from '@/store/appStore';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { BottomNav } from '@/components/BottomNav';
import { StreakBadge } from '@/components/StreakBadge';
import {
  ArrowLeft,
  Trophy,
  Flame,
  Star,
  Target,
  Users,
  ChevronRight,
  Crown,
  TrendingUp,
  TrendingDown,
  Minus,
  ChevronDown,
  BookOpen
} from 'lucide-react';
import { cn } from '@/lib/utils';
import { StudyBuddySection } from '@/components/StudyBuddySection';

// 模拟学习搭子数据
const MOCK_BUDDIES = [
  { id: 'b1', name: '小红', streakDays: 12 },
  { id: 'b2', name: '小刚', streakDays: 10 },
  { id: 'b3', name: '小丽', streakDays: 8 },
];

// 排名项（与真实接口 RankingItemDto 形状对齐：mock/真实切换不改页面代码）
interface RankingDataItem {
  rank: number;
  userId: string;
  userName: string;
  value: number;
  avatar: string;
  streakDays: number;
  correctRate: number;
  isSelf: boolean;
  trend: 'up' | 'down' | 'same';
}

// 模拟排名数据
const MOCK_POWER_RANKING: RankingDataItem[] = [
  { rank: 1, userId: 's1', userName: '小明', value: 45, avatar: '', streakDays: 15, correctRate: 95, isSelf: true, trend: 'up' },
  { rank: 2, userId: 's2', userName: '小红', value: 42, avatar: '', streakDays: 12, correctRate: 92, isSelf: false, trend: 'same' },
  { rank: 3, userId: 's3', userName: '小刚', value: 38, avatar: '', streakDays: 10, correctRate: 88, isSelf: false, trend: 'up' },
  { rank: 4, userId: 's4', userName: '小丽', value: 35, avatar: '', streakDays: 8, correctRate: 90, isSelf: false, trend: 'down' },
  { rank: 5, userId: 's5', userName: '小华', value: 32, avatar: '', streakDays: 7, correctRate: 82, isSelf: false, trend: 'same' },
];

const MOCK_SCORE_RANKING: RankingDataItem[] = [
  { rank: 1, userId: 's4', userName: '小丽', value: 98, avatar: '', streakDays: 8, correctRate: 98, isSelf: false, trend: 'up' },
  { rank: 2, userId: 's1', userName: '小明', value: 95, avatar: '', streakDays: 15, correctRate: 95, isSelf: true, trend: 'same' },
  { rank: 3, userId: 's2', userName: '小红', value: 92, avatar: '', streakDays: 12, correctRate: 92, isSelf: false, trend: 'down' },
  { rank: 4, userId: 's6', userName: '小军', value: 88, avatar: '', streakDays: 5, correctRate: 88, isSelf: false, trend: 'up' },
  { rank: 5, userId: 's3', userName: '小刚', value: 85, avatar: '', streakDays: 10, correctRate: 85, isSelf: false, trend: 'same' },
];

const SCOPE_OPTIONS = [
  { id: 'class', label: '班级' },
  { id: 'grade', label: '年级' },
];

const SUBJECT_OPTIONS = [
  { id: 'all', label: '全部学科' },
  { id: 'chinese', label: '语文' },
  { id: 'math', label: '数学' },
  { id: 'english', label: '英语' },
  { id: 'physics', label: '物理' },
  { id: 'chemistry', label: '化学' },
  { id: 'biology', label: '生物' },
  { id: 'history', label: '历史' },
  { id: 'geography', label: '地理' },
  { id: 'politics', label: '政治' },
];

export const Route = createFileRoute('/student/rank')({
  component: RankPage,
});

function RankPage() {
  const navigate = useNavigate();
  const { isLoggedIn, currentUser } = useAppStore();
  const [activeTab, setActiveTab] = useState<'power' | 'score'>('power');
  const [activeScope, setActiveScope] = useState('class');
  const [activeSubject, setActiveSubject] = useState('all');
  const [showSubjectDropdown, setShowSubjectDropdown] = useState(false);
  
  // 检查登录状态
  useEffect(() => {
    if (!isLoggedIn) {
      navigate({ to: '/auth/login' });
    }
  }, [isLoggedIn, navigate]);
  
  const rankingData = activeTab === 'power' ? MOCK_POWER_RANKING : MOCK_SCORE_RANKING;
  const selfRank = rankingData.find(r => r.isSelf);
  
  const getRankIcon = (rank: number) => {
    if (rank === 1) return <Crown className="w-5 h-5 text-amber-500" />;
    if (rank === 2) return <span className="text-lg font-bold text-gray-400">2</span>;
    if (rank === 3) return <span className="text-lg font-bold text-amber-700">3</span>;
    return <span className="text-sm font-medium text-muted-foreground">{rank}</span>;
  };
  
  const getRankStyle = (rank: number) => {
    if (rank === 1) return 'bg-gradient-to-r from-amber-100 to-yellow-50 border-amber-200';
    if (rank === 2) return 'bg-gradient-to-r from-gray-100 to-gray-50 border-gray-200';
    if (rank === 3) return 'bg-gradient-to-r from-amber-50 to-orange-50 border-amber-100';
    return '';
  };
  
  return (
    <div className="min-h-screen bg-background pb-20">
      {/* 顶部导航 */}
      <header className="sticky top-0 z-10 bg-card border-b border-border px-4 py-3">
        <div className="flex items-center gap-3">
          <button
            onClick={() => navigate({ to: '/student/home' })}
            className="p-2 -ml-2 rounded-full hover:bg-muted transition-colors"
          >
            <ArrowLeft className="w-5 h-5" />
          </button>
          <h1 className="font-semibold">排行榜</h1>
        </div>
      </header>
      
      <div className="p-4 space-y-4">
        {/* 范围和学科选择 */}
        <div className="flex gap-2">
          {/* 范围选择 */}
          {SCOPE_OPTIONS.map((scope) => (
            <button
              key={scope.id}
              onClick={() => setActiveScope(scope.id)}
              className={cn(
                'flex-1 py-2 px-4 rounded-lg text-sm font-medium transition-colors',
                activeScope === scope.id
                  ? 'bg-primary text-primary-foreground'
                  : 'bg-muted text-muted-foreground hover:bg-muted/80'
              )}
            >
              {scope.label}
            </button>
          ))}

          {/* 学科下拉 */}
          <div className="relative">
            <button
              onClick={() => setShowSubjectDropdown(!showSubjectDropdown)}
              className="flex items-center gap-1 py-2 px-3 rounded-lg text-sm font-medium bg-muted text-muted-foreground hover:bg-muted/80 transition-colors"
            >
              <BookOpen className="w-4 h-4" />
              <span className="max-w-[4em] truncate">
                {SUBJECT_OPTIONS.find(s => s.id === activeSubject)?.label}
              </span>
              <ChevronDown className="w-3 h-3" />
            </button>

            {showSubjectDropdown && (
              <Card className="absolute right-0 top-full mt-1 z-20 w-32 max-h-48 overflow-y-auto">
                {SUBJECT_OPTIONS.map((subject) => (
                  <button
                    key={subject.id}
                    onClick={() => {
                      setActiveSubject(subject.id);
                      setShowSubjectDropdown(false);
                    }}
                    className={cn(
                      'w-full text-left px-3 py-2 text-sm transition-colors',
                      activeSubject === subject.id
                        ? 'bg-primary/10 text-primary'
                        : 'hover:bg-muted'
                    )}
                  >
                    {subject.label}
                  </button>
                ))}
              </Card>
            )}
          </div>
        </div>
        
        {/* 榜单类型切换 */}
        <div className="flex gap-2">
          <button
            onClick={() => setActiveTab('power')}
            className={cn(
              'flex-1 py-3 px-4 rounded-xl text-sm font-medium transition-all flex items-center justify-center gap-2',
              activeTab === 'power'
                ? 'bg-gradient-to-r from-orange-500 to-amber-500 text-white shadow-lg shadow-orange-200'
                : 'bg-muted text-muted-foreground hover:bg-muted/80'
            )}
          >
            <Flame className="w-4 h-4" />
            战力榜
          </button>
          <button
            onClick={() => setActiveTab('score')}
            className={cn(
              'flex-1 py-3 px-4 rounded-xl text-sm font-medium transition-all flex items-center justify-center gap-2',
              activeTab === 'score'
                ? 'bg-gradient-to-r from-primary to-blue-500 text-white shadow-lg shadow-primary/20'
                : 'bg-muted text-muted-foreground hover:bg-muted/80'
            )}
          >
            <Trophy className="w-4 h-4" />
            战绩榜
          </button>
        </div>
        
        {/* 榜单说明 */}
        <div className="text-center">
          <p className="text-xs text-muted-foreground">
            {activeTab === 'power' 
              ? '🔥 战力榜：鼓励付出，按坚持天数和背诵量排名' 
              : '🏆 战绩榜：奖励成绩，按正确率和掌握度排名'}
          </p>
        </div>
        
        {/* 自己的排名 */}
        {selfRank && (
          <Card className="p-4 bg-gradient-to-r from-primary/5 to-amber-50 border-primary/20">
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 rounded-full bg-primary/10 flex items-center justify-center">
                <span className="text-lg font-bold text-primary">{selfRank.rank}</span>
              </div>
              <div className="flex-1">
                <p className="font-medium">我的排名</p>
                <p className="text-xs text-muted-foreground">
                  {activeTab === 'power' 
                    ? `坚持${currentUser?.streakDays}天 · 背诵${selfRank.value}篇` 
                    : `正确率${selfRank.correctRate}% · 掌握${selfRank.value}个知识点`}
                </p>
              </div>
              <ChevronRight className="w-5 h-5 text-muted-foreground" />
            </div>
          </Card>
        )}
        
        {/* 排行榜列表 */}
        <Card className="overflow-hidden">
          <div className="divide-y">
            {rankingData.map((item, index) => (
              <div
                key={item.userId}
                className={cn(
                  'p-4 flex items-center gap-3',
                  getRankStyle(item.rank),
                  item.isSelf && 'ring-1 ring-primary/30'
                )}
              >
                {/* 排名 */}
                <div className="w-8 h-8 flex items-center justify-center">
                  {getRankIcon(item.rank)}
                </div>
                
                {/* 头像 */}
                <div className="w-10 h-10 rounded-full bg-primary/10 flex items-center justify-center text-sm font-medium">
                  {item.userName.charAt(0)}
                </div>
                
                {/* 信息 */}
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2">
                    <span className="font-medium">{item.userName}</span>
                    {item.isSelf && (
                      <span className="text-[10px] px-1.5 py-0.5 bg-primary/10 text-primary rounded">
                        我
                      </span>
                    )}
                  </div>
                  <div className="flex items-center gap-2 text-xs text-muted-foreground mt-0.5">
                    {activeTab === 'power' ? (
                      <>
                        <Flame className="w-3 h-3 text-orange-500" />
                        <span>连续{item.streakDays}天</span>
                      </>
                    ) : (
                      <>
                        <Target className="w-3 h-3 text-primary" />
                        <span>正确率{item.correctRate}%</span>
                      </>
                    )}
                  </div>
                </div>
                
                {/* 数值 */}
                <div className="text-right">
                  <p className="font-bold text-lg">
                    {activeTab === 'power' ? (
                      <span className="flex items-center gap-1">
                        <Star className="w-4 h-4 text-amber-500 fill-amber-500" />
                        {item.value}
                      </span>
                    ) : (
                      <span className="text-primary">{item.value}</span>
                    )}
                  </p>
                  <p className="text-xs text-muted-foreground flex items-center gap-1 justify-end">
                    {activeTab === 'power' ? '篇' : '分'}
                    {item.trend === 'up' && <TrendingUp className="w-3 h-3 text-green-500" />}
                    {item.trend === 'down' && <TrendingDown className="w-3 h-3 text-red-500" />}
                    {item.trend === 'same' && <Minus className="w-3 h-3 text-gray-400" />}
                  </p>
                </div>
              </div>
            ))}
          </div>
        </Card>
        
        {/* 学习搭子 */}
        <StudyBuddySection
          buddies={MOCK_BUDDIES}
          onInvite={() => console.log('invite buddy')}
        />

        {/* 激励文案 */}
        <div className="text-center py-4">
          <p className="text-sm text-muted-foreground">
            {activeTab === 'power'
              ? '💪 坚持就是胜利，每天进步一点点！'
              : '🎯 精益求精，追求更高的正确率！'}
          </p>
        </div>
      </div>
      
      {/* 底部导航 */}
      <BottomNav />
    </div>
  );
}

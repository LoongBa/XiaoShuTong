import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { useEffect, useState } from 'react';
import { useAppStore } from '@/store/appStore';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { BottomNav } from '@/components/BottomNav';
import { EmptyState } from '@/components/EmptyState';
import {
  ArrowLeft,
  Calendar,
  TrendingUp,
  Target,
  ChevronDown,
  BookOpen,
  Star,
  Flame,
  Share2
} from 'lucide-react';
import { cn } from '@/lib/utils';
import { ShareReportModal } from '@/components/ShareReportModal';

const TIME_RANGE_OPTIONS = [
  { id: 'week', label: '本周', days: 7 },
  { id: 'month', label: '本月', days: 30 },
  { id: 'quarter', label: '本季度', days: 90 },
  { id: 'year', label: '今年', days: 365 },
  { id: 'all', label: '学习天数', days: 0 },
];

// 模拟掌握情况数据
const MOCK_MASTERY_DATA = {
  week: { total: 12, gray: 2, yellow: 3, green: 5, gold: 2 },
  month: { total: 45, gray: 8, yellow: 12, green: 18, gold: 7 },
  quarter: { total: 128, gray: 25, yellow: 35, green: 48, gold: 20 },
  year: { total: 356, gray: 68, yellow: 95, green: 128, gold: 65 },
  all: { total: 520, gray: 98, yellow: 145, green: 198, gold: 79 },
};

// 模拟周期表现数据
const MOCK_PERIOD_DATA = {
  week: [
    { label: '周一', gray: 1, yellow: 1, green: 2, gold: 0 },
    { label: '周二', gray: 0, yellow: 1, green: 1, gold: 1 },
    { label: '周三', gray: 0, yellow: 0, green: 2, gold: 1 },
    { label: '周四', gray: 1, yellow: 0, green: 1, gold: 1 },
    { label: '周五', gray: 0, yellow: 1, green: 1, gold: 0 },
    { label: '周六', gray: 0, yellow: 0, green: 1, gold: 0 },
    { label: '周日', gray: 0, yellow: 0, green: 0, gold: 0 },
  ],
  month: [
    { label: '第1周', gray: 2, yellow: 3, green: 4, gold: 1 },
    { label: '第2周', gray: 1, yellow: 2, green: 5, gold: 2 },
    { label: '第3周', gray: 2, yellow: 3, green: 3, gold: 1 },
    { label: '第4周', gray: 3, yellow: 4, green: 6, gold: 3 },
  ],
  quarter: [
    { label: '第1月', gray: 8, yellow: 12, green: 15, gold: 5 },
    { label: '第2月', gray: 6, yellow: 10, green: 18, gold: 8 },
    { label: '第3月', gray: 11, yellow: 13, green: 15, gold: 7 },
  ],
  year: [
    { label: '1月', gray: 5, yellow: 8, green: 10, gold: 3 },
    { label: '2月', gray: 6, yellow: 9, green: 12, gold: 4 },
    { label: '3月', gray: 5, yellow: 7, green: 11, gold: 5 },
    { label: '4月', gray: 7, yellow: 10, green: 13, gold: 6 },
    { label: '5月', gray: 6, yellow: 8, green: 12, gold: 5 },
    { label: '6月', gray: 8, yellow: 11, green: 14, gold: 7 },
  ],
  all: [
    { label: '2024上', gray: 35, yellow: 48, green: 62, gold: 28 },
    { label: '2024下', gray: 33, yellow: 47, green: 66, gold: 31 },
    { label: '2025上', gray: 30, yellow: 50, green: 70, gold: 20 },
  ],
};

export const Route = createFileRoute('/student/stats/heatmap')({
  component: HeatmapStatsPage,
});

function HeatmapStatsPage() {
  const navigate = useNavigate();
  const { isLoggedIn, learningStats, currentUser } = useAppStore();
  const [timeRange, setTimeRange] = useState('week');
  const [showRangeSelector, setShowRangeSelector] = useState(false);
  const [showShareModal, setShowShareModal] = useState(false);
  
  // 检查登录状态
  useEffect(() => {
    if (!isLoggedIn) {
      navigate({ to: '/auth/login' });
    }
  }, [isLoggedIn, navigate]);
  
  const currentRange = TIME_RANGE_OPTIONS.find(r => r.id === timeRange) || TIME_RANGE_OPTIONS[0];
  const masteryData = MOCK_MASTERY_DATA[timeRange as keyof typeof MOCK_MASTERY_DATA];
  const periodData = MOCK_PERIOD_DATA[timeRange as keyof typeof MOCK_PERIOD_DATA] || MOCK_PERIOD_DATA.week;
  
  // 根据时间范围计算统计数据
  const getStatsByRange = () => {
    const days = currentRange.days || learningStats.length;
    const filteredStats = learningStats.slice(-days);
    const totalDays = filteredStats.filter(s => s.count > 0).length;
    const totalQuestions = filteredStats.reduce((sum, s) => sum + s.count, 0);
    const totalCorrect = filteredStats.reduce((sum, s) => sum + s.correctCount, 0);
    const accuracy = totalQuestions > 0 ? Math.round((totalCorrect / totalQuestions) * 100) : 0;
    return { totalDays, totalQuestions, accuracy };
  };
  
  const stats = getStatsByRange();
  
  const getMasteryColor = (type: string) => {
    switch (type) {
      case 'gray': return 'bg-gray-400';
      case 'yellow': return 'bg-orange-400';
      case 'green': return 'bg-green-500';
      case 'gold': return 'bg-yellow-400';
      default: return 'bg-gray-200';
    }
  };

  const getMasteryBgColor = (type: string) => {
    switch (type) {
      case 'gray': return 'bg-gray-100';
      case 'yellow': return 'bg-orange-100';
      case 'green': return 'bg-green-100';
      case 'gold': return 'bg-yellow-100';
      default: return 'bg-gray-50';
    }
  };

  const getMasteryTextColor = (type: string) => {
    switch (type) {
      case 'gray': return 'text-gray-700';
      case 'yellow': return 'text-orange-800';
      case 'green': return 'text-green-800';
      case 'gold': return 'text-yellow-800';
      default: return 'text-gray-600';
    }
  };

  const getMasteryNumberColor = (type: string) => {
    switch (type) {
      case 'gray': return 'text-gray-800';
      case 'yellow': return 'text-orange-900';
      case 'green': return 'text-green-900';
      case 'gold': return 'text-yellow-900';
      default: return 'text-gray-700';
    }
  };
  
  const getMasteryLabel = (type: string) => {
    switch (type) {
      case 'gray': return '再背背';
      case 'yellow': return '快熟了';
      case 'green': return '很棒';
      case 'gold': return '点亮了';
      default: return '';
    }
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
          <h1 className="font-semibold">学习统计</h1>
        </div>
      </header>
      
      <div className="p-4 space-y-4">
        {/* 时间范围选择 */}
        <div className="flex items-center justify-between">
          <button
            onClick={() => setShowRangeSelector(!showRangeSelector)}
            className="flex items-center gap-2 px-4 py-2 bg-muted rounded-lg text-sm font-medium"
          >
            <Calendar className="w-4 h-4" />
            {currentRange.label}
            <ChevronDown className="w-4 h-4" />
          </button>
        </div>
        
        {/* 时间范围选项 */}
        {showRangeSelector && (
          <Card className="p-2">
            <div className="grid grid-cols-3 gap-2">
              {TIME_RANGE_OPTIONS.map((option) => (
                <button
                  key={option.id}
                  onClick={() => {
                    setTimeRange(option.id);
                    setShowRangeSelector(false);
                  }}
                  className={cn(
                    'py-2 px-3 rounded-lg text-sm font-medium transition-colors',
                    timeRange === option.id
                      ? 'bg-primary text-primary-foreground'
                      : 'bg-muted text-muted-foreground hover:bg-muted/80'
                  )}
                >
                  {option.label}
                </button>
              ))}
            </div>
          </Card>
        )}
        
        {/* 统计卡片 */}
        <div className="grid grid-cols-3 gap-3">
          <Card className="p-4 text-center">
            <div className="w-8 h-8 rounded-full bg-primary/10 flex items-center justify-center mx-auto mb-2">
              <Calendar className="w-4 h-4 text-primary" />
            </div>
            <p className="text-2xl font-bold">{stats.totalDays}</p>
            <p className="text-xs text-muted-foreground">学习天数</p>
          </Card>
          
          <Card className="p-4 text-center">
            <div className="w-8 h-8 rounded-full bg-green-100 flex items-center justify-center mx-auto mb-2">
              <Target className="w-4 h-4 text-green-600" />
            </div>
            <p className="text-2xl font-bold">{stats.totalQuestions}</p>
            <p className="text-xs text-muted-foreground">背诵题数</p>
          </Card>
          
          <Card className="p-4 text-center">
            <div className="w-8 h-8 rounded-full bg-amber-100 flex items-center justify-center mx-auto mb-2">
              <TrendingUp className="w-4 h-4 text-amber-600" />
            </div>
            <p className="text-2xl font-bold">{stats.accuracy}%</p>
            <p className="text-xs text-muted-foreground">正确率</p>
          </Card>
        </div>
        
        {/* 掌握情况 */}
        <Card className="p-4">
          <h2 className="font-semibold mb-4 flex items-center gap-2">
            <BookOpen className="w-4 h-4" />
            掌握情况
          </h2>
          <p className="text-sm text-muted-foreground mb-3">
            本周期内新增 {masteryData.total} 条知识点
          </p>
          
          {/* 总进度条 */}
          <div className="h-4 bg-gray-100 rounded-full overflow-hidden flex mb-3">
            <div className={`h-full ${getMasteryColor('gray')}`} style={{ width: `${(masteryData.gray / masteryData.total) * 100}%` }} />
            <div className={`h-full ${getMasteryColor('yellow')}`} style={{ width: `${(masteryData.yellow / masteryData.total) * 100}%` }} />
            <div className={`h-full ${getMasteryColor('green')}`} style={{ width: `${(masteryData.green / masteryData.total) * 100}%` }} />
            <div className={`h-full ${getMasteryColor('gold')}`} style={{ width: `${(masteryData.gold / masteryData.total) * 100}%` }} />
          </div>
          
          {/* 各熟练度统计 */}
          <div className="grid grid-cols-2 gap-2">
            {(['gray', 'yellow', 'green', 'gold'] as const).map((type) => (
              <div key={type} className={`flex items-center gap-2 p-2 ${getMasteryBgColor(type)} rounded-lg`}>
                <div className={`w-3 h-3 rounded-full ${getMasteryColor(type)}`} />
                <span className={`text-sm flex-1 ${getMasteryTextColor(type)}`}>{getMasteryLabel(type)}</span>
                <span className={`text-sm font-semibold ${getMasteryNumberColor(type)}`}>+{masteryData[type]}</span>
              </div>
            ))}
          </div>
        </Card>
        
        {/* 本周期表现 */}
        <Card className="p-4">
          <h2 className="font-semibold mb-4 flex items-center gap-2">
            <Flame className="w-4 h-4" />
            本周期表现
          </h2>
          <div className="space-y-3">
            {periodData.slice(0, 6).map((item, index) => {
              const total = item.gray + item.yellow + item.green + item.gold;
              return (
                <div key={index} className="flex items-center gap-3">
                  <span className="text-sm text-muted-foreground w-12">{item.label}</span>
                  <div className="flex-1 h-6 bg-gray-100 rounded-lg overflow-hidden flex">
                    {item.gray > 0 && (
                      <div 
                        className="h-full bg-gray-400" 
                        style={{ width: `${(item.gray / total) * 100}%` }}
                      />
                    )}
                    {item.yellow > 0 && (
                      <div 
                        className="h-full bg-amber-400" 
                        style={{ width: `${(item.yellow / total) * 100}%` }}
                      />
                    )}
                    {item.green > 0 && (
                      <div 
                        className="h-full bg-green-500" 
                        style={{ width: `${(item.green / total) * 100}%` }}
                      />
                    )}
                    {item.gold > 0 && (
                      <div 
                        className="h-full bg-amber-500" 
                        style={{ width: `${(item.gold / total) * 100}%` }}
                      />
                    )}
                  </div>
                  <span className="text-sm font-medium w-8 text-right">{total}</span>
                </div>
              );
            })}
          </div>
          
          {/* 图例 */}
          <div className="flex items-center gap-3 mt-4 pt-4 border-t">
            {(['gray', 'yellow', 'green', 'gold'] as const).map((type) => (
              <div key={type} className="flex items-center gap-1">
                <div className={`w-3 h-3 rounded-full ${getMasteryColor(type)}`} />
                <span className="text-xs text-muted-foreground">{getMasteryLabel(type)}</span>
              </div>
            ))}
          </div>
        </Card>
      </div>
      
      {/* 分享战报按钮 */}
      <div className="px-4 pb-4">
        <Button
          onClick={() => setShowShareModal(true)}
          className="w-full flex items-center justify-center gap-2"
        >
          <Share2 className="w-4 h-4" />
          分享战报
        </Button>
      </div>

      {/* 底部导航 */}
      <BottomNav />

      {/* 分享战报弹窗 */}
      <ShareReportModal
        isOpen={showShareModal}
        onClose={() => setShowShareModal(false)}
        stats={{
          totalDays: stats.totalDays,
          totalQuestions: stats.totalQuestions,
          accuracy: stats.accuracy,
          masteryData
        }}
        userName={currentUser?.name || '我'}
      />
    </div>
  );
}

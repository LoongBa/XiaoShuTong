import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import type { User, Task, ReviewItem, KnowledgePoint, Subject, WrongAnswer, LearningStats } from '@/types';

interface AppState {
  // 用户状态
  currentUser: User | null;
  isLoggedIn: boolean;
  hasAgreedPrivacy: boolean;
  
  // 数据
  tasks: Task[];
  reviewItems: ReviewItem[];
  subjects: Subject[];
  knowledgePoints: KnowledgePoint[];
  wrongAnswers: WrongAnswer[];
  learningStats: LearningStats[];
  
  // 当前学习会话
  currentTaskId: string | null;
  currentQuestionIndex: number;
  answers: Record<string, { answer: string; isCorrect: boolean; usedHint: boolean }>;
  
  // Actions
  setCurrentUser: (user: User | null) => void;
  login: (user: User) => void;
  logout: () => void;
  agreePrivacy: () => void;
  
  setTasks: (tasks: Task[]) => void;
  setReviewItems: (items: ReviewItem[]) => void;
  setSubjects: (subjects: Subject[]) => void;
  setKnowledgePoints: (points: KnowledgePoint[]) => void;
  setWrongAnswers: (wrongs: WrongAnswer[]) => void;
  setLearningStats: (stats: LearningStats[]) => void;
  
  startTask: (taskId: string) => void;
  submitAnswer: (questionId: string, answer: string, isCorrect: boolean, usedHint: boolean) => void;
  nextQuestion: () => void;
  completeTask: () => void;
  
  // 模拟数据初始化
  initMockData: () => void;
}

export const useAppStore = create<AppState>()(
  persist(
    (set, get) => ({
      // 初始状态
      currentUser: null,
      isLoggedIn: false,
      hasAgreedPrivacy: false,
      
      tasks: [],
      reviewItems: [],
      subjects: [],
      knowledgePoints: [],
      wrongAnswers: [],
      learningStats: [],
      
      currentTaskId: null,
      currentQuestionIndex: 0,
      answers: {},
      
      // Actions
      setCurrentUser: (user) => set({ currentUser: user }),
      
      login: (user) => set({ 
        currentUser: user, 
        isLoggedIn: true 
      }),
      
      logout: () => set({ 
        currentUser: null, 
        isLoggedIn: false,
        currentTaskId: null,
        currentQuestionIndex: 0,
        answers: {}
      }),
      
      agreePrivacy: () => set({ hasAgreedPrivacy: true }),
      
      setTasks: (tasks) => set({ tasks }),
      setReviewItems: (items) => set({ reviewItems: items }),
      setSubjects: (subjects) => set({ subjects }),
      setKnowledgePoints: (points) => set({ knowledgePoints: points }),
      setWrongAnswers: (wrongs) => set({ wrongAnswers: wrongs }),
      setLearningStats: (stats) => set({ learningStats: stats }),
      
      startTask: (taskId) => set({
        currentTaskId: taskId,
        currentQuestionIndex: 0,
        answers: {}
      }),
      
      submitAnswer: (questionId, answer, isCorrect, usedHint) => set((state) => ({
        answers: {
          ...state.answers,
          [questionId]: { answer, isCorrect, usedHint }
        }
      })),
      
      nextQuestion: () => set((state) => ({
        currentQuestionIndex: state.currentQuestionIndex + 1
      })),
      
      completeTask: () => {
        const { currentTaskId, tasks, currentUser } = get();
        if (!currentTaskId || !currentUser) return;
        
        // 更新任务完成状态
        const updatedTasks = tasks.map(t => 
          t.id === currentTaskId 
            ? { ...t, status: 'completed' as const, completedQuestions: t.totalQuestions }
            : t
        );
        
        // 更新用户连续打卡
        const updatedUser = {
          ...currentUser,
          streakDays: currentUser.streakDays + 1
        };
        
        set({
          tasks: updatedTasks,
          currentUser: updatedUser,
          currentTaskId: null,
          currentQuestionIndex: 0,
          answers: {}
        });
      },
      
      // 模拟数据初始化
      initMockData: () => {
        const mockTasks: Task[] = [
          {
            id: 'task-1',
            title: '背诵《人文地理·第一章》',
            description: '地理基础知识背诵',
            teacherName: '王老师',
            teacherId: 'teacher-1',
            classId: 'class-1',
            totalQuestions: 10,
            completedQuestions: 4,
            deadline: new Date(Date.now() + 24 * 60 * 60 * 1000).toISOString(),
            status: 'in_progress',
            createdAt: new Date().toISOString(),
            source: 'teacher'
          },
          {
            id: 'task-2',
            title: '背诵《蜀道难》',
            description: '李白名篇',
            teacherName: '李老师',
            teacherId: 'teacher-2',
            classId: 'class-1',
            totalQuestions: 8,
            completedQuestions: 0,
            deadline: new Date(Date.now() + 2 * 24 * 60 * 60 * 1000).toISOString(),
            status: 'pending',
            createdAt: new Date().toISOString(),
            source: 'teacher'
          },
          {
            id: 'task-3',
            title: '每日古诗打卡',
            description: '自主背诵',
            teacherName: '我',
            teacherId: 'self',
            classId: '',
            totalQuestions: 5,
            completedQuestions: 2,
            deadline: new Date(Date.now() + 12 * 60 * 60 * 1000).toISOString(),
            status: 'in_progress',
            createdAt: new Date().toISOString(),
            source: 'self'
          }
        ];
        
        const mockReviewItems: ReviewItem[] = [
          {
            id: 'review-1',
            knowledgePointId: 'kp-1',
            title: '黄鹤楼',
            memoryState: 'yellow',
            daysSinceLastReview: 5,
            isOverdue: false
          },
          {
            id: 'review-2',
            knowledgePointId: 'kp-2',
            title: '大运河',
            memoryState: 'green',
            daysSinceLastReview: 3,
            isOverdue: false
          },
          {
            id: 'review-3',
            knowledgePointId: 'kp-3',
            title: '滕王阁序·落霞句',
            memoryState: 'gray',
            daysSinceLastReview: 8,
            isOverdue: true
          }
        ];
        
        const mockSubjects: Subject[] = [
          { id: 'sub-1', name: '语文', icon: '📚', isHot: true, isLocked: false, knowledgeCount: 156 },
          { id: 'sub-2', name: '趣味', icon: '🎮', isHot: true, isLocked: false, knowledgeCount: 89 },
          { id: 'sub-3', name: '历史', icon: '🏛️', isHot: false, isLocked: true, knowledgeCount: 234 },
          { id: 'sub-4', name: '地理', icon: '🌍', isHot: false, isLocked: false, knowledgeCount: 178 },
          { id: 'sub-5', name: '生物', icon: '🧬', isHot: false, isLocked: true, knowledgeCount: 145 },
        ];
        
        const mockKnowledgePoints: KnowledgePoint[] = [
          {
            id: 'kp-1',
            title: '黄鹤楼',
            content: '昔人已乘黄鹤去，此地空余黄鹤楼。黄鹤一去不复返，白云千载空悠悠。',
            subject: '语文',
            memoryState: 'yellow',
            lastReviewedAt: new Date(Date.now() - 5 * 24 * 60 * 60 * 1000).toISOString(),
            reviewCount: 3
          },
          {
            id: 'kp-2',
            title: '大运河',
            content: '京杭大运河是世界上里程最长、工程最大的古代运河。',
            subject: '历史',
            memoryState: 'green',
            lastReviewedAt: new Date(Date.now() - 3 * 24 * 60 * 60 * 1000).toISOString(),
            reviewCount: 5
          }
        ];
        
        const mockWrongAnswers: WrongAnswer[] = [
          {
            id: 'wrong-1',
            questionId: 'q-1',
            knowledgePointId: 'kp-1',
            title: '黄鹤楼·首句',
            wrongAnswer: '昔人已乘白云去',
            correctAnswer: '昔人已乘黄鹤去',
            wrongCount: 2,
            isMastered: false,
            createdAt: new Date(Date.now() - 7 * 24 * 60 * 60 * 1000).toISOString()
          }
        ];
        
        // 生成最近30天的学习统计
        const mockStats: LearningStats[] = Array.from({ length: 30 }, (_, i) => {
          const date = new Date();
          date.setDate(date.getDate() - (29 - i));
          const count = Math.floor(Math.random() * 15) + 1;
          const correctCount = Math.floor(count * (0.6 + Math.random() * 0.3));
          return {
            date: date.toISOString().split('T')[0],
            count,
            correctCount
          };
        });
        
        set({
          tasks: mockTasks,
          reviewItems: mockReviewItems,
          subjects: mockSubjects,
          knowledgePoints: mockKnowledgePoints,
          wrongAnswers: mockWrongAnswers,
          learningStats: mockStats
        });
      }
    }),
    {
      name: 'beishu-app-storage',
      partialize: (state) => ({
        currentUser: state.currentUser,
        isLoggedIn: state.isLoggedIn,
        hasAgreedPrivacy: state.hasAgreedPrivacy,
        tasks: state.tasks,
        reviewItems: state.reviewItems,
        wrongAnswers: state.wrongAnswers,
        learningStats: state.learningStats
      })
    }
  )
);

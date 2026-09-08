// 小书童-背书伴侣类型定义

// 用户角色
export type UserRole = 'student' | 'teacher' | 'parent';

// 记忆状态
export type MemoryState = 'gray' | 'yellow' | 'green' | 'gold';

// 用户
export interface User {
  id: string;
  name: string;
  avatar?: string;
  role: UserRole;
  streakDays: number;
  classId?: string;
}

// 任务
export interface Task {
  id: string;
  title: string;
  description?: string;
  teacherName: string;
  teacherId: string;
  classId: string;
  totalQuestions: number;
  completedQuestions: number;
  deadline: string;
  status: 'pending' | 'in_progress' | 'completed' | 'expired';
  createdAt: string;
  source: 'teacher' | 'self'; // 任务来源：老师发布或自建
}

// 知识点
export interface KnowledgePoint {
  id: string;
  title: string;
  content: string;
  subject: string;
  memoryState: MemoryState;
  lastReviewedAt?: string;
  nextReviewAt?: string;
  reviewCount: number;
}

// 题目
export interface Question {
  id: string;
  taskId: string;
  type: 'R1' | 'R2' | 'R3' | 'O1' | 'O2';
  content: string;
  hint?: string;
  answer: string;
  order: number;
}

// 答题记录
export interface AnswerRecord {
  id: string;
  questionId: string;
  userId: string;
  taskId: string;
  userAnswer: string;
  isCorrect: boolean;
  usedHint: boolean;
  answeredAt: string;
}

// 复习项
export interface ReviewItem {
  id: string;
  knowledgePointId: string;
  title: string;
  memoryState: MemoryState;
  daysSinceLastReview: number;
  isOverdue: boolean;
}

// 学科
export interface Subject {
  id: string;
  name: string;
  icon: string;
  isHot: boolean;
  isLocked: boolean;
  knowledgeCount: number;
}

// 班级
export interface Class {
  id: string;
  name: string;
  subject: string;
  grade: string;
  studentCount: number;
  teacherId: string;
  inviteCode: string;
}

// 学生进度
export interface StudentProgress {
  userId: string;
  userName: string;
  avatar?: string;
  streakDays: number;
  todayTaskCompleted: boolean;
  completionRate: number;
  starCount: number;
}

// 错题
export interface WrongAnswer {
  id: string;
  questionId: string;
  knowledgePointId: string;
  title: string;
  wrongAnswer: string;
  correctAnswer: string;
  wrongCount: number;
  isMastered: boolean;
  createdAt: string;
}

// 学习统计
export interface LearningStats {
  date: string;
  count: number;
  correctCount: number;
}

// 家长报告
export interface ParentReport {
  childId: string;
  childName: string;
  todayTaskCompleted: boolean;
  streakDays: number;
  weeklyProgress: number;
  weeklyTotal: number;
  subjectMastery: {
    subject: string;
    state: MemoryState;
  }[];
}

// 记忆状态配置
export const MEMORY_STATE_CONFIG: Record<MemoryState, { label: string; helper: string; color: string }> = {
  gray: { label: '再背背', helper: '未掌握', color: 'memory-gray' },
  yellow: { label: '快熟了', helper: '需巩固', color: 'memory-yellow' },
  green: { label: '很棒', helper: '已掌握', color: 'memory-green' },
  gold: { label: '点亮了', helper: '熟练', color: 'memory-gold' },
};

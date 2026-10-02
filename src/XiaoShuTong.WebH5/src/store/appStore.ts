import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import type { User, Task, ReviewItem, KnowledgePoint, Subject, LearningStats } from '@/types';
import { Tkwf } from '@tkwf/tsclient';
import { executeQuery } from '@/lib/sdk-bypass';
import type {
  SessionQuestion_ExecuteService,
  GetNextQuestionResDto,
  SubmitAttemptResDto,
  CreateStudySessionResDto,
} from '@/gql/ts-client.g';

// 服务端判题结果记录（替换本地 { answer, isCorrect, usedHint } 推导）
export interface ServerAnswerRecord {
  result: string;
  preState: string;
  postState: string;
  needsGuidance: boolean;
  showAnswer: boolean;
  attemptCount: number;
  matchedKeywords: string[];
  missingKeywords: string[];
}

// startSession 入参（新学 / 复习两分支）
export interface StartSessionOptions {
  /** 会话场景：新学=Memorize、复习=Assess（BR-01 场景区分） */
  scenario: string;
  /** 会话类型：本迭代固定 Progressive */
  sessionType: string;
  /** 关联任务 ID（可空——自由/复习会话无任务） */
  taskId: number | null;
  /** 期望题数 */
  questionCount: number;
  /** 任务题集白名单（任务会话首次取题透传；与错题重练 wrongPracticeQuestionIds 互斥，任务场景优先） */
  questionIds?: string[] | null;
}

// startSession 结果：sessionUid 活源 + 第 1 题（题集耗尽时 question.questionId 为空）
export interface StartSessionResult {
  sessionUid: string;
  question: GetNextQuestionResDto;
}

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
  learningStats: LearningStats[];
  
  // 当前学习会话（服务端驱动；不进 persist partialize，会话恢复靠 createStudySession BR-05 幂等）
  currentTaskId: string | null;
  sessionUid: string | null;
  questionCount: number;
  currentQuestionIndex: number;
  answers: Record<string, ServerAnswerRecord>;
  /** 错题重练白名单（V0.6.14 学习-BR-51；wrong.tsx 设置 → task.tsx sessionQuestion 消费，会话后清空；不进 persist） */
  wrongPracticeQuestionIds: string[] | null;
  /** 错题重练来源题库（P2-3 修复：与 wrongPracticeQuestionIds 配对——task.tsx 复习路径用真实 bankId，替代 DEFAULT_BANK_ID 假兜底致 BankNotFound；wrong.tsx 携带错题项 bankId，会话后清空；不进 persist） */
  wrongPracticeBankId: string | null;
  
  // Actions
  setCurrentUser: (user: User | null) => void;
  login: (user: User) => void;
  logout: () => void;
  agreePrivacy: () => void;
  
  setTasks: (tasks: Task[]) => void;
  setReviewItems: (items: ReviewItem[]) => void;
  setSubjects: (subjects: Subject[]) => void;
  setKnowledgePoints: (points: KnowledgePoint[]) => void;
  setLearningStats: (stats: LearningStats[]) => void;
  
  startTask: (taskId: string) => void;
  /** 会话初始化归口：createStudySession → 存 sessionUid + 取第 1 题（T5 消费） */
  startSession: (bankId: string, opts: StartSessionOptions) => Promise<StartSessionResult>;
  /** 记录服务端判题结果（T6 消费，答案记录扩展为服务端字段） */
  recordAnswer: (questionId: string, res: SubmitAttemptResDto) => void;
  nextQuestion: () => void;
  completeTask: () => void;
  /** 设置错题重练白名单 + 来源题库（V0.6.14 + P2-3；wrong.tsx 重练入口写入，bankId 可选兼容旧调用） */
  setWrongPracticeQuestionIds: (ids: string[] | null, bankId?: string | null) => void;
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
      learningStats: [],
      
      currentTaskId: null,
      sessionUid: null,
      questionCount: 0,
      currentQuestionIndex: 0,
      answers: {},
      wrongPracticeQuestionIds: null,
      wrongPracticeBankId: null,
      
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
        sessionUid: null,
        questionCount: 0,
        currentQuestionIndex: 0,
        answers: {},
        wrongPracticeQuestionIds: null,
        wrongPracticeBankId: null,
      }),
      
      agreePrivacy: () => set({ hasAgreedPrivacy: true }),
      
      setTasks: (tasks) => set({ tasks }),
      setReviewItems: (items) => set({ reviewItems: items }),
      setSubjects: (subjects) => set({ subjects }),
      setKnowledgePoints: (points) => set({ knowledgePoints: points }),
      setLearningStats: (stats) => set({ learningStats: stats }),
      
      startTask: (taskId) => set({
        currentTaskId: taskId,
        currentQuestionIndex: 0,
        answers: {}
      }),

      setWrongPracticeQuestionIds: (ids, bankId) => set({
        wrongPracticeQuestionIds: ids,
        wrongPracticeBankId: ids === null ? null : (bankId ?? null),
      }),
      
      // 会话初始化归口：createStudySession_Execute → sessionUid 活源 → 首次 getSessionQuestion 取第 1 题
      // V0.7.7（G16）：SDK 前缀启发式把 create* 判 mutation 导致 400 → 走 executeQuery 强制 query（ts-client.g.ts 已标 type:'query'）
      startSession: async (bankId, opts) => {
        const res = await executeQuery<CreateStudySessionResDto>('createStudySession_Execute', {
          request: {
            scenario: opts.scenario,
            bankId,
            sessionType: opts.sessionType,
            taskId: opts.taskId ?? null,
            questionCount: opts.questionCount,
          },
        });
        
        // 域级失败（BankNotFound / Forbidden / SessionError 等）——抛错由调用方进入错误态兜底
        if (!res.success) {
          throw new Error(res.errorCode || '会话创建失败');
        }
        
        const sessionUid = res.sessionUid;
        // 会话三件套（sessionUid/questionCount/answers）——均不进 persist partialize
        set({
          sessionUid,
          questionCount: res.questionCount,
          currentQuestionIndex: 0,
          answers: {}
        });
        
        // 首次取第 1 题（BR-18 题集耗尽时 questionId 为空，由调用方导航结果页）
        // V0.6.14（学习-BR-51）：错题重练白名单透传（wrong.tsx 设置 → 首次取题携带）；
        // 白名单生命周期 = 重练会话全程（后续取题 task.tsx fetchNextQuestion 从 store 读；endStudy 时清空）
        // V0.7.8：任务题集白名单优先（task.tsx initSession 传入 → 任务会话全程出题范围约束；与错题重练互斥）
        const practiceIds = get().wrongPracticeQuestionIds;
        const whiteList = opts.questionIds && opts.questionIds.length > 0 ? opts.questionIds : practiceIds;
        const question = await Tkwf.User.Use<SessionQuestion_ExecuteService>().sessionQuestion_Execute({
          request: { sessionUid, bankId, type: null, knowledgePoint: null, questionIds: whiteList },
        });
        
        return { sessionUid, question };
      },
      
      // 答案记录扩展为服务端判题结果字段（本地 isCorrect/usedHint 推导废弃）
      recordAnswer: (questionId, res) => set((state) => ({
        answers: {
          ...state.answers,
          [questionId]: {
            result: res.result,
            preState: res.preState,
            postState: res.postState,
            needsGuidance: res.needsGuidance,
            showAnswer: res.showAnswer,
            attemptCount: res.attemptCount,
            matchedKeywords: res.matchedKeywords,
            missingKeywords: res.missingKeywords,
          },
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
          sessionUid: null,
          questionCount: 0,
          currentQuestionIndex: 0,
          answers: {}
        });
      },
    }),
    {
      name: 'beishu-app-storage',
      partialize: (state) => ({
        currentUser: state.currentUser,
        isLoggedIn: state.isLoggedIn,
        hasAgreedPrivacy: state.hasAgreedPrivacy,
        tasks: state.tasks,
        reviewItems: state.reviewItems,
        learningStats: state.learningStats
      })
    }
  )
);

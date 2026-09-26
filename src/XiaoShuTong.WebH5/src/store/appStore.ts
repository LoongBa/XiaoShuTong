import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import type { User, Task, ReviewItem, KnowledgePoint, Subject, WrongAnswer, LearningStats } from '@/types';
import { Tkwf } from '@tkwf/tsclient';
import type {
  StudySession_ExecuteService,
  SessionQuestion_ExecuteService,
  GetNextQuestionResDto,
  SubmitAttemptResDto,
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
  wrongAnswers: WrongAnswer[];
  learningStats: LearningStats[];
  
  // 当前学习会话（服务端驱动；不进 persist partialize，会话恢复靠 createStudySession BR-05 幂等）
  currentTaskId: string | null;
  sessionUid: string | null;
  questionCount: number;
  currentQuestionIndex: number;
  answers: Record<string, ServerAnswerRecord>;
  
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
  /** 会话初始化归口：createStudySession → 存 sessionUid + 取第 1 题（T5 消费） */
  startSession: (bankId: string, opts: StartSessionOptions) => Promise<StartSessionResult>;
  /** 记录服务端判题结果（T6 消费，答案记录扩展为服务端字段） */
  recordAnswer: (questionId: string, res: SubmitAttemptResDto) => void;
  nextQuestion: () => void;
  completeTask: () => void;
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
      sessionUid: null,
      questionCount: 0,
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
        sessionUid: null,
        questionCount: 0,
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
      
      // 会话初始化归口：createStudySession_Execute → sessionUid 活源 → 首次 getSessionQuestion 取第 1 题
      startSession: async (bankId, opts) => {
        const res = await Tkwf.User.Use<StudySession_ExecuteService>().createStudySession_Execute({
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
        const question = await Tkwf.User.Use<SessionQuestion_ExecuteService>().sessionQuestion_Execute({
          request: { sessionUid, bankId, type: null, knowledgePoint: null },
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
        wrongAnswers: state.wrongAnswers,
        learningStats: state.learningStats
      })
    }
  )
);

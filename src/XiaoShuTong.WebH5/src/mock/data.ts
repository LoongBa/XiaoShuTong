// ── mock 数据层 ──
// 纯数据文件，无 db 实例依赖，无 env，node/browser 双端兼容
// 数据来源：.TKWF/MOCK_SPEC.md §2（ResDto 级策略）
// 由 tkwf-tsclient-mock（注入模式）与 tkwf-tsclient-mock-server（HTTP 模式）共享
// 重跑 gen-mock 后本文件永不覆盖，按 satisfies 报错增改 diff 字段
//
// 填充要求（MOCK_SPEC.md §2）：
// - 所有表名与 src/gql/ts-client.mock.g.ts 的 createMockDb 表名完全对齐（57 表）
// - success=true，errorCode=null（错误态走 scenarioOverrides）
// - 时间字段用相对当前时间 ISO 字符串

import type { DatasetSeed } from "@tkwf/tsclient-mock";

export const initialData: DatasetSeed = {
  // =====================================================================
  // P0：登录链路（scenarioOverrides 已覆盖，保留兜底）
  // =====================================================================

  // ── 登录 ──
  loginPayloads: [
    {
      success: true,
      displayName: "小明",
      userName: "xiaoming",
      sessionKey: "mock-session-xiaoming",
      accessToken: "mock-token",
      refreshToken: "mock-refresh",
      expiresAt: new Date(Date.now() + 86400000).toISOString(),
      deviceId: "web-001",
      extensions: [],
    },
  ],
  challengeResponses: [
    { challengeToken: "mock-challenge-token", salt: "mock-salt", iterations: 600000 },
  ],
  registerResults: [
    { success: true, message: "注册成功" },
  ],

  // =====================================================================
  // P1：群组管理域（Groups）
  // =====================================================================

  // listGroupsResDtos — 2 条，rankEnabled 一 true 一 false
  listGroupsResDtos: [
    {
      success: true, errorCode: null,
      items: [
        { groupId: 1, name: "三年级一班", subject: "数学", grade: "三年级", memberCount: 32, executionRate: 0.85, rankEnabled: true },
        { groupId: 2, name: "课外练习组", subject: "语文", grade: "五年级", memberCount: 15, executionRate: 0.72, rankEnabled: false },
      ],
      total: 2,
    },
  ],

  // groupDetailResDtos — 1 条，含 owner + student 成员
  groupDetailResDtos: [
    {
      success: true, errorCode: null,
      items: [
        { userId: 1001, nickname: "小明", role: "owner", joinedAt: new Date(Date.now() - 2592000000).toISOString(), lastActiveAt: new Date(Date.now() - 3600000).toISOString() },
        { userId: 1002, nickname: "小红", role: "student", joinedAt: new Date(Date.now() - 1728000000).toISOString(), lastActiveAt: new Date(Date.now() - 7200000).toISOString() },
        { userId: 1003, nickname: "小刚", role: "student", joinedAt: new Date(Date.now() - 864000000).toISOString(), lastActiveAt: new Date(Date.now() - 10800000).toISOString() },
      ],
    },
  ],

  // createGroupResDtos — 1 条，groupId 非空
  createGroupResDtos: [
    { success: true, errorCode: null, groupId: 1 },
  ],

  // activateMemberResDtos — 1 条，role=student
  activateMemberResDtos: [
    { success: true, errorCode: null, role: "student" },
  ],

  // importRosterResDtos — 1 条，status=Ready
  importRosterResDtos: [
    { success: true, errorCode: null, importId: "imp-001", status: "Ready", totalRows: 30, readyCount: 28, duplicateCount: 2 },
  ],

  // rosterPreviewResDtos — 1 条，phoneLast4 脱敏
  rosterPreviewResDtos: [
    {
      success: true, errorCode: null,
      items: [
        { row: 1, name: "张三", phoneLast4: "1234", status: "Ready" },
        { row: 2, name: "李四", phoneLast4: "5678", status: "Duplicate" },
      ],
    },
  ],

  // generateInviteCodesResDtos — 1 条，generatedCount>0
  generateInviteCodesResDtos: [
    { success: true, errorCode: null, generatedCount: 10 },
  ],

  // exportRosterCsvResDtos — 1 条，csvFileUrl 非空
  exportRosterCsvResDtos: [
    { success: true, errorCode: null, csvFileUrl: "mock://roster-export.csv", columns: ["姓名", "手机后四位", "邀请码"] },
  ],

  // setRankEnabledResDtos — 1 条
  setRankEnabledResDtos: [
    { success: true, errorCode: null, rankEnabled: true },
  ],

  // removeMemberResDtos — 1 条
  removeMemberResDtos: [
    { success: true, errorCode: null, removed: true },
  ],

  // =====================================================================
  // P1：学习 Session 域（Learning）
  // =====================================================================

  // createStudySessionResDtos — 1 条
  createStudySessionResDtos: [
    { success: true, errorCode: null, sessionUid: "sess-001", questionCount: 20 },
  ],

  // submitAttemptResDtos — 3 条，覆盖 Correct/Partial/Wrong
  submitAttemptResDtos: [
    {
      success: true, errorCode: null,
      result: "Correct",
      preState: "✕", postState: "△",
      isCorrect: true, hintLevel: "None", spentMs: 5000,
      nextReviewAt: new Date(Date.now() + 86400000).toISOString(),
    },
    {
      success: true, errorCode: null,
      result: "Partial",
      preState: "△", postState: "△",
      isCorrect: false, hintLevel: "Partial", spentMs: 8000,
      nextReviewAt: new Date(Date.now() + 43200000).toISOString(),
    },
    {
      success: true, errorCode: null,
      result: "Wrong",
      preState: "○", postState: "✕",
      isCorrect: false, hintLevel: "Full", spentMs: 3000,
      nextReviewAt: new Date(Date.now() + 14400000).toISOString(),
    },
  ],

  // getHintResDtos — 1 条，hint ≤20 字
  getHintResDtos: [
    { success: true, errorCode: null, hint: "注意乘法分配律的应用" },
  ],

  // getReviewQueueResDtos — 2 条，到期复习
  getReviewQueueResDtos: [
    {
      success: true, errorCode: null,
      items: [
        {
          id: 1, uId: "mem-001", userId: 1001, questionId: "q-001", bankId: "bank-001",
          state: "✕", consecutiveCorrect: 0, historyAccuracy: 0.3, easeFactor: 2.5,
          nextReviewAt: new Date(Date.now() - 3600000).toISOString(),
          lastAttemptId: null, lastHintLevel: "None",
          createTime: new Date(Date.now() - 604800000).toISOString(),
          updateTime: new Date(Date.now() - 3600000).toISOString(),
          isFromPersistentSource: false,
        },
      ],
      overdueCount: 1,
    },
  ],

  // getMemoryStatesResDtos — 4 条，覆盖四阶
  getMemoryStatesResDtos: [
    {
      success: true, errorCode: null,
      items: [
        { id: 1, uId: "mem-001", userId: 1001, questionId: "q-001", bankId: "bank-001", state: "✕", consecutiveCorrect: 0, historyAccuracy: 0.3, easeFactor: 2.5, nextReviewAt: new Date(Date.now() - 1800000).toISOString(), lastAttemptId: null, lastHintLevel: "None", createTime: new Date(Date.now() - 604800000).toISOString(), updateTime: new Date(Date.now() - 3600000).toISOString(), isFromPersistentSource: false },
        { id: 2, uId: "mem-002", userId: 1001, questionId: "q-002", bankId: "bank-001", state: "△", consecutiveCorrect: 1, historyAccuracy: 0.5, easeFactor: 2.6, nextReviewAt: new Date(Date.now() + 1800000).toISOString(), lastAttemptId: null, lastHintLevel: "Partial", createTime: new Date(Date.now() - 432000000).toISOString(), updateTime: new Date(Date.now() - 1800000).toISOString(), isFromPersistentSource: false },
        { id: 3, uId: "mem-003", userId: 1001, questionId: "q-003", bankId: "bank-001", state: "○", consecutiveCorrect: 2, historyAccuracy: 0.7, easeFactor: 2.7, nextReviewAt: new Date(Date.now() + 86400000).toISOString(), lastAttemptId: null, lastHintLevel: "None", createTime: new Date(Date.now() - 259200000).toISOString(), updateTime: new Date(Date.now() - 86400000).toISOString(), isFromPersistentSource: false },
        { id: 4, uId: "mem-004", userId: 1001, questionId: "q-004", bankId: "bank-001", state: "★", consecutiveCorrect: 5, historyAccuracy: 0.95, easeFactor: 3.0, nextReviewAt: new Date(Date.now() + 604800000).toISOString(), lastAttemptId: null, lastHintLevel: "None", createTime: new Date(Date.now() - 1296000000).toISOString(), updateTime: new Date(Date.now() - 259200000).toISOString(), isFromPersistentSource: false },
      ],
      totalCount: 4,
      pageIndex: 1,
      pageSize: 20,
    },
  ],

  // getSessionResultResDtos — 2 条，一空一满
  getSessionResultResDtos: [
    { success: true, errorCode: null, correctCount: 8, totalCount: 10, blockedPoints: [] },
    {
      success: true, errorCode: null, correctCount: 15, totalCount: 20,
      blockedPoints: [
        { questionId: "q-001", knowledgePoint: "乘法分配律", summary: "注意括号展开" },
      ],
    },
  ],

  // getWrongQuestionsResDtos — 3 条，mastered 混合
  getWrongQuestionsResDtos: [
    {
      success: true, errorCode: null,
      items: [
        { id: 1, uId: "wq-001", userId: 1001, questionId: "q-001", bankId: "bank-001", subject: "数学", wrongCount: 3, lastWrongAt: new Date(Date.now() - 86400000).toISOString(), mastered: false, knowledgePoint: "乘法分配律", summary: "注意括号展开", createTime: new Date(Date.now() - 604800000).toISOString(), updateTime: new Date(Date.now() - 86400000).toISOString(), isFromPersistentSource: false },
        { id: 2, uId: "wq-002", userId: 1001, questionId: "q-002", bankId: "bank-001", subject: "数学", wrongCount: 2, lastWrongAt: new Date(Date.now() - 172800000).toISOString(), mastered: false, knowledgePoint: "分数除法", summary: "除以一个数等于乘以倒数", createTime: new Date(Date.now() - 432000000).toISOString(), updateTime: new Date(Date.now() - 172800000).toISOString(), isFromPersistentSource: false },
        { id: 3, uId: "wq-003", userId: 1001, questionId: "q-003", bankId: "bank-002", subject: "语文", wrongCount: 1, lastWrongAt: new Date(Date.now() - 259200000).toISOString(), mastered: true, knowledgePoint: "成语运用", summary: "望文生义", createTime: new Date(Date.now() - 604800000).toISOString(), updateTime: new Date(Date.now() - 259200000).toISOString(), isFromPersistentSource: false },
      ],
      total: 3,
    },
  ],

  // =====================================================================
  // P1：家校任务闭环域（Tasks）
  // =====================================================================

  // createTaskResDtos — 1 条
  createTaskResDtos: [
    { success: true, errorCode: null, taskUid: "task-001", assignedCount: 10 },
  ],

  // listTasksResDtos — 2 条，一 Active 一 Closed
  listTasksResDtos: [
    {
      success: true, errorCode: null,
      items: [
        { taskUid: "task-001", title: "每日口算", deadlineAt: new Date(Date.now() + 172800000).toISOString(), status: "Active", questionCount: 20, completionRate: 0.65 },
        { taskUid: "task-002", title: "本周阅读", deadlineAt: new Date(Date.now() - 86400000).toISOString(), status: "Closed", questionCount: 10, completionRate: 1.0 },
      ],
      total: 2,
    },
  ],

  // listMyTasksResDtos — 2 条，一正常 + 一 Overdue
  listMyTasksResDtos: [
    {
      success: true, errorCode: null,
      items: [
        { taskUid: "task-001", title: "每日口算", deadlineAt: new Date(Date.now() + 172800000).toISOString(), status: "InProgress", questionCount: 20, completionRate: 0.65 },
        { taskUid: "task-003", title: "周末作文", deadlineAt: new Date(Date.now() - 86400000).toISOString(), status: "Overdue", questionCount: 1, completionRate: 0.0 },
      ],
      total: 2,
    },
  ],

  // getTaskDetailResDtos — 1 条
  getTaskDetailResDtos: [
    {
      success: true, errorCode: null,
      taskUid: "task-001",
      title: "每日口算",
      description: "每天 20 道口算题，巩固基础运算能力",
      subject: "数学",
      bankId: "bank-001",
      deadlineAt: new Date(Date.now() + 172800000).toISOString(),
      status: "Active",
      questionCount: 20,
      allowRedo: true,
      members: [
        { userId: 1001, nickname: "小明", progress: 0.85, status: "InProgress", lastActiveAt: new Date(Date.now() - 3600000).toISOString() },
        { userId: 1002, nickname: "小红", progress: 1.0, status: "Completed", lastActiveAt: new Date(Date.now() - 7200000).toISOString() },
      ],
    },
  ],

  // getOwnerDashboardResDtos — 1 条
  getOwnerDashboardResDtos: [
    {
      success: true, errorCode: null,
      todayExecutionRate: 0.72,
      avgProgress: 0.58,
      overdueCount: 2,
      weakPointsTop5: [
        { knowledgePoint: "乘法分配律", accuracy: 0.3 },
        { knowledgePoint: "分数除法", accuracy: 0.4 },
        { knowledgePoint: "几何图形", accuracy: 0.5 },
        { knowledgePoint: "成语运用", accuracy: 0.55 },
        { knowledgePoint: "阅读理解", accuracy: 0.6 },
      ],
      taskList: [
        {
          task: { taskUid: "task-001", title: "每日口算", deadlineAt: new Date(Date.now() + 86400000).toISOString(), status: "Active", questionCount: 20 },
          completionRate: 0.65,
          status: "Active",
        },
      ],
    },
  ],

  // =====================================================================
  // P1：题库判题域（Bank + Judging）
  // =====================================================================

  // listBanksResDtos — 2 条，一公一私
  listBanksResDtos: [
    {
      success: true, errorCode: null,
      items: [
        { bankId: "bank-001", name: "小学数学口算题库", subject: "数学", privacy: "Public", ownerId: null, questionCount: 500, isTrialAvailable: true, description: "涵盖加减乘除、分数、小数等口算题" },
        { bankId: "bank-002", name: "我的错题本", subject: "数学", privacy: "Private", ownerId: 1001, questionCount: 45, isTrialAvailable: false, description: "个人错题汇总" },
      ],
      total: 2,
    },
  ],

  // getBankDetailResDtos — 2 条
  getBankDetailResDtos: [
    {
      success: true, errorCode: null,
      bankId: "bank-001",
      name: "小学数学口算题库",
      subject: "数学",
      privacy: "Public",
      ownerId: null,
      questionCount: 500,
      description: "涵盖加减乘除、分数、小数等口算题",
      topics: ["加法", "减法", "乘法", "除法", "分数", "小数"],
      previewQuestions: [
        { questionId: "q-001", topic: "乘法", difficulty: 3 },
      ],
    },
    {
      success: true, errorCode: null,
      bankId: "bank-002",
      name: "我的错题本",
      subject: "数学",
      privacy: "Private",
      ownerId: 1001,
      questionCount: 45,
      description: "个人错题汇总",
      topics: ["乘法分配律", "分数除法"],
      previewQuestions: [],
    },
  ],

  // createBankResDtos — 1 条
  createBankResDtos: [
    { success: true, errorCode: null, bankId: "bank-003" },
  ],

  // importQuestionsResDtos — 2 条
  importQuestionsResDtos: [
    { success: true, errorCode: null, imported: 20, failed: 0, failures: [] },
    {
      success: true, errorCode: null, imported: 15, failed: 3,
      failures: [
        { row: 5, reason: "答案缺失" },
        { row: 12, reason: "格式错误" },
        { row: 18, reason: "重复题目" },
      ],
    },
  ],

  // getKnowledgeCardResDtos — 1 条
  getKnowledgeCardResDtos: [
    { success: true, errorCode: null, cardType: "authorCard", content: "本题库由教研团队精心编写，覆盖小学数学核心知识点" },
  ],

  // reviewBackingPointsResDtos — 1 条
  reviewBackingPointsResDtos: [
    { success: true, errorCode: null, imported: 8, skipped: 2 },
  ],

  // submitJudgmentFeedbackResDtos — 1 条
  submitJudgmentFeedbackResDtos: [
    { success: true, errorCode: null, feedbackUid: "fb-001", status: "Pending" },
  ],

  // =====================================================================
  // P1：可视化激励域（Stats）
  // =====================================================================

  // getHeatmapResDtos — 2 条，一空一满
  getHeatmapResDtos: [
    { success: true, errorCode: null, days: [] },
    {
      success: true, errorCode: null,
      days: [
        { statDate: new Date(Date.now() - 6 * 86400000).toISOString().slice(0, 10), learnedCount: 5, starredCount: 1, wrongCount: 2, accuracy: 0.75, sessionCount: 2 },
        { statDate: new Date(Date.now() - 5 * 86400000).toISOString().slice(0, 10), learnedCount: 8, starredCount: 2, wrongCount: 3, accuracy: 0.73, sessionCount: 3 },
        { statDate: new Date(Date.now() - 4 * 86400000).toISOString().slice(0, 10), learnedCount: 12, starredCount: 3, wrongCount: 1, accuracy: 0.92, sessionCount: 4 },
        { statDate: new Date(Date.now() - 3 * 86400000).toISOString().slice(0, 10), learnedCount: 6, starredCount: 0, wrongCount: 4, accuracy: 0.60, sessionCount: 2 },
        { statDate: new Date(Date.now() - 2 * 86400000).toISOString().slice(0, 10), learnedCount: 10, starredCount: 2, wrongCount: 2, accuracy: 0.83, sessionCount: 3 },
        { statDate: new Date(Date.now() - 1 * 86400000).toISOString().slice(0, 10), learnedCount: 15, starredCount: 3, wrongCount: 1, accuracy: 0.94, sessionCount: 4 },
        { statDate: new Date(Date.now()).toISOString().slice(0, 10), learnedCount: 7, starredCount: 1, wrongCount: 2, accuracy: 0.78, sessionCount: 2 },
      ],
    },
  ],

  // getStreakResDtos — 1 条
  getStreakResDtos: [
    { success: true, errorCode: null, currentStreak: 7, longestStreak: 14 },
  ],

  // getPeriodReportResDtos — 1 条
  getPeriodReportResDtos: [
    {
      success: true, errorCode: null,
      learnedCount: 63,
      accuracy: 0.82,
      starredCount: 12,
      weakPoints: [
        { knowledgePoint: "乘法分配律", accuracy: 0.35 },
        { knowledgePoint: "分数除法", accuracy: 0.42 },
        { knowledgePoint: "几何图形", accuracy: 0.55 },
        { knowledgePoint: "成语运用", accuracy: 0.58 },
        { knowledgePoint: "阅读理解", accuracy: 0.62 },
      ],
    },
  ],

  // statsGetWrongQuestionsResDtos — 3 条
  statsGetWrongQuestionsResDtos: [
    {
      success: true, errorCode: null,
      items: [
        { questionId: "q-001", knowledgePoint: "乘法分配律", summary: "展开括号时的符号错误", wrongCount: 3, mastered: false },
        { questionId: "q-002", knowledgePoint: "分数除法", summary: "未正确转化为乘法", wrongCount: 2, mastered: false },
        { questionId: "q-003", knowledgePoint: "成语运用", summary: "望文生义", wrongCount: 1, mastered: true },
      ],
      total: 3,
    },
  ],

  // =====================================================================
  // P1：排行榜搭子域（Rank + Buddy）
  // =====================================================================

  // getRankingsResDtos — 2 条，一战力榜 + 一战绩榜已关闭
  getRankingsResDtos: [
    {
      success: true, errorCode: null,
      snapshotDate: new Date(Date.now() - 86400000).toISOString().slice(0, 10),
      rankEnabled: true,
      items: [
        { rank: 1, userId: 1001, nickname: "小明", avatarUrl: "", value: 120, trend: "Up", isMe: true },
        { rank: 2, userId: 1002, nickname: "小红", avatarUrl: "", value: 105, trend: "Flat", isMe: false },
        { rank: 3, userId: 1003, nickname: "小刚", avatarUrl: "", value: 98, trend: "Down", isMe: false },
      ],
    },
    {
      success: true, errorCode: null,
      snapshotDate: new Date(Date.now() - 86400000).toISOString().slice(0, 10),
      rankEnabled: false,
      items: [],
    },
  ],

  // getMyRankingResDtos — 1 条
  getMyRankingResDtos: [
    { success: true, errorCode: null, rank: 1, value: 120, trend: "Up", rankChange: 2 },
  ],

  // inviteBuddyResDtos — 1 条
  inviteBuddyResDtos: [
    { success: true, errorCode: null, inviteId: "inv-001", expiresAt: new Date(Date.now() + 604800000).toISOString() },
  ],

  // acceptBuddyInviteResDtos — 1 条
  acceptBuddyInviteResDtos: [
    { success: true, errorCode: null, buddyId: "buddy-001" },
  ],

  // rejectBuddyInviteResDtos — 1 条
  rejectBuddyInviteResDtos: [
    { success: true, errorCode: null, rejected: true },
  ],

  // listBuddiesResDtos — 2 条，一含搭子 + 一空
  listBuddiesResDtos: [
    {
      success: true, errorCode: null,
      items: [
        { buddyId: "buddy-001", userId: 1002, nickname: "小红", avatarUrl: "", streakDays: 5, status: "Accepted", rank: { userId: 1002, scopeId: 1, metricType: "Stars", metricValue: 105, snapshotDate: new Date(Date.now() - 86400000).toISOString().slice(0, 10), createdAt: new Date(Date.now() - 86400000).toISOString(), isFromPersistentSource: false, id: 1, uId: "rank-002" } },
        { buddyId: "buddy-002", userId: 1003, nickname: "小刚", avatarUrl: "", streakDays: 3, status: "Accepted", rank: { userId: 1003, scopeId: 1, metricType: "Stars", metricValue: 98, snapshotDate: new Date(Date.now() - 86400000).toISOString().slice(0, 10), createdAt: new Date(Date.now() - 86400000).toISOString(), isFromPersistentSource: false, id: 2, uId: "rank-003" } },
      ],
      total: 2,
    },
    { success: true, errorCode: null, items: [], total: 0 },
  ],

  // getBuddyRankResDtos — 1 条
  getBuddyRankResDtos: [
    { success: true, errorCode: null, rank: 2, metricValue: 105, trend: "Flat", metricType: "Stars" },
  ],

  // removeBuddyResDtos — 1 条
  removeBuddyResDtos: [
    { success: true, errorCode: null, removed: true },
  ],

  // =====================================================================
  // P1：搭子 PK 竞技域（Pk）
  // =====================================================================

  // createPkMatchResDtos — 1 条
  createPkMatchResDtos: [
    { success: true, errorCode: null, matchUid: "pk-001", inviteCode: "A3K9" },
  ],

  // joinPkMatchResDtos — 1 条
  joinPkMatchResDtos: [
    { success: true, errorCode: null, matchUid: "pk-001", questionCount: 10, perQuestionTimeS: 30 },
  ],

  // submitPkAnswerResDtos — 2 条
  submitPkAnswerResDtos: [
    { success: true, errorCode: null, result: "Correct", score: 10, correctAnswer: "B" },
    { success: true, errorCode: null, result: "Wrong", score: 0, correctAnswer: "C" },
  ],

  // getPkResultResDtos — 2 条
  getPkResultResDtos: [
    {
      success: true, errorCode: null,
      status: "Finished",
      winnerId: 1001,
      finishReason: "Score",
      winReason: "正确率更高",
      players: [
        { userId: 1001, nickname: "小明", score: 80, correctCount: 8, aiComment: "计算准确，继续保持！" },
        { userId: 1002, nickname: "小红", score: 60, correctCount: 6, aiComment: "注意审题，避免粗心" },
      ],
    },
    {
      success: true, errorCode: null,
      status: "Finished",
      winnerId: null,
      finishReason: "Forfeit",
      winReason: null,
      players: [
        { userId: 1001, nickname: "小明", score: 0, correctCount: 0, aiComment: "" },
        { userId: 1003, nickname: "小刚", score: 0, correctCount: 0, aiComment: "" },
      ],
    },
  ],

  // getPkStatsResDtos — 1 条
  getPkStatsResDtos: [
    { success: true, errorCode: null, totalMatches: 8, winCount: 5, winRate: 0.625 },
  ],

  // =====================================================================
  // P1：家长报告订阅域（Parent）
  // =====================================================================

  // createParentRelationResDtos — 1 条
  createParentRelationResDtos: [
    { success: true, errorCode: null, relationUid: "rel-001" },
  ],

  // listChildrenResDtos — 1 条
  listChildrenResDtos: [
    {
      success: true, errorCode: null,
      items: [
        { studentUid: "stu-001", name: "小明", grade: "三年级", hasSubscription: true },
        { studentUid: "stu-002", name: "小红", grade: "五年级", hasSubscription: false },
      ],
    },
  ],

  // startTrialResDtos — 1 条
  startTrialResDtos: [
    { success: true, errorCode: null, status: "Trialing", trialEndAt: new Date(Date.now() + 604800000).toISOString() },
  ],

  // listSubscriptionsResDtos — 2 条
  listSubscriptionsResDtos: [
    {
      success: true, errorCode: null,
      items: [
        { subscriptionUid: "sub-001", studentUid: "stu-001", studentName: "小明", status: "Trialing", planType: "Monthly", trialEndAt: new Date(Date.now() + 604800000).toISOString(), expiresAt: null },
        { subscriptionUid: "sub-002", studentUid: "stu-002", studentName: "小红", status: "Active", planType: "Yearly", trialEndAt: null, expiresAt: new Date(Date.now() + 31536000000).toISOString() },
      ],
    },
  ],

  // cancelSubscriptionResDtos — 1 条
  cancelSubscriptionResDtos: [
    { success: true, errorCode: null, cancelled: true },
  ],

  // getDashboardReportResDtos — 2 条，一 locked 一完整
  getDashboardReportResDtos: [
    {
      success: true, errorCode: null,
      subscription: null, todayCompleted: null, streakDays: null,
      weekProgress: null, subjectsMastery: [],
      locked: true,
    },
    {
      success: true, errorCode: null,
      subscription: { subscriptionUid: "sub-001", status: "Active" },
      todayCompleted: true, streakDays: 7,
      weekProgress: {
        Mon: { learnedCount: 12, accuracy: 0.85 },
        Tue: { learnedCount: 8, accuracy: 0.78 },
        Wed: { learnedCount: 15, accuracy: 0.90 },
        Thu: { learnedCount: 10, accuracy: 0.82 },
        Fri: { learnedCount: 6, accuracy: 0.75 },
        Sat: { learnedCount: 20, accuracy: 0.88 },
        Sun: { learnedCount: 7, accuracy: 0.80 },
      },
      subjectsMastery: [
        { subject: "数学", accuracy: 0.82, totalQuestions: 120, masteredCount: 98 },
        { subject: "语文", accuracy: 0.75, totalQuestions: 80, masteredCount: 60 },
      ],
      locked: false,
    },
  ],

  // getProgressReportResDtos — 1 条
  getProgressReportResDtos: [
    {
      success: true, errorCode: null,
      trend: [
        { statDate: new Date(Date.now() - 6 * 86400000).toISOString().slice(0, 10), learnedCount: 10, accuracy: 0.80, starredCount: 2, wrongCount: 2, sessionCount: 3 },
        { statDate: new Date(Date.now() - 5 * 86400000).toISOString().slice(0, 10), learnedCount: 12, accuracy: 0.83, starredCount: 3, wrongCount: 1, sessionCount: 4 },
        { statDate: new Date(Date.now() - 4 * 86400000).toISOString().slice(0, 10), learnedCount: 8, accuracy: 0.75, starredCount: 1, wrongCount: 3, sessionCount: 2 },
        { statDate: new Date(Date.now() - 3 * 86400000).toISOString().slice(0, 10), learnedCount: 15, accuracy: 0.87, starredCount: 4, wrongCount: 1, sessionCount: 5 },
        { statDate: new Date(Date.now() - 2 * 86400000).toISOString().slice(0, 10), learnedCount: 11, accuracy: 0.82, starredCount: 2, wrongCount: 2, sessionCount: 3 },
        { statDate: new Date(Date.now() - 1 * 86400000).toISOString().slice(0, 10), learnedCount: 14, accuracy: 0.86, starredCount: 3, wrongCount: 1, sessionCount: 4 },
        { statDate: new Date(Date.now()).toISOString().slice(0, 10), learnedCount: 9, accuracy: 0.78, starredCount: 1, wrongCount: 3, sessionCount: 2 },
      ],
      vsLastWeek: { learnedDelta: 5, accuracyDelta: 0.03, starredDelta: 1, wrongDelta: -1 },
    },
  ],

  // getWeaknessReportResDtos — 2 条，一含弱项 + 一空
  getWeaknessReportResDtos: [
    {
      success: true, errorCode: null,
      weakPoints: [
        { knowledgePoint: "乘法分配律", accuracy: 0.35, stateText: "✕", questionCount: 8 },
        { knowledgePoint: "分数除法", accuracy: 0.42, stateText: "△", questionCount: 6 },
        { knowledgePoint: "几何图形", accuracy: 0.55, stateText: "△", questionCount: 5 },
      ],
    },
    { success: true, errorCode: null, weakPoints: [] },
  ],
};

// =====================================================================
// 场景覆盖（scenarioOverrides）
// 切换场景时：setScenario('error') / setScenario('empty') 等
// =====================================================================

export type ScenarioOverrides = Record<string, DatasetSeed>;

export const scenarioOverrides: ScenarioOverrides = {
  // ── 错误态场景 ──
  error: {
    loginPayloads: [
      { success: false, displayName: null, userName: null, sessionKey: null, accessToken: null, refreshToken: null, expiresAt: null, deviceId: null, extensions: [] },
    ],
    listGroupsResDtos: [
      { success: false, errorCode: "INTERNAL_ERROR", items: [], total: 0 },
    ],
    getHeatmapResDtos: [
      { success: false, errorCode: "NETWORK_ERROR", days: [] },
    ],
  },

  // ── 空数据场景 ──
  empty: {
    listGroupsResDtos: [
      { success: true, errorCode: null, items: [], total: 0 },
    ],
    listTasksResDtos: [
      { success: true, errorCode: null, items: [], total: 0 },
    ],
    listBanksResDtos: [
      { success: true, errorCode: null, items: [], total: 0 },
    ],
    getHeatmapResDtos: [
      { success: true, errorCode: null, days: [] },
    ],
    getMemoryStatesResDtos: [
      { success: true, errorCode: null, items: [], totalCount: 0, pageIndex: 1, pageSize: 20 },
    ],
    getReviewQueueResDtos: [
      { success: true, errorCode: null, items: [], overdueCount: 0 },
    ],
    getWrongQuestionsResDtos: [
      { success: true, errorCode: null, items: [], total: 0 },
    ],
    listBuddiesResDtos: [
      { success: true, errorCode: null, items: [], total: 0 },
    ],
  },

  // ── 新手场景（刚注册，无数据） ──
  beginner: {
    listGroupsResDtos: [
      { success: true, errorCode: null, items: [], total: 0 },
    ],
    listTasksResDtos: [
      { success: true, errorCode: null, items: [], total: 0 },
    ],
    getHeatmapResDtos: [
      { success: true, errorCode: null, days: [] },
    ],
    getMemoryStatesResDtos: [
      { success: true, errorCode: null, items: [], totalCount: 0, pageIndex: 1, pageSize: 20 },
    ],
    getStreakResDtos: [
      { success: true, errorCode: null, currentStreak: 0, longestStreak: 0 },
    ],
  },

  // ── 激励场景（高连击、多答对） ──
  motivational: {
    getStreakResDtos: [
      { success: true, errorCode: null, currentStreak: 30, longestStreak: 45 },
    ],
    getHeatmapResDtos: [
      {
        success: true, errorCode: null,
        days: [
          { statDate: new Date(Date.now() - 6 * 86400000).toISOString().slice(0, 10), learnedCount: 20, starredCount: 3, wrongCount: 1, accuracy: 0.95, sessionCount: 5 },
          { statDate: new Date(Date.now() - 5 * 86400000).toISOString().slice(0, 10), learnedCount: 25, starredCount: 4, wrongCount: 0, accuracy: 1.0, sessionCount: 6 },
          { statDate: new Date(Date.now() - 4 * 86400000).toISOString().slice(0, 10), learnedCount: 18, starredCount: 2, wrongCount: 2, accuracy: 0.90, sessionCount: 4 },
          { statDate: new Date(Date.now() - 3 * 86400000).toISOString().slice(0, 10), learnedCount: 22, starredCount: 3, wrongCount: 1, accuracy: 0.96, sessionCount: 5 },
          { statDate: new Date(Date.now() - 2 * 86400000).toISOString().slice(0, 10), learnedCount: 30, starredCount: 5, wrongCount: 0, accuracy: 1.0, sessionCount: 7 },
          { statDate: new Date(Date.now() - 1 * 86400000).toISOString().slice(0, 10), learnedCount: 28, starredCount: 4, wrongCount: 1, accuracy: 0.97, sessionCount: 6 },
          { statDate: new Date(Date.now()).toISOString().slice(0, 10), learnedCount: 15, starredCount: 2, wrongCount: 2, accuracy: 0.88, sessionCount: 4 },
        ],
      },
    ],
    getMyRankingResDtos: [
      { success: true, errorCode: null, rank: 1, value: 500, trend: "Up", rankChange: 5 },
    ],
  },
};

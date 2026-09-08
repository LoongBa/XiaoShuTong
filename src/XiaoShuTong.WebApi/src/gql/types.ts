/** Internal type. DO NOT USE DIRECTLY. */
export type Incremental<T> = T | { [P in keyof T]?: P extends ' $fragmentName' | '__typename' ? T[P] : never };
export type Maybe<T> = T | null;
export type InputMaybe<T> = Maybe<T>;
/** All built-in and custom scalars, mapped to their actual values */
export type Scalars = {
  ID: { input: string; output: string; }
  String: { input: string; output: string; }
  Boolean: { input: boolean; output: boolean; }
  Int: { input: number; output: number; }
  Float: { input: number; output: number; }
  /** The `DateTime` scalar type represents a date and time with time zone offset information. */
  DateTime: { input: unknown; output: unknown; }
  /** The `Decimal` scalar type represents a decimal floating-point number with high precision. */
  Decimal: { input: unknown; output: unknown; }
  /** The `LocalDate` scalar type represents a date without time or time zone information. */
  LocalDate: { input: unknown; output: unknown; }
  /** The `Long` scalar type represents a signed 64-bit integer. */
  Long: { input: unknown; output: unknown; }
  /** The `UnsignedByte` scalar type represents an unsigned 8-bit integer. */
  UnsignedByte: { input: unknown; output: unknown; }
};

/** 同意搭子邀请请求 DTO */
export type AcceptBuddyInviteReqDtoInput = {
  /** 邀请记录 Uid */
  inviteId: Scalars['String']['input'];
};

/** 同意搭子邀请响应 DTO */
export type AcceptBuddyInviteResDto = {
  __typename?: 'AcceptBuddyInviteResDto';
  /** 搭子关系 Uid */
  buddyId: Scalars['String']['output'];
  /** 错误码（失败时） */
  errorCode?: Maybe<Scalars['String']['output']>;
  /** 是否成功 */
  success: Scalars['Boolean']['output'];
};

/** 成员激活请求 DTO */
export type ActivateMemberReqDtoInput = {
  /** 一次性邀请码（8 位） */
  oneTimeCode: Scalars['String']['input'];
  /** 手机号后四位（4 位） */
  phoneLast4: Scalars['String']['input'];
};

/** 成员激活响应 DTO */
export type ActivateMemberResDto = {
  __typename?: 'ActivateMemberResDto';
  /** 错误码（失败时） */
  errorCode?: Maybe<Scalars['String']['output']>;
  /** 加入的群组 Id */
  groupId: Scalars['Long']['output'];
  /** 群组名 */
  groupName: Scalars['String']['output'];
  /** GroupMembers Id */
  memberId: Scalars['Long']['output'];
  /** 成员角色（student/parent） */
  role: Scalars['String']['output'];
  /** 是否成功 */
  success: Scalars['Boolean']['output'];
};

/** Defines when a policy shall be executed. */
export enum ApplyPolicy {
  /** After the resolver was executed. */
  AfterResolver = 'AFTER_RESOLVER',
  /** Before the resolver was executed. */
  BeforeResolver = 'BEFORE_RESOLVER',
  /** The policy is applied in the validation step before the execution. */
  Validation = 'VALIDATION'
}

/** 任务分配状态 */
export enum AssignmentStatus {
  /** 已完成 */
  Completed = 'COMPLETED',
  /** 进行中 */
  InProgress = 'IN_PROGRESS',
  /** 已逾期 */
  Overdue = 'OVERDUE',
  /** 待执行 */
  Pending = 'PENDING'
}

/** 题库卡片项 DTO（复用 BanksDto + 计数壳） */
export type BankListItemDto = {
  __typename?: 'BankListItemDto';
  /** 题库信息（复用 BanksDto） */
  bank?: Maybe<BanksDto>;
  /** 题量 */
  questionCount: Scalars['Int']['output'];
  /** 知识点数（切片简化为 0，B.2 展示完整树） */
  topicCount: Scalars['Int']['output'];
};

/** 题库隐私 */
export enum BankPrivacy {
  /** 群组（群组内引用） */
  Group = 'GROUP',
  /** 私有（默认，仅创建者可见） */
  Private = 'PRIVATE',
  /** 公开（官方/候选） */
  Public = 'PUBLIC'
}

/** 题库用途 */
export enum BankPurpose {
  /** 检验 */
  Assess = 'ASSESS',
  /** 背诵 */
  Memorize = 'MEMORIZE',
  /** 趣味 */
  Play = 'PLAY'
}

/** 题库状态 */
export enum BankStatus {
  /** 启用 */
  Active = 'ACTIVE',
  /** 归档 */
  Archived = 'ARCHIVED',
  /** 隐藏（下线入口） */
  Hidden = 'HIDDEN'
}

/** 题库元数据（API 查询索引） 的手写 DTO 扩展 */
export type BanksDto = {
  __typename?: 'BanksDto';
  /** 业务键（chinese-7to9-pep） */
  bankId: Scalars['String']['output'];
  /** 创建时间 */
  createTime: Scalars['DateTime']['output'];
  /** 自增主键 */
  id: Scalars['Long']['output'];
  isFromPersistentSource: Scalars['Boolean']['output'];
  /** bank.v1.json 文件路径（内容权威） */
  jsonPath: Scalars['String']['output'];
  /** 题库名（≤128） */
  name: Scalars['String']['output'];
  /** 私域题库 owner（官方题库 NULL） */
  ownerId?: Maybe<Scalars['Long']['output']>;
  /** 隐私（Private/Public/Group） */
  privacy: BankPrivacy;
  /** 用途（Memorize/Assess/Play） */
  purpose: BankPurpose;
  /** 状态（Active/Hidden/Archived） */
  status: BankStatus;
  /** 学科 */
  subject: Subject;
  /** 标签（JSONB，如 ["高频考点"]） */
  tags: Array<Scalars['String']['output']>;
  /** 外部业务键（uuid，API/DTO 暴露） */
  uId: Scalars['String']['output'];
  /** 更新时间 */
  updateTime: Scalars['DateTime']['output'];
  /** 内容版本（MinVer） */
  version: Scalars['String']['output'];
};

/** 卡壳知识点 DTO */
export type BlockedPointDto = {
  __typename?: 'BlockedPointDto';
  /** 知识点 */
  knowledgePoint: Scalars['String']['output'];
  /** 题目业务键 */
  questionId: Scalars['String']['output'];
  /** 记忆状态 */
  state: Scalars['String']['output'];
};

/** 搭子列表项 DTO */
export type BuddyListItemDto = {
  __typename?: 'BuddyListItemDto';
  /** 头像 URL（账户域，切片为空） */
  avatarUrl: Scalars['String']['output'];
  /** 搭子关系 Uid */
  buddyId: Scalars['String']['output'];
  /** 昵称（账户域，切片为空） */
  nickname: Scalars['String']['output'];
  /** 对方最新排名快照（仅排名与数值，不暴露答题明细） */
  rank?: Maybe<RankSnapshotsDto>;
  /** 关系状态 */
  status: Scalars['String']['output'];
  /** 连续打卡天数 */
  streakDays: Scalars['Int']['output'];
  /** 搭子用户 Id */
  userId: Scalars['Long']['output'];
};

/** 取消续费请求 DTO */
export type CancelSubscriptionReqDtoInput = {
  /** 订阅外部键 */
  subscriptionUid: Scalars['String']['input'];
};

/** 取消续费响应 DTO */
export type CancelSubscriptionResDto = {
  __typename?: 'CancelSubscriptionResDto';
  /** 错误码（失败时） */
  errorCode?: Maybe<Scalars['String']['output']>;
  /** 是否成功 */
  success: Scalars['Boolean']['output'];
};

/** Challenge 响应（自签名 token 模式，无状态）。 */
export type ChallengeResponse = {
  __typename?: 'ChallengeResponse';
  challengeToken: Scalars['String']['output'];
  iterations: Scalars['Int']['output'];
  salt: Scalars['String']['output'];
};

/** 安全改密请求。 */
export type ChangePasswordSecureInput = {
  challengeToken: Scalars['String']['input'];
  newClientHash: Scalars['String']['input'];
  newSalt: Scalars['String']['input'];
  oldChallengeResponse: Scalars['String']['input'];
};

/** 孩子项 DTO */
export type ChildItemDto = {
  __typename?: 'ChildItemDto';
  /** 班级（账户域/群组域，切片为空） */
  className: Scalars['String']['output'];
  /** 是否有订阅 */
  hasSubscription: Scalars['Boolean']['output'];
  /** 昵称（账户域，切片为空） */
  nickname: Scalars['String']['output'];
  /** 孩子外部键（账户域 Uid 未实施，透传 Id） */
  studentUid: Scalars['String']['output'];
};

/** 创建题库请求 DTO */
export type CreateBankReqDtoInput = {
  /** 群组 ID 列表（Privacy=Group 时必传） */
  groupIds?: InputMaybe<Array<Scalars['String']['input']>>;
  /** 题库名（≤128） */
  name: Scalars['String']['input'];
  /** 隐私（默认 Private） */
  privacy?: InputMaybe<Scalars['String']['input']>;
  /** 用途（默认 Memorize） */
  purpose?: InputMaybe<Scalars['String']['input']>;
  /** 学科 */
  subject: Scalars['String']['input'];
};

/** 创建题库响应 DTO */
export type CreateBankResDto = {
  __typename?: 'CreateBankResDto';
  /** 题库业务键（系统生成） */
  bankId: Scalars['String']['output'];
  /** 题库外部键 */
  bankUid: Scalars['String']['output'];
  /** 错误码（失败时） */
  errorCode?: Maybe<Scalars['String']['output']>;
  /** 是否成功 */
  success: Scalars['Boolean']['output'];
};

/** 激活建群请求 DTO */
export type CreateGroupReqDtoInput = {
  /** 内测邀请码（16 位） */
  betaCode: Scalars['String']['input'];
  /** 年级 */
  grade?: InputMaybe<Scalars['String']['input']>;
  /** 群组名称（≤64） */
  name: Scalars['String']['input'];
  /** 学科 */
  subject: Scalars['String']['input'];
};

/** 激活建群响应 DTO */
export type CreateGroupResDto = {
  __typename?: 'CreateGroupResDto';
  /** 错误码（失败时，SNAKE_CASE 语义名） */
  errorCode?: Maybe<Scalars['String']['output']>;
  /** 新群组 Id */
  groupId: Scalars['Long']['output'];
  /** 是否成功 */
  success: Scalars['Boolean']['output'];
};

/** 家长关联孩子请求 DTO */
export type CreateParentRelationReqDtoInput = {
  /** 关系（Parent/Guardian/Grandparent，默认 Parent） */
  relation?: InputMaybe<Scalars['String']['input']>;
  /** 孩子 Id（账户域 Uid→Id 未实施，直接 long） */
  studentId: Scalars['Long']['input'];
};

/** 家长关联孩子响应 DTO */
export type CreateParentRelationResDto = {
  __typename?: 'CreateParentRelationResDto';
  /** 错误码（失败时） */
  errorCode?: Maybe<Scalars['String']['output']>;
  /** 关联外部键 */
  relationUid: Scalars['String']['output'];
  /** 是否成功 */
  success: Scalars['Boolean']['output'];
};

/** 发起 PK 请求 DTO */
export type CreatePkMatchReqDtoInput = {
  /** 题库业务键 */
  bankId: Scalars['String']['input'];
  /** 模式（Sync/Async，默认 Sync） */
  mode?: InputMaybe<Scalars['String']['input']>;
  /** 对手用户 Id（accepted 搭子） */
  opponentUserId: Scalars['Long']['input'];
  /** 题量（5/10/20，默认 10） */
  questionCount: Scalars['Int']['input'];
  /** 知识点/主题 */
  topic?: InputMaybe<Scalars['String']['input']>;
};

/** 发起 PK 响应 DTO */
export type CreatePkMatchResDto = {
  __typename?: 'CreatePkMatchResDto';
  /** 错误码（失败时） */
  errorCode?: Maybe<Scalars['String']['output']>;
  /** 4 位对战码 */
  inviteCode: Scalars['String']['output'];
  /** 对局外部键 */
  matchUid: Scalars['String']['output'];
  /** 是否成功 */
  success: Scalars['Boolean']['output'];
};

/** 创建学习会话请求 DTO */
export type CreateStudySessionReqDtoInput = {
  /** 题库业务键 */
  bankId: Scalars['String']['input'];
  /** 计划题数（10/20/30/50，默认 20） */
  questionCount: Scalars['Int']['input'];
  /** 场景（Memorize/Assess/PlayPk/PlayDaily） */
  scenario: Scalars['String']['input'];
  /** 会话类型（Progressive/Free/Assembled/Level/Pk） */
  sessionType: Scalars['String']['input'];
  /** 群组任务关联（任务会话必传） */
  taskId?: InputMaybe<Scalars['Long']['input']>;
};

/** 创建学习会话响应 DTO */
export type CreateStudySessionResDto = {
  __typename?: 'CreateStudySessionResDto';
  /** 错误码（失败时） */
  errorCode?: Maybe<Scalars['String']['output']>;
  /** 本次题数 */
  questionCount: Scalars['Int']['output'];
  /** 会话外部键（uuid） */
  sessionUid: Scalars['String']['output'];
  /** 是否成功 */
  success: Scalars['Boolean']['output'];
};

/** 布置任务请求 DTO */
export type CreateTaskReqDtoInput = {
  /** 是否允许重做 */
  allowRedo: Scalars['Boolean']['input'];
  /** 题库业务键（自由编排时为空） */
  bankId?: InputMaybe<Scalars['String']['input']>;
  /** 截止时间 */
  deadlineAt?: InputMaybe<Scalars['DateTime']['input']>;
  /** 任务说明 */
  description?: InputMaybe<Scalars['String']['input']>;
  /** 群组外部键 */
  groupUid: Scalars['String']['input'];
  /** 题目 ID 列表 */
  questionIds: Array<Scalars['String']['input']>;
  /** 场景（Memorize/Assess） */
  scenario?: InputMaybe<Scalars['String']['input']>;
  /** 会话类型（Progressive/Free/Assembled） */
  sessionType?: InputMaybe<Scalars['String']['input']>;
  /** 任务名（≤128） */
  title: Scalars['String']['input'];
};

/** 布置任务响应 DTO */
export type CreateTaskResDto = {
  __typename?: 'CreateTaskResDto';
  /** 自动分配的学生数 */
  assignedCount: Scalars['Int']['output'];
  /** 错误码（失败时） */
  errorCode?: Maybe<Scalars['String']['output']>;
  /** 是否成功 */
  success: Scalars['Boolean']['output'];
  /** 任务外部键 */
  taskUid: Scalars['String']['output'];
};

/** 每日统计（热力图/连续天数数据源，一人一日一行） 的手写 DTO 扩展 */
export type DailyStatsDto = {
  __typename?: 'DailyStatsDto';
  /** 当日正确率 */
  accuracy?: Maybe<Scalars['Float']['output']>;
  /** 创建时间（框架审计字段） */
  createTime: Scalars['DateTime']['output'];
  /** 自增主键 */
  id: Scalars['Long']['output'];
  isFromPersistentSource: Scalars['Boolean']['output'];
  /** 学习题数 */
  learnedCount: Scalars['Int']['output'];
  /** 复习题数 */
  reviewCount: Scalars['Int']['output'];
  /** 点亮★数（热力图） */
  starredCount: Scalars['Int']['output'];
  /** UTC+8 业务日期 */
  statDate: Scalars['LocalDate']['output'];
  /** 学习时长（秒） */
  studySeconds: Scalars['Int']['output'];
  /** 外部业务键 */
  uId: Scalars['String']['output'];
  /** 更新时间（框架审计字段） */
  updateTime: Scalars['DateTime']['output'];
  /** 学生用户 */
  userId: Scalars['Long']['output'];
};

/** 看板任务项 DTO（复用 TasksDto + 完成率/状态壳） */
export type DashboardTaskDto = {
  __typename?: 'DashboardTaskDto';
  /** 完成任务率 */
  completionRate: Scalars['Float']['output'];
  /** 状态（Normal/Overdue） */
  status: Scalars['String']['output'];
  /** 任务信息（复用 TasksDto） */
  task?: Maybe<TasksDto>;
};

/**
 * 登录认证方式：描述用户通过何种方式证明身份。
 * 与 EnumLoginFrom（客户端渠道）是正交的两个概念。
 */
export enum EnumLoginAuthType {
  OAuth = 'O_AUTH',
  Password = 'PASSWORD',
  SecurePassword = 'SECURE_PASSWORD',
  SmsCode = 'SMS_CODE',
  Unset = 'UNSET',
  WeChatApplet = 'WE_CHAT_APPLET',
  WeChatScan = 'WE_CHAT_SCAN'
}

export enum EnumLoginFrom {
  AliPayApp = 'ALI_PAY_APP',
  AliPayWeb = 'ALI_PAY_WEB',
  App = 'APP',
  Customized = 'CUSTOMIZED',
  ELinkApp = 'E_LINK_APP',
  MobileWeb = 'MOBILE_WEB',
  PcWeb = 'PC_WEB',
  /**
   * V4.9.9: 系统身份（后台任务、OAuth 回调、系统服务）。
   * 取值 100 高位保留，避免与后续顺序新增值冲突。
   */
  System = 'SYSTEM',
  Tester = 'TESTER',
  Unset = 'UNSET',
  WechatApp = 'WECHAT_APP',
  WechatWep = 'WECHAT_WEP'
}

/** 导出 CSV 请求 DTO */
export type ExportRosterCsvReqDtoInput = {
  /** 群组 Id */
  groupId: Scalars['Long']['input'];
  /** 批次 Id */
  importId: Scalars['Long']['input'];
};

/** 导出 CSV 响应 DTO */
export type ExportRosterCsvResDto = {
  __typename?: 'ExportRosterCsvResDto';
  /** CSV 列名 */
  columns: Array<Scalars['String']['output']>;
  /** OSS 下载链接 */
  csvFileUrl?: Maybe<Scalars['String']['output']>;
  /** 错误码（失败时） */
  errorCode?: Maybe<Scalars['String']['output']>;
  /** 过期时间 */
  expiresAt?: Maybe<Scalars['DateTime']['output']>;
  /** 是否成功 */
  success: Scalars['Boolean']['output'];
};

/** 扩展字段条目（替代 Dictionary，兼容 GraphQL schema）。 */
export type ExtensionEntry = {
  __typename?: 'ExtensionEntry';
  key: Scalars['String']['output'];
  value?: Maybe<Scalars['String']['output']>;
};

/** 生成一次性邀请码请求 DTO */
export type GenerateInviteCodesReqDtoInput = {
  /** 确认标识（防误触） */
  confirm: Scalars['Boolean']['input'];
  /** 群组 Id */
  groupId: Scalars['Long']['input'];
  /** 批次 Id */
  importId: Scalars['Long']['input'];
};

/** 生成一次性邀请码响应 DTO */
export type GenerateInviteCodesResDto = {
  __typename?: 'GenerateInviteCodesResDto';
  /** 错误码（失败时） */
  errorCode?: Maybe<Scalars['String']['output']>;
  /** 生成数量 */
  generatedCount: Scalars['Int']['output'];
  /** 是否成功 */
  success: Scalars['Boolean']['output'];
};

/** 题库详情请求 DTO */
export type GetBankDetailReqDtoInput = {
  /** 题库业务键 */
  bankId: Scalars['String']['input'];
};

/** 题库详情响应 DTO（外层壳：Success/ErrorCode + 内嵌实体 Dto） */
export type GetBankDetailResDto = {
  __typename?: 'GetBankDetailResDto';
  /** 题库信息（复用 BanksDto） */
  bank?: Maybe<BanksDto>;
  /** 错误码（失败时） */
  errorCode?: Maybe<Scalars['String']['output']>;
  /** 篇目示例（不含答案） */
  previewQuestions: Array<QuestionsDto>;
  /** 是否成功 */
  success: Scalars['Boolean']['output'];
  /** 知识点树 */
  topics: Array<TopicNodeDto>;
};

/** 搭子排名详情请求 DTO */
export type GetBuddyRankReqDtoInput = {
  /** 搭子关系 Uid */
  buddyId: Scalars['String']['input'];
  /** 群组 id 或年级 key */
  scopeId: Scalars['String']['input'];
  /** 范围类型（Group/Grade） */
  scopeType: Scalars['String']['input'];
  /** 学科筛选 */
  subject?: InputMaybe<Scalars['String']['input']>;
};

/** 搭子排名详情响应 DTO */
export type GetBuddyRankResDto = {
  __typename?: 'GetBuddyRankResDto';
  /** 错误码（失败时） */
  errorCode?: Maybe<Scalars['String']['output']>;
  /** 战力/战绩值 */
  metricValue: Scalars['Decimal']['output'];
  /** 排名 */
  rank: Scalars['Int']['output'];
  /** 快照日期 */
  snapshotDate: Scalars['DateTime']['output'];
  /** 是否成功 */
  success: Scalars['Boolean']['output'];
  /** 趋势（Up/Down/Flat） */
  trend: Scalars['String']['output'];
};

/** 成长总览请求 DTO */
export type GetDashboardReportReqDtoInput = {
  /** 孩子 Id */
  studentId: Scalars['Long']['input'];
};

/** 成长总览响应 DTO（合规：无群组正确率排名） */
export type GetDashboardReportResDto = {
  __typename?: 'GetDashboardReportResDto';
  /** 错误码（失败时） */
  errorCode?: Maybe<Scalars['String']['output']>;
  /** 未订阅标记（完整内容锁定） */
  locked: Scalars['Boolean']['output'];
  /** 坚持天数（预览项） */
  streakDays?: Maybe<Scalars['Int']['output']>;
  /** 学科掌握度（完整） */
  subjectsMastery: Array<SubjectMasteryDto>;
  /** 订阅状态 */
  subscription?: Maybe<SubscriptionBriefDto>;
  /** 是否成功 */
  success: Scalars['Boolean']['output'];
  /** 今日任务完成（预览项） */
  todayCompleted?: Maybe<Scalars['Boolean']['output']>;
  /** 本周进度（完整） */
  weekProgress?: Maybe<WeekProgressDto>;
};

/** 热力图请求 DTO */
export type GetHeatmapReqDtoInput = {
  /** 区间止 */
  end: Scalars['DateTime']['input'];
  /** 区间起（YYYY-MM-DD） */
  start: Scalars['DateTime']['input'];
};

/** 热力图响应 DTO */
export type GetHeatmapResDto = {
  __typename?: 'GetHeatmapResDto';
  /** 每日数据 */
  days: Array<DailyStatsDto>;
  /** 错误码（失败时） */
  errorCode?: Maybe<Scalars['String']['output']>;
  /** 是否成功 */
  success: Scalars['Boolean']['output'];
};

/** 请求提示 DTO */
export type GetHintReqDtoInput = {
  /** 难度档（S1/S2/S3，可空按状态路由） */
  difficultySlot?: InputMaybe<Scalars['String']['input']>;
  /** 题目业务键 */
  questionId: Scalars['String']['input'];
};

/** 请求提示响应 DTO */
export type GetHintResDto = {
  __typename?: 'GetHintResDto';
  /** 难度档（S1/S2/S3） */
  difficultySlot: Scalars['String']['output'];
  /** 错误码（失败时） */
  errorCode?: Maybe<Scalars['String']['output']>;
  /** 提示文本（≤20 字线索） */
  hint: Scalars['String']['output'];
  /** 提示来源（记忆钩子） */
  hintSource: Scalars['String']['output'];
  /** 是否成功 */
  success: Scalars['Boolean']['output'];
};

/** 知识卡片请求 DTO */
export type GetKnowledgeCardReqDtoInput = {
  /** 题目业务键 */
  questionId: Scalars['String']['input'];
};

/** 知识卡片响应 DTO */
export type GetKnowledgeCardResDto = {
  __typename?: 'GetKnowledgeCardResDto';
  /** 卡片类型（authorCard/wordCard/eventCard） */
  cardType: Scalars['String']['output'];
  /** 卡片内容（按题库模板） */
  content: Scalars['String']['output'];
  /** 错误码（失败时） */
  errorCode?: Maybe<Scalars['String']['output']>;
  /** 是否成功 */
  success: Scalars['Boolean']['output'];
};

/** 查询成员列表请求 DTO */
export type GetMembersReqDtoInput = {
  /** 群组 Id */
  groupId: Scalars['Long']['input'];
};

/** 记忆状态查询请求 DTO */
export type GetMemoryStatesReqDtoInput = {
  /** 题库过滤 */
  bankId?: InputMaybe<Scalars['String']['input']>;
  /** 页码（默认 1） */
  pageIndex: Scalars['Int']['input'];
  /** 每页数（1~100，默认 20） */
  pageSize: Scalars['Int']['input'];
  /** 状态过滤 */
  state?: InputMaybe<MemoryState>;
};

/** 记忆状态查询响应 DTO */
export type GetMemoryStatesResDto = {
  __typename?: 'GetMemoryStatesResDto';
  /** 错误码（失败时） */
  errorCode?: Maybe<Scalars['String']['output']>;
  /** 状态列表 */
  items: Array<MemoryStatesDto>;
  /** 页码 */
  pageIndex: Scalars['Int']['output'];
  /** 每页数 */
  pageSize: Scalars['Int']['output'];
  /** 是否成功 */
  success: Scalars['Boolean']['output'];
  /** 总记录数 */
  totalCount: Scalars['Int']['output'];
};

/** 我的排名请求 DTO */
export type GetMyRankingReqDtoInput = {
  /** 榜单类型（Combat/Performance） */
  metric: Scalars['String']['input'];
  /** 群组 id 或年级 key */
  scopeId: Scalars['String']['input'];
  /** 范围类型（Group/Grade） */
  scopeType: Scalars['String']['input'];
  /** 学科筛选 */
  subject?: InputMaybe<Scalars['String']['input']>;
};

/** 我的排名响应 DTO */
export type GetMyRankingResDto = {
  __typename?: 'GetMyRankingResDto';
  /** 错误码（失败时） */
  errorCode?: Maybe<Scalars['String']['output']>;
  /** 当前排名 */
  rank: Scalars['Int']['output'];
  /** 今日 rank − 昨日 rank（正=上升） */
  rankChange: Scalars['Int']['output'];
  /** 战绩榜开关 */
  rankEnabled: Scalars['Boolean']['output'];
  /** 是否成功 */
  success: Scalars['Boolean']['output'];
  /** 趋势（Up/Down/Flat） */
  trend: Scalars['String']['output'];
  /** 战力/战绩值 */
  value: Scalars['Decimal']['output'];
};

/** 群主看板请求 DTO */
export type GetOwnerDashboardReqDtoInput = {
  /** 群组外部键 */
  groupUid: Scalars['String']['input'];
};

/** 群主看板响应 DTO */
export type GetOwnerDashboardResDto = {
  __typename?: 'GetOwnerDashboardResDto';
  /** 平均进度（含全部分配） */
  avgProgress: Scalars['Float']['output'];
  /** 错误码（失败时） */
  errorCode?: Maybe<Scalars['String']['output']>;
  /** 逾期任务/分配数 */
  overdueCount: Scalars['Int']['output'];
  /** 是否成功 */
  success: Scalars['Boolean']['output'];
  /** 任务列表 */
  taskList: Array<DashboardTaskDto>;
  /** 今日执行率（已完成/全部分配，分母含全部） */
  todayExecutionRate: Scalars['Float']['output'];
  /** 薄弱知识点 Top5（掌握度正确率升序） */
  weakPointsTop5: Array<WeakPointDto>;
};

/** 学习报告请求 DTO */
export type GetPeriodReportReqDtoInput = {
  /** 周期（Week/Month） */
  period: Scalars['String']['input'];
};

/** 学习报告响应 DTO */
export type GetPeriodReportResDto = {
  __typename?: 'GetPeriodReportResDto';
  /** 周期正确率（个人） */
  accuracy?: Maybe<Scalars['Float']['output']>;
  /** 错误码（失败时） */
  errorCode?: Maybe<Scalars['String']['output']>;
  /** 周期学习题数 */
  learnedCount: Scalars['Int']['output'];
  /** 点亮 ★ 数 */
  starredCount: Scalars['Int']['output'];
  /** 是否成功 */
  success: Scalars['Boolean']['output'];
  /** 薄弱知识点（正确率升序） */
  weakPoints: Array<WeakPointDto>;
};

/** PK 结果请求 DTO */
export type GetPkResultReqDtoInput = {
  /** 对局外部键 */
  matchUid: Scalars['String']['input'];
};

/** PK 结果响应 DTO */
export type GetPkResultResDto = {
  __typename?: 'GetPkResultResDto';
  /** 错误码（失败时） */
  errorCode?: Maybe<Scalars['String']['output']>;
  /** 结束原因（Score/Forfeit/Timeout） */
  finishReason: Scalars['String']['output'];
  /** 双方结果（合规：无正确率对比榜） */
  players: Array<PkPlayerResultDto>;
  /** 对局状态（Finished 等） */
  status: Scalars['String']['output'];
  /** 是否成功 */
  success: Scalars['Boolean']['output'];
  /** 胜负说明 */
  winReason?: Maybe<Scalars['String']['output']>;
  /** 胜方用户 Id（NULL=平局） */
  winnerId?: Maybe<Scalars['Long']['output']>;
};

/** PK 战绩请求 DTO */
export type GetPkStatsReqDtoInput = {
  /** 用户 Uid（账户域未实施，默认当前用户） */
  userUid?: InputMaybe<Scalars['String']['input']>;
};

/** PK 战绩响应 DTO（合规：无正确率对比榜） */
export type GetPkStatsResDto = {
  __typename?: 'GetPkStatsResDto';
  /** 平 */
  draws: Scalars['Int']['output'];
  /** 错误码（失败时） */
  errorCode?: Maybe<Scalars['String']['output']>;
  /** 是否成功 */
  success: Scalars['Boolean']['output'];
  /** 总场数 */
  totalMatches: Scalars['Int']['output'];
  /** 累计分 */
  totalScore: Scalars['Int']['output'];
  /** 胜率（API 层计算） */
  winRate: Scalars['Float']['output'];
  /** 胜 */
  wins: Scalars['Int']['output'];
};

/** 进度趋势请求 DTO */
export type GetProgressReportReqDtoInput = {
  /** 周期（Week/Month） */
  period: Scalars['String']['input'];
  /** 孩子 Id */
  studentId: Scalars['Long']['input'];
};

/** 进度趋势响应 DTO（合规：无群组正确率排名） */
export type GetProgressReportResDto = {
  __typename?: 'GetProgressReportResDto';
  /** 错误码（失败时） */
  errorCode?: Maybe<Scalars['String']['output']>;
  /** 是否成功 */
  success: Scalars['Boolean']['output'];
  /** 趋势数据点（复用 DailyStatsDto） */
  trend: Array<DailyStatsDto>;
  /** 相对进步（vsLastWeek） */
  vsLastWeek?: Maybe<VsLastWeekDto>;
};

/** 榜单查询请求 DTO */
export type GetRankingsReqDtoInput = {
  /** 指定快照日期（默认最新） */
  date?: InputMaybe<Scalars['DateTime']['input']>;
  /** 返回条数上限（默认 50） */
  limit: Scalars['Int']['input'];
  /** 榜单类型（Combat 战力 / Performance 战绩） */
  metric: Scalars['String']['input'];
  /** 群组 id 或年级 key */
  scopeId: Scalars['String']['input'];
  /** 范围类型（Group/Grade） */
  scopeType: Scalars['String']['input'];
  /** 学科筛选（默认 All） */
  subject?: InputMaybe<Scalars['String']['input']>;
};

/** 榜单查询响应 DTO */
export type GetRankingsResDto = {
  __typename?: 'GetRankingsResDto';
  /** 错误码（失败时） */
  errorCode?: Maybe<Scalars['String']['output']>;
  /** 排名列表 */
  items: Array<RankingItemDto>;
  /** 该群组战绩榜是否开启 */
  rankEnabled: Scalars['Boolean']['output'];
  /** 快照日期 */
  snapshotDate: Scalars['DateTime']['output'];
  /** 是否成功 */
  success: Scalars['Boolean']['output'];
};

/** 复习队列查询请求 DTO */
export type GetReviewQueueReqDtoInput = {
  /** 查询日期 */
  date: Scalars['DateTime']['input'];
  /** 页码（默认 1） */
  pageIndex: Scalars['Int']['input'];
  /** 每页条数（1~100，默认 20） */
  pageSize: Scalars['Int']['input'];
};

/** 复习队列查询响应 DTO */
export type GetReviewQueueResDto = {
  __typename?: 'GetReviewQueueResDto';
  /** 错误码（失败时） */
  errorCode?: Maybe<Scalars['String']['output']>;
  /** 到期卡片列表（不含答案） */
  items: Array<MemoryStatesDto>;
  /** 逾期数 */
  overdueCount: Scalars['Int']['output'];
  /** 是否成功 */
  success: Scalars['Boolean']['output'];
};

/** 名单整理预览请求 DTO */
export type GetRosterPreviewReqDtoInput = {
  /** 群组 Id */
  groupId: Scalars['Long']['input'];
  /** 批次 Id */
  importId: Scalars['Long']['input'];
};

/** 会话结果请求 DTO */
export type GetSessionResultReqDtoInput = {
  /** 会话外部键 */
  sessionUid: Scalars['String']['input'];
};

/** 会话结果响应 DTO */
export type GetSessionResultResDto = {
  __typename?: 'GetSessionResultResDto';
  /** 下次重点复习（卡壳知识点） */
  blockedPoints: Array<BlockedPointDto>;
  /** 答对数 */
  correctCount: Scalars['Int']['output'];
  /** 错误码（失败时） */
  errorCode?: Maybe<Scalars['String']['output']>;
  /** 新增 ★ 数 */
  newStarCount: Scalars['Int']['output'];
  /** 是否成功 */
  success: Scalars['Boolean']['output'];
  /** 总题数 */
  totalCount: Scalars['Int']['output'];
};

/** 连续天数响应 DTO */
export type GetStreakResDto = {
  __typename?: 'GetStreakResDto';
  /** 当前连续天数（断更 0） */
  currentStreak: Scalars['Int']['output'];
  /** 错误码（失败时） */
  errorCode?: Maybe<Scalars['String']['output']>;
  /** 历史最长连续 */
  longestStreak: Scalars['Int']['output'];
  /** 是否成功 */
  success: Scalars['Boolean']['output'];
};

/** 任务详情请求 DTO */
export type GetTaskDetailReqDtoInput = {
  /** 任务外部键 */
  taskUid: Scalars['String']['input'];
};

/** 任务详情响应 DTO（外层壳：Success/ErrorCode + 内嵌实体 Dto） */
export type GetTaskDetailResDto = {
  __typename?: 'GetTaskDetailResDto';
  /** 错误码（失败时） */
  errorCode?: Maybe<Scalars['String']['output']>;
  /** 成员执行状态列表（不含正确率排名，合规） */
  members: Array<TaskAssignmentsDto>;
  /** 是否成功 */
  success: Scalars['Boolean']['output'];
  /** 任务信息（复用 TasksDto，含任务头/截止/状态等） */
  task?: Maybe<TasksDto>;
};

/** 薄弱知识点请求 DTO */
export type GetWeaknessReportReqDtoInput = {
  /** 孩子 Id */
  studentId: Scalars['Long']['input'];
};

/** 薄弱知识点响应 DTO */
export type GetWeaknessReportResDto = {
  __typename?: 'GetWeaknessReportResDto';
  /** 错误码（失败时） */
  errorCode?: Maybe<Scalars['String']['output']>;
  /** 是否成功 */
  success: Scalars['Boolean']['output'];
  /** 薄弱知识点列表（正确率升序） */
  weakPoints: Array<ParentWeakPointDto>;
};

/** 错题本查询请求（跨域共享：学习域 UC-4.7 与统计域 UC-6.4 契约形状一致） */
export type GetWrongQuestionsReqDtoInput = {
  /** 分组过滤（false=待掌握，true=已掌握） */
  mastered: Scalars['Boolean']['input'];
  /** 页码（默认 1） */
  pageIndex: Scalars['Int']['input'];
  /** 每页数（1~100，默认 20） */
  pageSize: Scalars['Int']['input'];
  /** 学科过滤（可空） */
  subject?: InputMaybe<Scalars['String']['input']>;
};

/** 错题本查询响应 DTO */
export type GetWrongQuestionsResDto = {
  __typename?: 'GetWrongQuestionsResDto';
  /** 错误码（失败时） */
  errorCode?: Maybe<Scalars['String']['output']>;
  /** 错题列表（复用 WrongQuestionsDto + KnowledgePoint 计算字段） */
  items: Array<WrongQuestionsDto>;
  /** 页码 */
  pageIndex: Scalars['Int']['output'];
  /** 每页数 */
  pageSize: Scalars['Int']['output'];
  /** 是否成功 */
  success: Scalars['Boolean']['output'];
  /** 总记录数 */
  totalCount: Scalars['Int']['output'];
};

/** 群组详情响应 DTO */
export type GroupDetailResDto = {
  __typename?: 'GroupDetailResDto';
  /** 错误码（失败时） */
  errorCode?: Maybe<Scalars['String']['output']>;
  /** 群组 Id */
  groupId: Scalars['Long']['output'];
  /** 成员列表 */
  members: Array<MemberItemDto>;
  /** 是否成功 */
  success: Scalars['Boolean']['output'];
};

/** 群组卡片项 DTO */
export type GroupListItemDto = {
  __typename?: 'GroupListItemDto';
  /** 任务执行率（依赖模块 2，切片为 0） */
  executionRate: Scalars['Int']['output'];
  /** 年级 */
  grade?: Maybe<Scalars['String']['output']>;
  /** 群组 Id */
  groupId: Scalars['Long']['output'];
  /** 成员数 */
  memberCount: Scalars['Int']['output'];
  /** 群组名称 */
  name: Scalars['String']['output'];
  /** 战绩榜开关 */
  rankEnabled: Scalars['Boolean']['output'];
  /** 学科 */
  subject: Scalars['String']['output'];
};

/** 求助档位 */
export enum HintLevel {
  /** 直接看答案（不计入记录、不迁移状态） */
  Full = 'FULL',
  /** 未求助（独立作答） */
  None = 'NONE',
  /** 部分提示（点击"请提示我"后作答） */
  Partial = 'PARTIAL'
}

/** 导入题目请求 DTO */
export type ImportQuestionsReqDtoInput = {
  /** 题库业务键 */
  bankId: Scalars['String']['input'];
  /** 上传文件（.txt/.json，WebApi 层二进制） */
  file?: InputMaybe<Array<Scalars['UnsignedByte']['input']>>;
  /** 批量粘贴文本（txt 格式 问题###答案） */
  rawText?: InputMaybe<Scalars['String']['input']>;
  /** 目标章节 */
  topic?: InputMaybe<Scalars['String']['input']>;
};

/** 导入题目响应 DTO */
export type ImportQuestionsResDto = {
  __typename?: 'ImportQuestionsResDto';
  /** 错误码（失败时） */
  errorCode?: Maybe<Scalars['String']['output']>;
  /** 失败数 */
  failed: Scalars['Int']['output'];
  /** 失败明细（行号 + 原因） */
  failures: Array<Scalars['String']['output']>;
  /** 成功导入数 */
  imported: Scalars['Int']['output'];
  /** 是否成功 */
  success: Scalars['Boolean']['output'];
};

/** 导入成员名单请求 DTO */
export type ImportRosterReqDtoInput = {
  /** 群组 Id */
  groupId: Scalars['Long']['input'];
  /** 导入方式（Paste / File） */
  method: Scalars['String']['input'];
  /** 批量粘贴内容（File 方式由 WebApi 层抽取文本后传入） */
  rawText?: InputMaybe<Scalars['String']['input']>;
};

/** 导入成员名单响应 DTO */
export type ImportRosterResDto = {
  __typename?: 'ImportRosterResDto';
  /** 错误码（失败时） */
  errorCode?: Maybe<Scalars['String']['output']>;
  /** 批次 Id */
  importId: Scalars['Long']['output'];
  /** 原始行数 */
  sourceCount: Scalars['Int']['output'];
  /** 批次状态（Processing） */
  status?: Maybe<Scalars['String']['output']>;
  /** 是否成功 */
  success: Scalars['Boolean']['output'];
};

/** 发起搭子邀请请求 DTO */
export type InviteBuddyReqDtoInput = {
  /** 被邀请人 Id（账户域 Uid→Id 映射未实施，直接 long Id） */
  inviteeUserId: Scalars['Long']['input'];
};

/** 发起搭子邀请响应 DTO */
export type InviteBuddyResDto = {
  __typename?: 'InviteBuddyResDto';
  /** 错误码（失败时） */
  errorCode?: Maybe<Scalars['String']['output']>;
  /** 过期时间（+7 天） */
  expiresAt: Scalars['DateTime']['output'];
  /** 邀请记录 Uid */
  inviteId: Scalars['String']['output'];
  /** 是否成功 */
  success: Scalars['Boolean']['output'];
};

/** 加入 PK 请求 DTO */
export type JoinPkMatchReqDtoInput = {
  /** 4 位对战码 */
  inviteCode: Scalars['String']['input'];
  /** 对局外部键 */
  matchUid: Scalars['String']['input'];
};

/** 加入 PK 响应 DTO */
export type JoinPkMatchResDto = {
  __typename?: 'JoinPkMatchResDto';
  /** 错误码（失败时） */
  errorCode?: Maybe<Scalars['String']['output']>;
  /** 对局外部键 */
  matchUid: Scalars['String']['output'];
  /** 模式（Sync/Async） */
  mode: Scalars['String']['output'];
  /** 每题时长（秒） */
  perQuestionTimeS: Scalars['Int']['output'];
  /** 题量 */
  questionCount: Scalars['Int']['output'];
  /** 是否成功 */
  success: Scalars['Boolean']['output'];
};

/** 题库列表请求 DTO */
export type ListBanksReqDtoInput = {
  /** 页码（默认 1） */
  pageIndex: Scalars['Int']['input'];
  /** 每页数（默认 20） */
  pageSize: Scalars['Int']['input'];
  /** 用途过滤（Memorize/Assess/Play） */
  purpose?: InputMaybe<Scalars['String']['input']>;
  /** 学科筛选 */
  subject?: InputMaybe<Scalars['String']['input']>;
};

/** 题库列表响应 DTO */
export type ListBanksResDto = {
  __typename?: 'ListBanksResDto';
  /** 错误码（失败时） */
  errorCode?: Maybe<Scalars['String']['output']>;
  /** 题库卡片列表 */
  items: Array<BankListItemDto>;
  /** 是否成功 */
  success: Scalars['Boolean']['output'];
  /** 总条数 */
  total: Scalars['Int']['output'];
};

/** 搭子列表响应 DTO */
export type ListBuddiesResDto = {
  __typename?: 'ListBuddiesResDto';
  /** 错误码（失败时） */
  errorCode?: Maybe<Scalars['String']['output']>;
  /** 搭子列表 */
  items: Array<BuddyListItemDto>;
  /** 是否成功 */
  success: Scalars['Boolean']['output'];
};

/** 我的孩子列表响应 DTO */
export type ListChildrenResDto = {
  __typename?: 'ListChildrenResDto';
  /** 错误码（失败时） */
  errorCode?: Maybe<Scalars['String']['output']>;
  /** 孩子列表 */
  items: Array<ChildItemDto>;
  /** 是否成功 */
  success: Scalars['Boolean']['output'];
};

/** 查询群组列表请求 DTO */
export type ListGroupsReqDtoInput = {
  /** 页码（默认 1） */
  pageIndex: Scalars['Int']['input'];
  /** 每页条数（默认 20） */
  pageSize: Scalars['Int']['input'];
};

/** 查询群组列表响应 DTO */
export type ListGroupsResDto = {
  __typename?: 'ListGroupsResDto';
  /** 错误码（失败时） */
  errorCode?: Maybe<Scalars['String']['output']>;
  /** 群组卡片列表 */
  items: Array<GroupListItemDto>;
  /** 页码 */
  pageIndex: Scalars['Int']['output'];
  /** 每页条数 */
  pageSize: Scalars['Int']['output'];
  /** 是否成功 */
  success: Scalars['Boolean']['output'];
  /** 总记录数 */
  totalCount: Scalars['Int']['output'];
};

/** 我的任务列表请求 DTO */
export type ListMyTasksReqDtoInput = {
  /** 状态过滤（Pending/InProgress/Completed/Overdue） */
  status?: InputMaybe<Scalars['String']['input']>;
};

/** 我的任务列表响应 DTO */
export type ListMyTasksResDto = {
  __typename?: 'ListMyTasksResDto';
  /** 错误码（失败时） */
  errorCode?: Maybe<Scalars['String']['output']>;
  /** 任务卡列表 */
  items: Array<MyTaskItemDto>;
  /** 是否成功 */
  success: Scalars['Boolean']['output'];
};

/** 我的订阅列表响应 DTO */
export type ListSubscriptionsResDto = {
  __typename?: 'ListSubscriptionsResDto';
  /** 错误码（失败时） */
  errorCode?: Maybe<Scalars['String']['output']>;
  /** 订阅列表 */
  items: Array<SubscriptionItemDto>;
  /** 是否成功 */
  success: Scalars['Boolean']['output'];
};

/** 任务列表请求 DTO */
export type ListTasksReqDtoInput = {
  /** 群组过滤（群主） */
  groupUid?: InputMaybe<Scalars['String']['input']>;
  /** 页码（默认 1） */
  pageIndex: Scalars['Int']['input'];
  /** 每页数（默认 20） */
  pageSize: Scalars['Int']['input'];
  /** 状态过滤（Active/Closed） */
  status?: InputMaybe<Scalars['String']['input']>;
};

/** 任务列表响应 DTO */
export type ListTasksResDto = {
  __typename?: 'ListTasksResDto';
  /** 错误码（失败时） */
  errorCode?: Maybe<Scalars['String']['output']>;
  /** 任务列表 */
  items: Array<TaskListItemDto>;
  /** 是否成功 */
  success: Scalars['Boolean']['output'];
  /** 总数 */
  total: Scalars['Int']['output'];
};

/** 统一登录上下文（GraphQL input type，由客户端 AuthContext 映射而来）。 */
export type LoginContextInput = {
  authInfo?: InputMaybe<Scalars['String']['input']>;
  authType: EnumLoginAuthType;
  credential: Scalars['String']['input'];
  deviceId?: InputMaybe<Scalars['String']['input']>;
  loginFrom: EnumLoginFrom;
  userName: Scalars['String']['input'];
};

/** 登录 / 注销结果。 */
export type LoginPayload = {
  __typename?: 'LoginPayload';
  accessToken?: Maybe<Scalars['String']['output']>;
  deviceId?: Maybe<Scalars['String']['output']>;
  displayName?: Maybe<Scalars['String']['output']>;
  expiresAt?: Maybe<Scalars['DateTime']['output']>;
  extensions?: Maybe<Array<ExtensionEntry>>;
  refreshToken?: Maybe<Scalars['String']['output']>;
  sessionKey?: Maybe<Scalars['String']['output']>;
  success: Scalars['Boolean']['output'];
  userName?: Maybe<Scalars['String']['output']>;
};

/** 成员项 DTO */
export type MemberItemDto = {
  __typename?: 'MemberItemDto';
  /** 加入时间 */
  joinedAt: Scalars['DateTime']['output'];
  /** 群内昵称 */
  nickname?: Maybe<Scalars['String']['output']>;
  /** 任务完成度（依赖模块 2，切片为 0） */
  progress: Scalars['Int']['output'];
  /** 成员角色（student/parent） */
  role: Scalars['String']['output'];
  /** 成员用户 Id */
  userId: Scalars['Long']['output'];
};

/** 四阶记忆状态 */
export enum MemoryState {
  /** 模糊（求助后才对） */
  Fuzzy = 'FUZZY',
  /** 掌握（独立答对） */
  Mastered = 'MASTERED',
  /** 未掌握（答错/未形成记忆） */
  NotMastered = 'NOT_MASTERED',
  /** 熟练（多次独立答对，长期记忆） */
  Proficient = 'PROFICIENT'
}

/** 记忆状态（状态机运行表，一人一题一行） 的手写 DTO 扩展 */
export type MemoryStatesDto = {
  __typename?: 'MemoryStatesDto';
  /** 题库业务键 */
  bankId: Scalars['String']['output'];
  /** 连续独立答对（2 次→★） */
  consecutiveCorrect: Scalars['Int']['output'];
  /** 创建时间（框架审计字段） */
  createTime: Scalars['DateTime']['output'];
  /** 预留（Phase2 SM-2） */
  easeFactor: Scalars['Float']['output'];
  /** 近 20 次正确率（间隔系数 0.5~1.5） */
  historyAccuracy: Scalars['Float']['output'];
  /** 自增主键 */
  id: Scalars['Long']['output'];
  isFromPersistentSource: Scalars['Boolean']['output'];
  /** 最近一次作答 */
  lastAttemptId?: Maybe<Scalars['Long']['output']>;
  /** 最近求助档位（None/Partial/Full） */
  lastHintLevel: HintLevel;
  /** 下次复习（驱动队列） */
  nextReviewAt: Scalars['DateTime']['output'];
  /** 题目业务键 */
  questionId: Scalars['String']['output'];
  /** 记忆状态（NotMastered/Fuzzy/Mastered/Proficient） */
  state: MemoryState;
  /** 外部业务键 */
  uId: Scalars['String']['output'];
  /** 更新时间（框架审计字段） */
  updateTime: Scalars['DateTime']['output'];
  /** 学生用户 */
  userId: Scalars['Long']['output'];
};

/**
 * GraphQL Mutation 根类型空壳。
 * 由 UseGraphQL() 自动注册为 HC 的 Mutation 根类型。
 * 所有 Mutation 通过 [ExtendObjectType] 或 [GraphQLMutation] SG 自动扩展到此类型下。
 */
export type Mutation = {
  __typename?: 'Mutation';
  changePasswordSecure?: Maybe<RegisterResult>;
  loginByContext?: Maybe<LoginPayload>;
  loginByPassword?: Maybe<LoginPayload>;
  logout?: Maybe<LoginPayload>;
};


/**
 * GraphQL Mutation 根类型空壳。
 * 由 UseGraphQL() 自动注册为 HC 的 Mutation 根类型。
 * 所有 Mutation 通过 [ExtendObjectType] 或 [GraphQLMutation] SG 自动扩展到此类型下。
 */
export type MutationChangePasswordSecureArgs = {
  input?: InputMaybe<ChangePasswordSecureInput>;
};


/**
 * GraphQL Mutation 根类型空壳。
 * 由 UseGraphQL() 自动注册为 HC 的 Mutation 根类型。
 * 所有 Mutation 通过 [ExtendObjectType] 或 [GraphQLMutation] SG 自动扩展到此类型下。
 */
export type MutationLoginByContextArgs = {
  input?: InputMaybe<LoginContextInput>;
};


/**
 * GraphQL Mutation 根类型空壳。
 * 由 UseGraphQL() 自动注册为 HC 的 Mutation 根类型。
 * 所有 Mutation 通过 [ExtendObjectType] 或 [GraphQLMutation] SG 自动扩展到此类型下。
 */
export type MutationLoginByPasswordArgs = {
  password?: InputMaybe<Scalars['String']['input']>;
  userName?: InputMaybe<Scalars['String']['input']>;
};


/**
 * GraphQL Mutation 根类型空壳。
 * 由 UseGraphQL() 自动注册为 HC 的 Mutation 根类型。
 * 所有 Mutation 通过 [ExtendObjectType] 或 [GraphQLMutation] SG 自动扩展到此类型下。
 */
export type MutationLogoutArgs = {
  broadcast: Scalars['Boolean']['input'];
};

/** 我的任务项 DTO */
export type MyTaskItemDto = {
  __typename?: 'MyTaskItemDto';
  /** 截止时间 */
  deadlineAt?: Maybe<Scalars['DateTime']['output']>;
  /** 群主名（账户域依赖，切片为空） */
  ownerName: Scalars['String']['output'];
  /** 完成度 */
  progress: Scalars['Int']['output'];
  /** 状态（Pending/InProgress/Completed/Overdue） */
  status: Scalars['String']['output'];
  /** 任务外部键 */
  taskUid: Scalars['String']['output'];
  /** 任务名 */
  title: Scalars['String']['output'];
};

/** 薄弱点 DTO（Parent 版——重命名消 GraphQL 跨域同名冲突；含学科/状态文案，与共享版形状不同） */
export type ParentWeakPointDto = {
  __typename?: 'ParentWeakPointDto';
  /** 聚合正确率 */
  accuracy: Scalars['Float']['output'];
  /** 知识点 */
  knowledgePoint: Scalars['String']['output'];
  /** 状态家长端文案（未掌握/模糊/掌握/熟练） */
  stateText: Scalars['String']['output'];
  /** 学科 */
  subject: Scalars['String']['output'];
};

/** PK 玩家结果 DTO */
export type PkPlayerResultDto = {
  __typename?: 'PkPlayerResultDto';
  /** AI 趣味点评（失败兜底文案） */
  aiComment: Scalars['String']['output'];
  /** 昵称（账户域，切片为空） */
  nickname: Scalars['String']['output'];
  /** 得分 */
  score: Scalars['Int']['output'];
  /** 总用时（毫秒） */
  totalTimeMs: Scalars['Int']['output'];
  /** 用户 Id */
  userId: Scalars['Long']['output'];
};

/**
 * GraphQL Query 根类型空壳。
 * 由 UseGraphQL() 自动注册为 HC 的 Query 根类型。
 * 所有查询通过 [ExtendObjectType] 或 [AutoQueryResolver] 自动扩展到此类型下。
 */
export type Query = {
  __typename?: 'Query';
  execute?: Maybe<ListTasksResDto>;
  execute_ByRequest?: Maybe<RemoveMemberResDto>;
  registerSecure?: Maybe<RegisterResult>;
  requestChallenge?: Maybe<ChallengeResponse>;
};


/**
 * GraphQL Query 根类型空壳。
 * 由 UseGraphQL() 自动注册为 HC 的 Query 根类型。
 * 所有查询通过 [ExtendObjectType] 或 [AutoQueryResolver] 自动扩展到此类型下。
 */
export type QueryExecuteArgs = {
  request?: InputMaybe<ListTasksReqDtoInput>;
};


/**
 * GraphQL Query 根类型空壳。
 * 由 UseGraphQL() 自动注册为 HC 的 Query 根类型。
 * 所有查询通过 [ExtendObjectType] 或 [AutoQueryResolver] 自动扩展到此类型下。
 */
export type QueryExecute_ByRequestArgs = {
  request?: InputMaybe<RemoveMemberReqDtoInput>;
};


/**
 * GraphQL Query 根类型空壳。
 * 由 UseGraphQL() 自动注册为 HC 的 Query 根类型。
 * 所有查询通过 [ExtendObjectType] 或 [AutoQueryResolver] 自动扩展到此类型下。
 */
export type QueryRegisterSecureArgs = {
  input?: InputMaybe<RegisterSecureInput>;
};


/**
 * GraphQL Query 根类型空壳。
 * 由 UseGraphQL() 自动注册为 HC 的 Query 根类型。
 * 所有查询通过 [ExtendObjectType] 或 [AutoQueryResolver] 自动扩展到此类型下。
 */
export type QueryRequestChallengeArgs = {
  userName?: InputMaybe<Scalars['String']['input']>;
};

/** 题目状态 */
export enum QuestionStatus {
  /** 启用 */
  Active = 'ACTIVE',
  /** 已下线（显示"题目已下线"） */
  Hidden = 'HIDDEN',
  /** 已改版（SupersededBy 指向新题） */
  Superseded = 'SUPERSEDED'
}

/** 题型键 */
export enum QuestionType {
  /** 单选（objective/rule exact） */
  O1 = 'O1',
  /** 多选（objective/rule set+partial） */
  O2 = 'O2',
  /** 判断（objective/rule exact） */
  O3 = 'O3',
  /** 连线（objective/rule pairs+partial） */
  O4 = 'O4',
  /** 填空（objective/hybrid keyword 主判） */
  O5 = 'O5',
  /** 补全记忆（memory/hybrid） */
  R1 = 'R1',
  /** 逆向补全（memory/hybrid） */
  R2 = 'R2',
  /** 段落默写（memory/hybrid） */
  R3A = 'R3A',
  /** 整篇默写（memory/hybrid） */
  R3B = 'R3B',
  /** 知识卡片（memory/display_only，非判题） */
  R4 = 'R4',
  /** 简答（subjective/llm） */
  X1 = 'X1',
  /** 论述（subjective/llm+人工复核） */
  X2 = 'X2',
  /** 计算（subjective/rule_llm 步骤分） */
  X3 = 'X3'
}

/** 题目索引表（API 查询索引） 的手写 DTO 扩展 */
export type QuestionsDto = {
  __typename?: 'QuestionsDto';
  /** 题库业务键（外键引用 Banks.BankId） */
  bankId: Scalars['String']['output'];
  /** 章节/单元（如 7a） */
  chapterId?: Maybe<Scalars['String']['output']>;
  /** 题目展示内容镜像（JSON，不含答案，防爬） */
  content: Scalars['String']['output'];
  /** 创建时间 */
  createTime: Scalars['DateTime']['output'];
  /** 难度 0-5 */
  difficulty: Scalars['Int']['output'];
  /** 自增主键 */
  id: Scalars['Long']['output'];
  isFromPersistentSource: Scalars['Boolean']['output'];
  /** 知识点列表（JSONB GIN 索引） */
  knowledgePoints: Array<Scalars['String']['output']>;
  /** 题型键（R1/R2/O1...） */
  qType: QuestionType;
  /** 业务键（Q-ch-7a-0001） */
  questionId: Scalars['String']['output'];
  /** 状态（Active/Superseded/Hidden） */
  status: QuestionStatus;
  /** 题目改版替换（软删除） */
  supersededBy?: Maybe<Scalars['String']['output']>;
  /** 外部业务键 */
  uId: Scalars['String']['output'];
  /** 更新时间 */
  updateTime: Scalars['DateTime']['output'];
};

/** 排名指标类型 */
export enum RankMetricType {
  /** 正确率（战绩榜） */
  Accuracy = 'ACCURACY',
  /** 掌握度（战绩榜） */
  Mastery = 'MASTERY',
  /** PK 胜场（战力榜，跨模块 PkPlayerStats） */
  PkWins = 'PK_WINS',
  /** 点亮★数（当前无榜单消费，枚举保留） */
  Stars = 'STARS',
  /** 坚持天数（战力榜） */
  Streak = 'STREAK',
  /** 背诵量（战力榜，= DailyStats.LearnedCount 累计） */
  Volume = 'VOLUME'
}

/** 排名范围类型 */
export enum RankScopeType {
  /** 年级范围 */
  Grade = 'GRADE',
  /** 群组范围 */
  Group = 'GROUP'
}

/** 排名快照（排行榜趋势数据源，一人一范围一学科一指标一日一行） 的手写 DTO 扩展 */
export type RankSnapshotsDto = {
  __typename?: 'RankSnapshotsDto';
  /** 创建时间（框架审计字段） */
  createTime: Scalars['DateTime']['output'];
  /** 自增主键 */
  id: Scalars['Long']['output'];
  isFromPersistentSource: Scalars['Boolean']['output'];
  /** 指标类型（Streak/Volume/PkWins/Accuracy/Mastery/Stars） */
  metricType: RankMetricType;
  /** 冻结的指标原始值 */
  metricValue: Scalars['Decimal']['output'];
  /** 冻结的排名（不回溯） */
  rank: Scalars['Int']['output'];
  /** group_id（群组）或 grade_key（年级） */
  scopeId?: Maybe<Scalars['String']['output']>;
  /** 范围类型（Group/Grade） */
  scopeType: RankScopeType;
  /** 快照日期 */
  snapshotDate: Scalars['LocalDate']['output'];
  /** 学科筛选维度（All/语文/…/政治） */
  subject: Scalars['String']['output'];
  /** 外部业务键（uuid，API/DTO 暴露） */
  uId: Scalars['String']['output'];
  /** 更新时间（框架审计字段） */
  updateTime: Scalars['DateTime']['output'];
  /** 用户 */
  userId: Scalars['Long']['output'];
};

/** 排名项 DTO */
export type RankingItemDto = {
  __typename?: 'RankingItemDto';
  /** 头像 URL（账户域，切片为空） */
  avatarUrl: Scalars['String']['output'];
  /** 是否本人 */
  isMe: Scalars['Boolean']['output'];
  /** 昵称（账户域，切片为空） */
  nickname: Scalars['String']['output'];
  /** 排名 */
  rank: Scalars['Int']['output'];
  /** 趋势（Up/Down/Flat） */
  trend: Scalars['String']['output'];
  /** 用户 Id */
  userId: Scalars['Long']['output'];
  /** 指标值（组合求和） */
  value: Scalars['Decimal']['output'];
};

/** 安全注册结果。 */
export type RegisterResult = {
  __typename?: 'RegisterResult';
  message?: Maybe<Scalars['String']['output']>;
  success: Scalars['Boolean']['output'];
};

/** 安全注册请求。 */
export type RegisterSecureInput = {
  clientHash: Scalars['String']['input'];
  salt: Scalars['String']['input'];
  userName: Scalars['String']['input'];
};

/** 拒绝搭子邀请请求 DTO */
export type RejectBuddyInviteReqDtoInput = {
  /** 邀请记录 Uid */
  inviteId: Scalars['String']['input'];
};

/** 拒绝搭子邀请响应 DTO */
export type RejectBuddyInviteResDto = {
  __typename?: 'RejectBuddyInviteResDto';
  /** 错误码（失败时） */
  errorCode?: Maybe<Scalars['String']['output']>;
  /** 是否成功 */
  success: Scalars['Boolean']['output'];
};

/** 解除搭子请求 DTO */
export type RemoveBuddyReqDtoInput = {
  /** 搭子关系 Uid */
  buddyId: Scalars['String']['input'];
};

/** 解除搭子响应 DTO */
export type RemoveBuddyResDto = {
  __typename?: 'RemoveBuddyResDto';
  /** 错误码（失败时） */
  errorCode?: Maybe<Scalars['String']['output']>;
  /** 是否成功 */
  success: Scalars['Boolean']['output'];
};

/** 移除成员请求 DTO */
export type RemoveMemberReqDtoInput = {
  /** 群组 Id */
  groupId: Scalars['Long']['input'];
  /** 移除目标成员用户 Id */
  userId: Scalars['Long']['input'];
};

/** 移除成员响应 DTO */
export type RemoveMemberResDto = {
  __typename?: 'RemoveMemberResDto';
  /** 错误码（失败时） */
  errorCode?: Maybe<Scalars['String']['output']>;
  /** 移除结果 */
  removed: Scalars['Boolean']['output'];
  /** 是否成功 */
  success: Scalars['Boolean']['output'];
};

/** 校验入库请求 DTO */
export type ReviewBackingPointsReqDtoInput = {
  /** 题库业务键 */
  bankId: Scalars['String']['input'];
  /** 校验批次 ID */
  batchId: Scalars['String']['input'];
  /** 逐条（通过/编辑后内容） */
  items: Array<ReviewItemInputDtoInput>;
  /** 跳过未校验确认 */
  skipUnreviewed: Scalars['Boolean']['input'];
};

/** 校验入库响应 DTO */
export type ReviewBackingPointsResDto = {
  __typename?: 'ReviewBackingPointsResDto';
  /** 错误码（失败时） */
  errorCode?: Maybe<Scalars['String']['output']>;
  /** 入库数 */
  imported: Scalars['Int']['output'];
  /** 跳过数 */
  skipped: Scalars['Int']['output'];
  /** 是否成功 */
  success: Scalars['Boolean']['output'];
};

/** 校验条目输入 DTO */
export type ReviewItemInputDtoInput = {
  /** 编辑后答案（空=用草稿原样） */
  editedAnswer?: InputMaybe<Scalars['String']['input']>;
  /** 编辑后题干（空=用草稿原样） */
  editedStem?: InputMaybe<Scalars['String']['input']>;
  /** 草稿题目业务键 */
  questionId: Scalars['String']['input'];
};

/** 名单整理预览响应 DTO */
export type RosterPreviewResDto = {
  __typename?: 'RosterPreviewResDto';
  /** 整理后有效数 */
  cleanedCount: Scalars['Int']['output'];
  /** 去重剔除数 */
  duplicateCount: Scalars['Int']['output'];
  /** 错误码（失败时） */
  errorCode?: Maybe<Scalars['String']['output']>;
  /** 批次 Id */
  importId: Scalars['Long']['output'];
  /** 非法剔除数 */
  invalidCount: Scalars['Int']['output'];
  /** 脱敏手机号列表（前缀 + 后四位） */
  preview: Array<Scalars['String']['output']>;
  /** 原始行数 */
  sourceCount: Scalars['Int']['output'];
  /** 批次状态（Ready/Processing/Failed/Exported） */
  status?: Maybe<Scalars['String']['output']>;
  /** 是否成功 */
  success: Scalars['Boolean']['output'];
};

/** 设置排名开关请求 DTO */
export type SetRankEnabledReqDtoInput = {
  /** 群组 Id */
  groupId: Scalars['Long']['input'];
  /** 开关值（默认 true） */
  rankEnabled: Scalars['Boolean']['input'];
};

/** 设置排名开关响应 DTO */
export type SetRankEnabledResDto = {
  __typename?: 'SetRankEnabledResDto';
  /** 错误码（失败时） */
  errorCode?: Maybe<Scalars['String']['output']>;
  /** 群组 Id */
  groupId: Scalars['Long']['output'];
  /** 更新后开关值 */
  rankEnabled: Scalars['Boolean']['output'];
  /** 是否成功 */
  success: Scalars['Boolean']['output'];
};

/** 开通试用请求 DTO */
export type StartTrialReqDtoInput = {
  /** 孩子 Id */
  studentId: Scalars['Long']['input'];
};

/** 开通试用响应 DTO */
export type StartTrialResDto = {
  __typename?: 'StartTrialResDto';
  /** 错误码（失败时） */
  errorCode?: Maybe<Scalars['String']['output']>;
  /** 状态（Trialing） */
  status: Scalars['String']['output'];
  /** 订阅外部键 */
  subscriptionUid: Scalars['String']['output'];
  /** 是否成功 */
  success: Scalars['Boolean']['output'];
  /** 试用结束（+7 天） */
  trialEndAt?: Maybe<Scalars['DateTime']['output']>;
};

/** 错题本查询响应 DTO（Stats 版——重命名消 GraphQL 跨域同名冲突，对齐 ControllerName=StatsWrongQuestions） */
export type StatsGetWrongQuestionsResDto = {
  __typename?: 'StatsGetWrongQuestionsResDto';
  /** 错误码（失败时） */
  errorCode?: Maybe<Scalars['String']['output']>;
  /** 错题列表（复用 WrongQuestionsDto + Summary 计算字段） */
  items: Array<WrongQuestionsDto>;
  /** 是否成功 */
  success: Scalars['Boolean']['output'];
  /** 总数 */
  total: Scalars['Int']['output'];
};

/** 学科 */
export enum Subject {
  /** 生物 */
  Biology = 'BIOLOGY',
  /** 化学 */
  Chemistry = 'CHEMISTRY',
  /** 语文（官方冷启动） */
  Chinese = 'CHINESE',
  /** 英语 */
  English = 'ENGLISH',
  /** 趣味（免费引流） */
  Fun = 'FUN',
  /** 地理 */
  Geography = 'GEOGRAPHY',
  /** 历史 */
  History = 'HISTORY',
  /** 数学（群主上传主战场） */
  Math = 'MATH',
  /** 物理 */
  Physics = 'PHYSICS',
  /** 政治 */
  Politics = 'POLITICS'
}

/** 学科掌握度 DTO */
export type SubjectMasteryDto = {
  __typename?: 'SubjectMasteryDto';
  /** 聚合正确率 */
  accuracy: Scalars['Float']['output'];
  /** 学科 */
  subject: Scalars['String']['output'];
};

/** 提交作答请求 DTO */
export type SubmitAttemptReqDtoInput = {
  /** 求助档位（None/Partial/Full） */
  hintLevel: Scalars['String']['input'];
  /** 题目业务键 */
  questionId: Scalars['String']['input'];
  /** 会话外部键 */
  sessionUid: Scalars['String']['input'];
  /** 作答耗时（毫秒，服务端可选） */
  timeCostMs?: InputMaybe<Scalars['Int']['input']>;
  /** 用户作答文本 */
  userAnswer: Scalars['String']['input'];
};

/** 提交作答响应 DTO */
export type SubmitAttemptResDto = {
  __typename?: 'SubmitAttemptResDto';
  /** 判题置信度 */
  confidence?: Maybe<Scalars['Float']['output']>;
  /** 错误码（失败时） */
  errorCode?: Maybe<Scalars['String']['output']>;
  /** 引导线索（hintLevel=Partial 时必给） */
  hint: Scalars['String']['output'];
  /** 命中关键词 */
  matchedKeywords: Array<Scalars['String']['output']>;
  /** 缺失关键词 */
  missingKeywords: Array<Scalars['String']['output']>;
  /** 下次复习时间 */
  nextReviewAt: Scalars['DateTime']['output'];
  /** 迁移后状态（MemoryState） */
  postState: Scalars['String']['output'];
  /** 迁移前状态（MemoryState） */
  preState: Scalars['String']['output'];
  /** 判题结果（Correct/Partial/Wrong） */
  result: Scalars['String']['output'];
  /** 是否成功 */
  success: Scalars['Boolean']['output'];
};

/** 判错反馈请求 DTO */
export type SubmitJudgmentFeedbackReqDtoInput = {
  /** 作答外部键（跨模块引用学习域） */
  attemptUid: Scalars['String']['input'];
  /** 反馈类型（默认 WrongJudgment） */
  feedbackType?: InputMaybe<Scalars['String']['input']>;
};

/** 判错反馈响应 DTO */
export type SubmitJudgmentFeedbackResDto = {
  __typename?: 'SubmitJudgmentFeedbackResDto';
  /** 错误码（失败时） */
  errorCode?: Maybe<Scalars['String']['output']>;
  /** 反馈外部键 */
  feedbackUid: Scalars['String']['output'];
  /** 复核状态（Pending） */
  status: Scalars['String']['output'];
  /** 是否成功 */
  success: Scalars['Boolean']['output'];
};

/** 提交 PK 作答请求 DTO */
export type SubmitPkAnswerReqDtoInput = {
  /** 对局外部键 */
  matchUid: Scalars['String']['input'];
  /** 题目业务键 */
  questionId: Scalars['String']['input'];
  /** 答题用时（毫秒，客户端上送） */
  timeCostMs: Scalars['Int']['input'];
  /** 用户答案 */
  userAnswer: Scalars['String']['input'];
};

/** 提交 PK 作答响应 DTO */
export type SubmitPkAnswerResDto = {
  __typename?: 'SubmitPkAnswerResDto';
  /** 判题置信度 */
  confidence?: Maybe<Scalars['Float']['output']>;
  /** 错误码（失败时） */
  errorCode?: Maybe<Scalars['String']['output']>;
  /** 是否答对 */
  isCorrect: Scalars['Boolean']['output'];
  /** 判题结果（Correct/Partial/Wrong） */
  result: Scalars['String']['output'];
  /** 本题得分（10/0） */
  score: Scalars['Int']['output'];
  /** 是否成功 */
  success: Scalars['Boolean']['output'];
};

/** 订阅摘要 DTO */
export type SubscriptionBriefDto = {
  __typename?: 'SubscriptionBriefDto';
  /** 状态 */
  status: Scalars['String']['output'];
  /** 试用结束 */
  trialEndAt?: Maybe<Scalars['DateTime']['output']>;
};

/** 订阅项 DTO */
export type SubscriptionItemDto = {
  __typename?: 'SubscriptionItemDto';
  /** 当前计费周期结束 */
  periodEndAt?: Maybe<Scalars['DateTime']['output']>;
  /** 方案（Month/Year） */
  plan: Scalars['String']['output'];
  /** 状态（Trialing/Active/Expired/Cancelled） */
  status: Scalars['String']['output'];
  /** 孩子昵称（账户域，切片为空） */
  studentNickname: Scalars['String']['output'];
  /** 孩子外部键 */
  studentUid: Scalars['String']['output'];
  /** 订阅外部键 */
  subscriptionUid: Scalars['String']['output'];
  /** 试用结束 */
  trialEndAt?: Maybe<Scalars['DateTime']['output']>;
};

/** 任务分配与执行（每成员一条） 的手写 DTO 扩展 */
export type TaskAssignmentsDto = {
  __typename?: 'TaskAssignmentsDto';
  /** 分配时间 */
  assignedAt: Scalars['DateTime']['output'];
  /** 全部完成时间 */
  completedAt?: Maybe<Scalars['DateTime']['output']>;
  /** 创建时间（框架审计字段） */
  createTime: Scalars['DateTime']['output'];
  /** 自增主键 */
  id: Scalars['Long']['output'];
  isFromPersistentSource: Scalars['Boolean']['output'];
  /** 完成度 0-100（已完成题数/总题数） */
  progress: Scalars['Int']['output'];
  /** 关联学习会话（执行任务时创建，跨模块） */
  sessionId?: Maybe<Scalars['Long']['output']>;
  /** 首次开始时间 */
  startedAt?: Maybe<Scalars['DateTime']['output']>;
  /** 状态（Pending/InProgress/Completed/Overdue） */
  status: AssignmentStatus;
  /** 所属任务 */
  taskId: Scalars['Long']['output'];
  /** 外部业务键 */
  uId: Scalars['String']['output'];
  /** 更新时间（框架审计字段） */
  updateTime: Scalars['DateTime']['output'];
  /** 学生 */
  userId: Scalars['Long']['output'];
};

/** 任务列表项 DTO */
export type TaskListItemDto = {
  __typename?: 'TaskListItemDto';
  /** 完成任务率（已完成/全部分配，含逾期分母） */
  completionRate: Scalars['Float']['output'];
  /** 截止时间 */
  deadlineAt?: Maybe<Scalars['DateTime']['output']>;
  /** 题目数 */
  questionCount: Scalars['Int']['output'];
  /** 状态 */
  status: Scalars['String']['output'];
  /** 任务外部键 */
  taskUid: Scalars['String']['output'];
  /** 任务名 */
  title: Scalars['String']['output'];
};

/** 任务场景（状态机耦合） */
export enum TaskScenario {
  /** 检验（feedback_only：答错降级、答对不升级） */
  Assess = 'ASSESS',
  /** 背诵（full 状态机） */
  Memorize = 'MEMORIZE'
}

/** 任务会话类型 */
export enum TaskSessionType {
  /** 组卷作答 */
  Assembled = 'ASSEMBLED',
  /** 自由背诵 */
  Free = 'FREE',
  /** 分阶（默认，按记忆状态分阶检索） */
  Progressive = 'PROGRESSIVE'
}

/** 任务状态 */
export enum TaskStatus {
  /** 进行中（已发布，学生可执行） */
  Active = 'ACTIVE',
  /** 已关闭（已截止/关闭，不可再新执行） */
  Closed = 'CLOSED',
  /** 草稿（创建未发布，本期创建即发布，枚举保留待扩展） */
  Draft = 'DRAFT'
}

/** 任务主表（群主布置的背书任务） 的手写 DTO 扩展 */
export type TasksDto = {
  __typename?: 'TasksDto';
  /** 是否允许重做（重做不改变 Completed 状态） */
  allowRedo: Scalars['Boolean']['output'];
  /** 关联题库（官方/PGC/自定义，业务键） */
  bankId?: Maybe<Scalars['String']['output']>;
  /** 创建时间 */
  createTime: Scalars['DateTime']['output'];
  /** 截止时间（NULL=无截止） */
  deadlineAt?: Maybe<Scalars['DateTime']['output']>;
  /** 任务说明 */
  description?: Maybe<Scalars['String']['output']>;
  /** 目标群组 */
  groupId: Scalars['Long']['output'];
  /** 自增主键 */
  id: Scalars['Long']['output'];
  isFromPersistentSource: Scalars['Boolean']['output'];
  /** 群主（发布者） */
  ownerId: Scalars['Long']['output'];
  /** 题目数量 */
  questionCount: Scalars['Int']['output'];
  /** 题目 ID 列表（jsonb，任务题集） */
  questionIds: Array<Scalars['String']['output']>;
  /** 场景（Memorize/Assess） */
  scenario: TaskScenario;
  /** 会话类型（Progressive/Free/Assembled） */
  sessionType: TaskSessionType;
  /** 任务开始时间 */
  startedAt: Scalars['DateTime']['output'];
  /** 状态（Draft/Active/Closed） */
  status: TaskStatus;
  /** 任务标题（≤128） */
  title: Scalars['String']['output'];
  /** 外部业务键（uuid，API/DTO 暴露） */
  uId: Scalars['String']['output'];
  /** 更新时间 */
  updateTime: Scalars['DateTime']['output'];
};

/** 知识点树节点 DTO */
export type TopicNodeDto = {
  __typename?: 'TopicNodeDto';
  /** 章节/单元 */
  chapterId: Scalars['String']['output'];
  /** 题目业务键列表 */
  questionIds: Array<Scalars['String']['output']>;
  /** 子知识点 */
  subTopics: Array<Scalars['String']['output']>;
  /** 章节标题 */
  title: Scalars['String']['output'];
};

/** 相对进步 DTO */
export type VsLastWeekDto = {
  __typename?: 'VsLastWeekDto';
  /** 多背 N 篇（本周−上周） */
  learnedDelta: Scalars['Int']['output'];
  /** 薄弱点迁移（当前薄弱点数） */
  weaknessShift: Scalars['Int']['output'];
};

/** 薄弱知识点（跨域共享：群主看板/学习报告/家长端均消费） */
export type WeakPointDto = {
  __typename?: 'WeakPointDto';
  /** 聚合正确率 */
  accuracy: Scalars['Float']['output'];
  /** 知识点 */
  knowledgePoint: Scalars['String']['output'];
};

/** 本周进度 DTO */
export type WeekProgressDto = {
  __typename?: 'WeekProgressDto';
  /** 本周正确率 */
  accuracy?: Maybe<Scalars['Float']['output']>;
  /** 本周学习题数 */
  learnedCount: Scalars['Int']['output'];
};

/** 错题本（物化派生表，一人一题一行） 的手写 DTO 扩展 */
export type WrongQuestionsDto = {
  __typename?: 'WrongQuestionsDto';
  /** 题库业务键 */
  bankId: Scalars['String']['output'];
  /** 创建时间（框架审计字段） */
  createTime: Scalars['DateTime']['output'];
  /** 自增主键 */
  id: Scalars['Long']['output'];
  isFromPersistentSource: Scalars['Boolean']['output'];
  /** 题意知识点（跨题库域注册表映射，非 SQL 列，Service 赋值） */
  knowledgePoint: Scalars['String']['output'];
  /** 最近错误时间 */
  lastWrongAt: Scalars['DateTime']['output'];
  /** 连续 2 次答对 → true */
  mastered: Scalars['Boolean']['output'];
  /** 题目业务键 */
  questionId: Scalars['String']['output'];
  /** 冗余学科字段（跨学科查询免 join） */
  subject: Scalars['String']['output'];
  /** 题目摘要（题库域关联，非 SQL 列，Service 赋值） */
  summary: Scalars['String']['output'];
  /** 外部业务键 */
  uId: Scalars['String']['output'];
  /** 更新时间（框架审计字段） */
  updateTime: Scalars['DateTime']['output'];
  /** 学生用户 */
  userId: Scalars['Long']['output'];
  /** 错误次数 */
  wrongCount: Scalars['Int']['output'];
};

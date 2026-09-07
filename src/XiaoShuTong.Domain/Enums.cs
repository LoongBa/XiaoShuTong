namespace XiaoShuTong;

// ── 群组管理域（Groups）枚举 ──────────────────────────────────
// 来源：DS01-数据结构设计-群组管理 §枚举定义
// 存储格式：string（PascalCase），实体列标注 [Column(MapType = typeof(string))]

/// <summary>群组状态</summary>
public enum GroupStatus
{
    /// <summary>启用（正常使用）</summary>
    Active = 0,

    /// <summary>归档（群组停用）</summary>
    Archived = 1,
}

/// <summary>群组成员角色</summary>
public enum MemberRole
{
    /// <summary>学生（名单预设）</summary>
    Student = 0,

    /// <summary>家长（名单预设）</summary>
    Parent = 1,
}

/// <summary>内测邀请码状态</summary>
public enum BetaCodeStatus
{
    /// <summary>待使用（平台发放未使用）</summary>
    Pending = 0,

    /// <summary>已使用（群主激活建群）</summary>
    Used = 1,

    /// <summary>已过期（超过有效期）</summary>
    Expired = 2,
}

/// <summary>一次性邀请码状态</summary>
public enum OneTimeCodeStatus
{
    /// <summary>未使用（可激活）</summary>
    Unused = 0,

    /// <summary>已使用（激活成功，不可复用）</summary>
    Used = 1,

    /// <summary>已过期（超过有效期）</summary>
    Expired = 2,
}

/// <summary>名单批次状态</summary>
public enum RosterImportStatus
{
    /// <summary>整理中（Agent 异步整理）</summary>
    Processing = 0,

    /// <summary>就绪（可生成一次性码）</summary>
    Ready = 1,

    /// <summary>已导出（CSV 已生成）</summary>
    Exported = 2,

    /// <summary>失败（整理失败）</summary>
    Failed = 3,
}

// ── 学习 Session 域（Learning）枚举 ─────────────────────────────
// 来源：DS01-数据结构设计-学习Session §枚举定义
// 存储格式：string（PascalCase），实体列标注 [Column(MapType = typeof(string))]

/// <summary>四阶记忆状态</summary>
public enum MemoryState
{
    /// <summary>未掌握（答错/未形成记忆）</summary>
    NotMastered = 0,

    /// <summary>模糊（求助后才对）</summary>
    Fuzzy = 1,

    /// <summary>掌握（独立答对）</summary>
    Mastered = 2,

    /// <summary>熟练（多次独立答对，长期记忆）</summary>
    Proficient = 3,
}

/// <summary>学习场景</summary>
public enum LearningScenario
{
    /// <summary>背记（full 状态机）</summary>
    Memorize = 0,

    /// <summary>检验（feedback_only：答错降级、答对不升级）</summary>
    Assess = 1,

    /// <summary>搭子 PK（isolated，不影响记忆状态）</summary>
    PlayPk = 2,

    /// <summary>每日趣味（isolated）</summary>
    PlayDaily = 3,
}

/// <summary>会话类型</summary>
public enum SessionType
{
    /// <summary>分阶（按记忆状态分阶检索）</summary>
    Progressive = 0,

    /// <summary>自由背诵</summary>
    Free = 1,

    /// <summary>组卷作答</summary>
    Assembled = 2,

    /// <summary>关卡通关</summary>
    Level = 3,

    /// <summary>搭子对战（场景隔离）</summary>
    Pk = 4,
}

/// <summary>判题结果</summary>
public enum JudgmentResult
{
    /// <summary>正确（答案正确）</summary>
    Correct = 0,

    /// <summary>部分正确（关键要点部分命中）</summary>
    Partial = 1,

    /// <summary>错误（答案错误）</summary>
    Wrong = 2,
}

/// <summary>求助档位</summary>
public enum HintLevel
{
    /// <summary>未求助（独立作答）</summary>
    None = 0,

    /// <summary>部分提示（点击"请提示我"后作答）</summary>
    Partial = 1,

    /// <summary>直接看答案（不计入记录、不迁移状态）</summary>
    Full = 2,
}

// ── 题库判题域（Bank/Judging）枚举 ─────────────────────────────
// 来源：DS01-数据结构设计-题库与判题 §枚举定义
// 存储格式：string（PascalCase），实体列标注 [Column(MapType = typeof(string))]

/// <summary>学科</summary>
public enum Subject
{
    /// <summary>语文（官方冷启动）</summary>
    Chinese = 0,

    /// <summary>数学（群主上传主战场）</summary>
    Math = 1,

    /// <summary>英语</summary>
    English = 2,

    /// <summary>物理</summary>
    Physics = 3,

    /// <summary>化学</summary>
    Chemistry = 4,

    /// <summary>生物</summary>
    Biology = 5,

    /// <summary>历史</summary>
    History = 6,

    /// <summary>地理</summary>
    Geography = 7,

    /// <summary>政治</summary>
    Politics = 8,

    /// <summary>趣味（免费引流）</summary>
    Fun = 9,
}

/// <summary>题库用途</summary>
public enum BankPurpose
{
    /// <summary>背诵</summary>
    Memorize = 0,

    /// <summary>检验</summary>
    Assess = 1,

    /// <summary>趣味</summary>
    Play = 2,
}

/// <summary>题库隐私</summary>
public enum BankPrivacy
{
    /// <summary>私有（默认，仅创建者可见）</summary>
    Private = 0,

    /// <summary>公开（官方/候选）</summary>
    Public = 1,

    /// <summary>群组（群组内引用）</summary>
    Group = 2,
}

/// <summary>题库状态</summary>
public enum BankStatus
{
    /// <summary>启用</summary>
    Active = 0,

    /// <summary>隐藏（下线入口）</summary>
    Hidden = 1,

    /// <summary>归档</summary>
    Archived = 2,
}

/// <summary>题型键</summary>
public enum QuestionType
{
    /// <summary>补全记忆（memory/hybrid）</summary>
    R1 = 0,

    /// <summary>逆向补全（memory/hybrid）</summary>
    R2 = 1,

    /// <summary>段落默写（memory/hybrid）</summary>
    R3a = 2,

    /// <summary>整篇默写（memory/hybrid）</summary>
    R3b = 3,

    /// <summary>知识卡片（memory/display_only，非判题）</summary>
    R4 = 4,

    /// <summary>单选（objective/rule exact）</summary>
    O1 = 5,

    /// <summary>多选（objective/rule set+partial）</summary>
    O2 = 6,

    /// <summary>判断（objective/rule exact）</summary>
    O3 = 7,

    /// <summary>连线（objective/rule pairs+partial）</summary>
    O4 = 8,

    /// <summary>填空（objective/hybrid keyword 主判）</summary>
    O5 = 9,

    /// <summary>简答（subjective/llm）</summary>
    X1 = 10,

    /// <summary>论述（subjective/llm+人工复核）</summary>
    X2 = 11,

    /// <summary>计算（subjective/rule_llm 步骤分）</summary>
    X3 = 12,
}

/// <summary>题目状态</summary>
public enum QuestionStatus
{
    /// <summary>启用</summary>
    Active = 0,

    /// <summary>已改版（SupersededBy 指向新题）</summary>
    Superseded = 1,

    /// <summary>已下线（显示"题目已下线"）</summary>
    Hidden = 2,
}

/// <summary>判错反馈类型</summary>
public enum FeedbackType
{
    /// <summary>判题有误</summary>
    WrongJudgment = 0,

    /// <summary>其他</summary>
    Other = 1,
}

/// <summary>反馈复核状态</summary>
public enum FeedbackStatus
{
    /// <summary>待复核</summary>
    Pending = 0,

    /// <summary>已复核</summary>
    Reviewed = 1,

    /// <summary>已解决</summary>
    Resolved = 2,
}

// ── 家校任务闭环域（TaskManagement）枚举 ─────────────────────────
// 来源：DS01-数据结构设计-家校任务闭环 §枚举定义
// 存储格式：string（PascalCase），实体列标注 [Column(MapType = typeof(string))]

/// <summary>任务状态</summary>
public enum TaskStatus
{
    /// <summary>草稿（创建未发布，本期创建即发布，枚举保留待扩展）</summary>
    Draft = 0,

    /// <summary>进行中（已发布，学生可执行）</summary>
    Active = 1,

    /// <summary>已关闭（已截止/关闭，不可再新执行）</summary>
    Closed = 2,
}

/// <summary>任务场景（状态机耦合）</summary>
public enum TaskScenario
{
    /// <summary>背诵（full 状态机）</summary>
    Memorize = 0,

    /// <summary>检验（feedback_only：答错降级、答对不升级）</summary>
    Assess = 1,
}

/// <summary>任务会话类型</summary>
public enum TaskSessionType
{
    /// <summary>分阶（默认，按记忆状态分阶检索）</summary>
    Progressive = 0,

    /// <summary>自由背诵</summary>
    Free = 1,

    /// <summary>组卷作答</summary>
    Assembled = 2,
}

/// <summary>任务分配状态</summary>
public enum AssignmentStatus
{
    /// <summary>待执行</summary>
    Pending = 0,

    /// <summary>进行中</summary>
    InProgress = 1,

    /// <summary>已完成</summary>
    Completed = 2,

    /// <summary>已逾期</summary>
    Overdue = 3,
}

// ── 排行榜搭子域（Rank/Buddy）枚举 ─────────────────────────────
// 来源：DS01-数据结构设计-排行榜与学习搭子 §枚举定义
// 存储格式：string（PascalCase），实体列标注 [Column(MapType = typeof(string))]

/// <summary>排名范围类型</summary>
public enum RankScopeType
{
    /// <summary>群组范围</summary>
    Group = 0,

    /// <summary>年级范围</summary>
    Grade = 1,
}

/// <summary>排名指标类型</summary>
public enum RankMetricType
{
    /// <summary>坚持天数（战力榜）</summary>
    Streak = 0,

    /// <summary>背诵量（战力榜，= DailyStats.LearnedCount 累计）</summary>
    Volume = 1,

    /// <summary>PK 胜场（战力榜，跨模块 PkPlayerStats）</summary>
    PkWins = 2,

    /// <summary>正确率（战绩榜）</summary>
    Accuracy = 3,

    /// <summary>掌握度（战绩榜）</summary>
    Mastery = 4,

    /// <summary>点亮★数（当前无榜单消费，枚举保留）</summary>
    Stars = 5,
}

/// <summary>搭子状态</summary>
public enum BuddyStatus
{
    /// <summary>邀请中（ExpiresAt 内）</summary>
    Pending = 0,

    /// <summary>已搭子（双向同意生效）</summary>
    Accepted = 1,

    /// <summary>已拒绝</summary>
    Rejected = 2,

    /// <summary>已解除</summary>
    Removed = 3,

    /// <summary>已过期（7 天未响应）</summary>
    Expired = 4,
}

// ── 搭子PK竞技域（Pk）枚举 ───────────────────────────────────
// 来源：DS01-数据结构设计-搭子PK竞技 §枚举定义（Subject 复用切片 03）
// 存储格式：string（PascalCase），实体列标注 [Column(MapType = typeof(string))]

/// <summary>PK 模式</summary>
public enum PkMode
{
    /// <summary>同步实时（WebSocket/SignalR）</summary>
    Sync = 0,

    /// <summary>异步回合制（微信 H5 无 WS 降级）</summary>
    Async = 1,
}

/// <summary>对局状态</summary>
public enum PkMatchStatus
{
    /// <summary>待加入（等待对手确认）</summary>
    Pending = 0,

    /// <summary>进行中</summary>
    Ongoing = 1,

    /// <summary>已结束（含 forfeit/timeout，本切片决策）</summary>
    Finished = 2,

    /// <summary>已取消</summary>
    Cancelled = 3,
}

/// <summary>结束原因</summary>
public enum PkFinishReason
{
    /// <summary>正常比分</summary>
    Score = 0,

    /// <summary>对手离线 30s 判弃权</summary>
    Forfeit = 1,

    /// <summary>超时结束</summary>
    Timeout = 2,
}

/// <summary>PK 判题结果（Partial 计 0 分，本切片决策）</summary>
public enum PkAttemptResult
{
    /// <summary>正确（+10 分）</summary>
    Correct = 0,

    /// <summary>部分正确（0 分，IsCorrect=false）</summary>
    Partial = 1,

    /// <summary>错误（0 分）</summary>
    Wrong = 2,
}

// ── 家长报告订阅域（Parent）枚举 ───────────────────────────────
// 来源：DS01-数据结构设计-家长成长报告 §枚举定义
// 存储格式：string（PascalCase），实体列标注 [Column(MapType = typeof(string))]

/// <summary>家长关系</summary>
public enum ParentRelation
{
    /// <summary>父母</summary>
    Parent = 0,

    /// <summary>监护人</summary>
    Guardian = 1,

    /// <summary>祖辈</summary>
    Grandparent = 2,
}

/// <summary>订阅方案</summary>
public enum SubscriptionPlan
{
    /// <summary>月付（10 元/月）</summary>
    Month = 0,

    /// <summary>年付（100 元/年）</summary>
    Year = 1,
}

/// <summary>订阅状态</summary>
public enum SubscriptionStatus
{
    /// <summary>试用中（7 天）</summary>
    Trialing = 0,

    /// <summary>已订阅</summary>
    Active = 1,

    /// <summary>已过期</summary>
    Expired = 2,

    /// <summary>已取消（权益至周期末）</summary>
    Cancelled = 3,
}

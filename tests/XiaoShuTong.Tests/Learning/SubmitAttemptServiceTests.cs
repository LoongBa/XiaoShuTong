using System.Text.Json;
using XiaoShuTong.DataServices.Bank;
using XiaoShuTong.DataServices.Learning;
using XiaoShuTong.DataServices.TaskManagement;
using XiaoShuTong.Entities.Bank;
using XiaoShuTong.Entities.Learning;
using XiaoShuTong.Entities.TaskManagement;
using XiaoShuTong.Services.Learning;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.Learning;

/// <summary>
/// UC-4.2 提交作答（SubmitAttemptService）Contract 测试 ★核心
/// 覆盖 BR：BR-06/07/10/26/27 前置与判题 | BR-11~21 四阶状态机迁移矩阵 | BR-17/18 间隔计算
/// BR-22 DailyStats | BR-23 WrongQuestions | BR-25 迁移结果
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class SubmitAttemptServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : XiaoShuTongTestBase(fixture, output)
{
    private static readonly string[] FullKeywords = ["若出其中", "星汉灿烂", "幸甚至哉", "歌以咏志"];

    private long SetUser(long id)
    {
        User.UserInfo!.Id = id;
        User.UserInfo.UserIdString = id.ToString();
        return id;
    }

    private async Task SeedQuestionAsync(string questionId, string bankId = "bank-ch-7a", string[]? keywords = null, string? hint = "首字：若")
    {
        await SeedBankAsync(bankId);
        var ds = User.Use<QuestionsDataService>();
        await ds.EntityCreateAsync(new Questions
        {
            UId = UidGenerator.NewId(),
            QuestionId = questionId,
            BankId = bankId,
            ChapterId = "7a",
            QType = QuestionType.R1,
            Content = $"{{\"questionId\":\"{questionId}\",\"stem\":\"补全：东临碣石，___\"}}",
            // KeywordGroup[] JSON 原文（Weight/Required 保留，ADR-008 决策二——判题服务端真实读取）
            Keywords = JsonSerializer.Serialize((keywords ?? FullKeywords).Select(k => new KeywordGroup([k])).ToList()),
            KnowledgePoints = ["岳阳楼记-背诵"],
            Difficulty = 0,
            Status = QuestionStatus.Active,
            Hint = hint,
        }, TestContext.Current.CancellationToken);
    }

    private async Task SeedBankAsync(string bankId)
    {
        var ds = User.Use<BanksDataService>();
        var existing = await ds.EntityGetAsync(x => x.BankId == bankId, TestContext.Current.CancellationToken);
        if (existing != null) return;
        await ds.EntityCreateAsync(new Banks
        {
            UId = UidGenerator.NewId(),
            BankId = bankId,
            Name = "判题测试题库",
            Subject = Subject.Chinese,
            Purpose = BankPurpose.Memorize,
            Privacy = BankPrivacy.Public,
            OwnerId = null,
            JsonPath = $"bank.{bankId}.json",
            Tags = [],
            Status = BankStatus.Active,
        }, TestContext.Current.CancellationToken);
    }

    private async Task<StudySessions> SeedSessionAsync(long userId, LearningScenario scenario = LearningScenario.Memorize, string bankId = "bank-ch-7a")
    {
        var ds = User.Use<StudySessionsDataService>();
        return await ds.EntityCreateAsync(new StudySessions
        {
            UId = UidGenerator.NewId(),
            UserId = userId,
            Scenario = scenario,
            BankId = bankId,
            SessionType = SessionType.Free,
            QuestionCount = 10,
            StartedAt = DateTime.UtcNow,
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>BR-24：带任务归属的会话（TaskId 非空 → Progress 派生）</summary>
    private async Task<StudySessions> SeedTaskSessionAsync(long userId, long taskId, string bankId = "bank-ch-7a")
    {
        var ds = User.Use<StudySessionsDataService>();
        return await ds.EntityCreateAsync(new StudySessions
        {
            UId = UidGenerator.NewId(),
            UserId = userId,
            Scenario = LearningScenario.Memorize,
            BankId = bankId,
            SessionType = SessionType.Progressive,
            QuestionCount = 10,
            TaskId = taskId,
            StartedAt = DateTime.UtcNow,
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>BR-24：任务种子（分母 = QuestionCount）</summary>
    private async Task<Tasks> SeedTaskAsync(long ownerId, int questionCount, string[] questionIds)
    {
        var ds = User.Use<TasksDataService>();
        return await ds.EntityCreateAsync(new Tasks
        {
            UId = UidGenerator.NewId(),
            OwnerId = ownerId,
            GroupId = 1,
            Title = "BR-24 进度测试任务",
            QuestionIds = questionIds,
            QuestionCount = questionCount,
            Scenario = TaskScenario.Memorize,
            SessionType = TaskSessionType.Progressive,
            AllowRedo = false,
            Status = TaskStatus.Active,
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>BR-24：任务分配种子（BR-05 TaskId+UserId 唯一）；consumed 参数构造漂移/历史场景（V0.6.1）</summary>
    private async Task<TaskAssignments> SeedTaskAssignmentAsync(long taskId, long userId, AssignmentStatus status = AssignmentStatus.Pending, int progress = 0, string consumed = "")
    {
        var ds = User.Use<TaskAssignmentsDataService>();
        return await ds.EntityCreateAsync(new TaskAssignments
        {
            UId = UidGenerator.NewId(),
            TaskId = taskId,
            UserId = userId,
            Status = status,
            Progress = progress,
            ConsumedQuestionIds = consumed,
            AssignedAt = DateTime.UtcNow,
        }, TestContext.Current.CancellationToken);
    }

    private async Task<TaskAssignments?> GetAssignmentAsync(long taskId, long userId)
    {
        var ds = User.Use<TaskAssignmentsDataService>();
        return await ds.EntityGetAsync(x => x.TaskId == taskId && x.UserId == userId, TestContext.Current.CancellationToken);
    }

    private async Task SeedStateAsync(long userId, string questionId, MemoryState state, int cc = 0, double accuracy = 0.8)
    {
        var ds = User.Use<MemoryStatesDataService>();
        await ds.EntityCreateAsync(new MemoryStates
        {
            UId = UidGenerator.NewId(),
            UserId = userId,
            QuestionId = questionId,
            BankId = "bank-ch-7a",
            State = state,
            ConsecutiveCorrect = cc,
            HistoryAccuracy = accuracy,
            NextReviewAt = DateTime.UtcNow.AddDays(1),
        }, TestContext.Current.CancellationToken);
    }

    private async Task<SubmitAttemptResDto> SubmitAsync(long userId, string sessionUid, string questionId, string answer, string hintLevel = "None")
    {
        var svc = User.Use<SubmitAttemptService>();
        return await svc.ExecuteAsync(new SubmitAttemptReqDto
        {
            SessionUid = sessionUid,
            QuestionId = questionId,
            UserAnswer = answer,
            HintLevel = hintLevel,
        }, TestContext.Current.CancellationToken);
    }

    private static string FullAnswer() => string.Join("，", FullKeywords);
    private const string WrongAnswer = "完全无关的回答内容";

    // ── 前置校验 ──

    /// <summary>BR-06：他人会话 → SESSION_NOT_FOUND</summary>
    [Fact]
    public async Task ExecuteAsync_OthersSession_ReturnsSessionNotFound()
    {
        var owner = SetUser(42001);
        var session = await SeedSessionAsync(owner);
        SetUser(42002); // 另一个用户

        var result = await SubmitAsync(42002, session.UId, "Q-42001", FullAnswer());

        Assert.False(result.Success);
        Assert.Equal(LearningErrorCodes.SessionNotFound, result.ErrorCode);
    }

    /// <summary>BR-07：空答案 → ANSWER_FORMAT_INVALID</summary>
    [Fact]
    public async Task ExecuteAsync_EmptyAnswer_ReturnsAnswerFormatInvalid()
    {
        var userId = SetUser(42003);
        var session = await SeedSessionAsync(userId);
        await SeedQuestionAsync("Q-42003");

        var result = await SubmitAsync(userId, session.UId, "Q-42003", "  ");

        Assert.False(result.Success);
        Assert.Equal(LearningErrorCodes.AnswerFormatInvalid, result.ErrorCode);
    }

    /// <summary>BR-26：题目不属会话题库 → QUESTION_NOT_IN_BANK</summary>
    [Fact]
    public async Task ExecuteAsync_QuestionNotInBank_ReturnsQuestionNotInBank()
    {
        var userId = SetUser(42004);
        var session = await SeedSessionAsync(userId, bankId: "bank-ch-7a");
        await SeedQuestionAsync("Q-42004", bankId: "bank-other-9x"); // 属于其他题库

        var result = await SubmitAsync(userId, session.UId, "Q-42004", FullAnswer());

        Assert.False(result.Success);
        Assert.Equal(LearningErrorCodes.QuestionNotInBank, result.ErrorCode);
    }

    /// <summary>BR-10：判题三分——全命中→Correct；3/5→Partial；1/5→Wrong</summary>
    [Fact]
    public async Task ExecuteAsync_JudgmentThreeWay_CorrectPartialWrong()
    {
        var userId = SetUser(42005);
        var session = await SeedSessionAsync(userId);
        await SeedQuestionAsync("Q-42005", keywords: FullKeywords);

        var full = await SubmitAsync(userId, session.UId, "Q-42005", FullAnswer());
        Assert.Equal("Correct", full.Result);
        Assert.NotNull(full.Confidence);

        await SeedQuestionAsync("Q-42005b", keywords: FullKeywords);
        var partial = await SubmitAsync(userId, session.UId, "Q-42005b", "若出其中 星汉灿烂 幸甚至哉");
        Assert.Equal("Partial", partial.Result);

        await SeedQuestionAsync("Q-42005c", keywords: FullKeywords);
        var wrong = await SubmitAsync(userId, session.UId, "Q-42005c", WrongAnswer);
        Assert.Equal("Wrong", wrong.Result);
    }

    // ── 状态机迁移矩阵（BR-11~21）──

    /// <summary>BR-11：新题独立答对 ✕→△</summary>
    [Fact]
    public async Task ExecuteAsync_IndependentCorrect_NotMasteredToFuzzy()
    {
        var userId = SetUser(42011);
        var session = await SeedSessionAsync(userId);
        await SeedQuestionAsync("Q-42011");

        var result = await SubmitAsync(userId, session.UId, "Q-42011", FullAnswer());

        Assert.True(result.Success);
        Assert.Equal("NotMastered", result.PreState);
        Assert.Equal("Fuzzy", result.PostState);
        Assert.NotEqual(default, result.NextReviewAt);
    }

    /// <summary>BR-11：△ 独立答对 → ○</summary>
    [Fact]
    public async Task ExecuteAsync_IndependentCorrect_FuzzyToMastered()
    {
        var userId = SetUser(42012);
        var session = await SeedSessionAsync(userId);
        await SeedQuestionAsync("Q-42012");
        await SeedStateAsync(userId, "Q-42012", MemoryState.Fuzzy);

        var result = await SubmitAsync(userId, session.UId, "Q-42012", FullAnswer());

        Assert.Equal("Fuzzy", result.PreState);
        Assert.Equal("Mastered", result.PostState);
    }

    /// <summary>BR-12：○（CC=1）再独立答对 → ★（熟练升级）</summary>
    [Fact]
    public async Task ExecuteAsync_IndependentCorrect_MasteredToProficient()
    {
        var userId = SetUser(42013);
        var session = await SeedSessionAsync(userId);
        await SeedQuestionAsync("Q-42013");
        await SeedStateAsync(userId, "Q-42013", MemoryState.Mastered, cc: 1);

        var result = await SubmitAsync(userId, session.UId, "Q-42013", FullAnswer());

        Assert.Equal("Mastered", result.PreState);
        Assert.Equal("Proficient", result.PostState);

        // MemoryStates 已更新（CC+1）
        var statesDs = User.Use<MemoryStatesDataService>();
        var state = await statesDs.EntityGetAsync(x => x.UserId == userId && x.QuestionId == "Q-42013", TestContext.Current.CancellationToken);
        Assert.NotNull(state);
        Assert.Equal(MemoryState.Proficient, state.State);
        Assert.Equal(2, state.ConsecutiveCorrect);
    }

    /// <summary>BR-13：求助后答对（Partial+Correct）→ △，CC 清零</summary>
    [Fact]
    public async Task ExecuteAsync_HintCorrect_MasteredToFuzzy()
    {
        var userId = SetUser(42014);
        var session = await SeedSessionAsync(userId);
        await SeedQuestionAsync("Q-42014");
        await SeedStateAsync(userId, "Q-42014", MemoryState.Mastered, cc: 2);

        var result = await SubmitAsync(userId, session.UId, "Q-42014", FullAnswer(), hintLevel: "Partial");

        Assert.Equal("Mastered", result.PreState);
        Assert.Equal("Fuzzy", result.PostState);

        var statesDs = User.Use<MemoryStatesDataService>();
        var state = await statesDs.EntityGetAsync(x => x.UserId == userId && x.QuestionId == "Q-42014", TestContext.Current.CancellationToken);
        Assert.NotNull(state);
        Assert.Equal(0, state.ConsecutiveCorrect);
    }

    /// <summary>BR-14：独立答错 ○→△；★→○；△→✕</summary>
    [Fact]
    public async Task ExecuteAsync_IndependentWrong_Downgrades()
    {
        var userId = SetUser(42015);
        var session = await SeedSessionAsync(userId);

        await SeedQuestionAsync("Q-42015a");
        await SeedStateAsync(userId, "Q-42015a", MemoryState.Mastered);
        var m = await SubmitAsync(userId, session.UId, "Q-42015a", WrongAnswer);
        Assert.Equal("Fuzzy", m.PostState);

        await SeedQuestionAsync("Q-42015b");
        await SeedStateAsync(userId, "Q-42015b", MemoryState.Proficient);
        var p = await SubmitAsync(userId, session.UId, "Q-42015b", WrongAnswer);
        Assert.Equal("Mastered", p.PostState);

        await SeedQuestionAsync("Q-42015c");
        await SeedStateAsync(userId, "Q-42015c", MemoryState.Fuzzy);
        var f = await SubmitAsync(userId, session.UId, "Q-42015c", WrongAnswer);
        Assert.Equal("NotMastered", f.PostState);
    }

    /// <summary>BR-16：直接看答案（Full）→ 状态不变、无 Attempts 写入</summary>
    [Fact]
    public async Task ExecuteAsync_FullHint_NoStateChange_NoAttempt()
    {
        var userId = SetUser(42016);
        var session = await SeedSessionAsync(userId);
        await SeedQuestionAsync("Q-42016");
        await SeedStateAsync(userId, "Q-42016", MemoryState.Mastered, cc: 1);

        var result = await SubmitAsync(userId, session.UId, "Q-42016", WrongAnswer, hintLevel: "Full");

        Assert.True(result.Success);
        Assert.Equal("Mastered", result.PostState); // 状态不变

        var attemptsDs = User.Use<AttemptsDataService>();
        var attempts = await attemptsDs.EntitySelectAsync(x => x.QuestionId == "Q-42016", ct: TestContext.Current.CancellationToken);
        Assert.Empty(attempts); // 无作答记录

        var statesDs = User.Use<MemoryStatesDataService>();
        var state = await statesDs.EntityGetAsync(x => x.UserId == userId && x.QuestionId == "Q-42016", TestContext.Current.CancellationToken);
        Assert.NotNull(state);
        Assert.Equal(MemoryState.Mastered, state.State); // 状态未迁移
    }

    /// <summary>BR-18：新题答错 → NextReviewAt ≈ +30min</summary>
    [Fact]
    public async Task ExecuteAsync_NewQuestionWrong_ReviewIn30Min()
    {
        var userId = SetUser(42018);
        var session = await SeedSessionAsync(userId);
        await SeedQuestionAsync("Q-42018");
        var before = DateTime.UtcNow;

        var result = await SubmitAsync(userId, session.UId, "Q-42018", WrongAnswer);

        Assert.Equal("NotMastered", result.PostState);
        Assert.InRange(result.NextReviewAt, before.AddMinutes(27), DateTime.UtcNow.AddMinutes(33));
    }

    /// <summary>BR-20：Assess 场景（feedback_only）答对不升级</summary>
    [Fact]
    public async Task ExecuteAsync_AssessScenario_CorrectNoUpgrade()
    {
        var userId = SetUser(42020);
        var session = await SeedSessionAsync(userId, scenario: LearningScenario.Assess);
        await SeedQuestionAsync("Q-42020");
        await SeedStateAsync(userId, "Q-42020", MemoryState.NotMastered);

        var result = await SubmitAsync(userId, session.UId, "Q-42020", FullAnswer());

        Assert.Equal("NotMastered", result.PreState);
        Assert.Equal("NotMastered", result.PostState); // 答对不升级
    }

    /// <summary>BR-21：Play 场景（isolated）不写 MemoryStates、不入 Attempts</summary>
    [Fact]
    public async Task ExecuteAsync_PlayScenario_Isolated()
    {
        var userId = SetUser(42021);
        var session = await SeedSessionAsync(userId, scenario: LearningScenario.PlayPk);
        await SeedQuestionAsync("Q-42021");
        await SeedStateAsync(userId, "Q-42021", MemoryState.Mastered);

        var result = await SubmitAsync(userId, session.UId, "Q-42021", FullAnswer());

        Assert.True(result.Success);
        Assert.Equal("Mastered", result.PostState); // 状态不变

        var attemptsDs = User.Use<AttemptsDataService>();
        var attempts = await attemptsDs.EntitySelectAsync(x => x.QuestionId == "Q-42021", ct: TestContext.Current.CancellationToken);
        Assert.Empty(attempts);
    }

    // ── 派生同步（BR-22/23）──

    /// <summary>BR-22：DailyStats 当日累加（LearnedCount/StarredCount）</summary>
    [Fact]
    public async Task ExecuteAsync_CorrectAnswer_UpdatesDailyStats()
    {
        var userId = SetUser(42022);
        var session = await SeedSessionAsync(userId);
        await SeedQuestionAsync("Q-42022a");
        await SeedQuestionAsync("Q-42022b");

        await SubmitAsync(userId, session.UId, "Q-42022a", FullAnswer());   // ✕→△（无★）
        await SubmitAsync(userId, session.UId, "Q-42022b", FullAnswer());   // ✕→△（无★）

        var dailyDs = User.Use<DailyStatsDataService>();
        var bizDate = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8));
        var daily = await dailyDs.EntityGetAsync(x => x.UserId == userId && x.StatDate == bizDate, TestContext.Current.CancellationToken);

        Assert.NotNull(daily);
        Assert.Equal(2, daily.LearnedCount);
        Assert.Equal(0, daily.StarredCount);
    }

    /// <summary>BR-23：答错归集 WrongQuestions；连续 2 次答对 → Mastered=true（跨会话累计，避免同会话幂等拦截）</summary>
    [Fact]
    public async Task ExecuteAsync_WrongThenTwoCorrect_Mastered()
    {
        var userId = SetUser(42023);
        await SeedQuestionAsync("Q-42023");

        // 1 次答错 → 错题 WrongCount=1（会话 A）
        var sessionA = await SeedSessionAsync(userId);
        var w1 = await SubmitAsync(userId, sessionA.UId, "Q-42023", WrongAnswer);
        Assert.Equal("Wrong", w1.Result);

        var wrongDs = User.Use<WrongQuestionsDataService>();
        var wrong = await wrongDs.EntityGetAsync(x => x.UserId == userId && x.QuestionId == "Q-42023", TestContext.Current.CancellationToken);
        Assert.NotNull(wrong);
        Assert.Equal(1, wrong.WrongCount);
        Assert.False(wrong.Mastered);

        // 连续 2 次答对（各自新会话）→ Mastered=true
        var sessionB = await SeedSessionAsync(userId);
        var sessionC = await SeedSessionAsync(userId);
        await SubmitAsync(userId, sessionB.UId, "Q-42023", FullAnswer());
        await SubmitAsync(userId, sessionC.UId, "Q-42023", FullAnswer());

        var updated = await wrongDs.EntityGetAsync(x => x.UserId == userId && x.QuestionId == "Q-42023", TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        Assert.True(updated.Mastered);
        Assert.Equal(1, updated.WrongCount); // 答对不增加 WrongCount
    }

    // ── 幂等与结果（BR-25/27）──

    /// <summary>
    /// BR-27：幂等（细化）——同会话同题**同答案**重复提交 → 返回首次结果，不重复写 Attempts；
    /// 同会话同题**改答案重试** → 放行写入新 Attempts（幂等键含 AnswerHash，idx_attempts_idem 兜底）
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_SameQuestionTwice_Idempotent()
    {
        var userId = SetUser(42027);
        var session = await SeedSessionAsync(userId);
        await SeedQuestionAsync("Q-42027");

        var first = await SubmitAsync(userId, session.UId, "Q-42027", FullAnswer());
        // 同答案重发 → 幂等返回首次结果（网络重发/双击防抖）
        var resend = await SubmitAsync(userId, session.UId, "Q-42027", FullAnswer());
        Assert.True(resend.Success);
        Assert.Equal(first.Result, resend.Result);
        Assert.Equal(first.PostState, resend.PostState);
        Assert.Equal(1, resend.AttemptCount); // T7：幂等不累加

        // 改答案重试（partial）→ 放行新记录，attemptCount 递增
        var retry = await SubmitAsync(userId, session.UId, "Q-42027", "若出其中 星汉灿烂 幸甚至哉");
        Assert.True(retry.Success);
        Assert.Equal(2, retry.AttemptCount);
        Assert.Equal("Partial", retry.Result);

        var attemptsDs = User.Use<AttemptsDataService>();
        var attempts = await attemptsDs.EntitySelectAsync(
            x => x.SessionId == session.Id && x.QuestionId == "Q-42027", ct: TestContext.Current.CancellationToken);
        Assert.Equal(2, attempts.Count); // 首次 + 重试各一条（同答案重发不重复写）
    }

    /// <summary>BR-25：响应含状态迁移结果（PreState/PostState/NextReviewAt）</summary>
    [Fact]
    public async Task ExecuteAsync_ResponseContainsMigrationResult()
    {
        var userId = SetUser(42025);
        var session = await SeedSessionAsync(userId);
        await SeedQuestionAsync("Q-42025");

        var result = await SubmitAsync(userId, session.UId, "Q-42025", FullAnswer());

        Assert.True(result.Success);
        Assert.False(string.IsNullOrEmpty(result.PreState));
        Assert.False(string.IsNullOrEmpty(result.PostState));
        Assert.True(result.NextReviewAt > DateTime.UtcNow);
        Assert.Equal(4, result.MatchedKeywords.Length); // 全命中
    }

    // ── 判题引擎接入（步骤 0：JudgingEngineService 替换 LocalJudgmentEngine）──

    /// <summary>
    /// 接入实证：0.50 命中率边界 → JudgingEngineService 判 Partial（阈值 ≥0.50，BR-35）
    /// 旧 LocalJudgmentEngine 阈值 0.60 在此边界判 Wrong——本用例锁定"接入已生效"。
    /// 同时验证 PreferLlm 降级链（平台-BR-04 无启用模型 → Degraded → 保守 Partial，BR-34）不抛异常。
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_JudgingEngineIntegrated_Boundary0050IsPartial()
    {
        var userId = SetUser(42050);
        var session = await SeedSessionAsync(userId);
        await SeedQuestionAsync("Q-42050", keywords: FullKeywords); // 4 关键词

        // 命中 2/4 = 0.50：JudgingEngine(≥0.50→Partial) vs LocalJudgmentEngine(<0.60→Wrong)
        var result = await SubmitAsync(userId, session.UId, "Q-42050", "若出其中 星汉灿烂");

        Assert.True(result.Success);
        Assert.Equal("Partial", result.Result);
        Assert.Equal(2, result.MatchedKeywords.Length);
        Assert.Equal(2, result.MissingKeywords.Length);
    }

    // ── 判题契约扩展（方案 §五/§六：needsGuidance/attemptCount/showAnswer/isDegraded）──

    /// <summary>T2：partial 第 1 次 → needsGuidance=true, showAnswer=false（提供"再试一次"）</summary>
    [Fact]
    public async Task ExecuteAsync_PartialFirstAttempt_NeedsGuidance()
    {
        var userId = SetUser(42061);
        var session = await SeedSessionAsync(userId);
        await SeedQuestionAsync("Q-42061", keywords: FullKeywords);

        var result = await SubmitAsync(userId, session.UId, "Q-42061", "若出其中 星汉灿烂 幸甚至哉"); // 3/4=0.75 → Partial

        Assert.Equal("Partial", result.Result);
        Assert.True(result.NeedsGuidance);
        Assert.False(result.ShowAnswer);
        Assert.Equal(1, result.AttemptCount);
        Assert.Equal(2, result.MaxAttempts);
    }

    /// <summary>T1：correct 无引导 → needsGuidance=false, showAnswer=false, postState 升级（BR-11 △→○）</summary>
    [Fact]
    public async Task ExecuteAsync_Correct_NoGuidance_StateUpgrades()
    {
        var userId = SetUser(42060);
        var session = await SeedSessionAsync(userId);
        await SeedQuestionAsync("Q-42060");
        await SeedStateAsync(userId, "Q-42060", MemoryState.Fuzzy); // BR-11 △→○ 升级路径

        var result = await SubmitAsync(userId, session.UId, "Q-42060", FullAnswer()); // hintLevel 默认 None

        Assert.True(result.Success);
        Assert.Equal("Correct", result.Result);
        Assert.False(result.NeedsGuidance);
        Assert.False(result.ShowAnswer);
        Assert.Equal(1, result.AttemptCount);
        Assert.Equal(2, result.MaxAttempts);
        Assert.Equal("Fuzzy", result.PreState);
        Assert.Equal("Mastered", result.PostState); // 状态升级（△→○）
    }

    /// <summary>T3：partial 达上限（attemptCount≥MaxAttempts）→ needsGuidance=false, showAnswer=true（决策表 #4，防引导死循环）</summary>
    [Fact]
    public async Task ExecuteAsync_PartialReachesLimit_ShowAnswer()
    {
        var userId = SetUser(42062);
        var session = await SeedSessionAsync(userId);
        await SeedQuestionAsync("Q-42062", keywords: FullKeywords);

        // 第 1 次 partial（3/4=0.75，与 T2 同串实证）→ 引导
        var first = await SubmitAsync(userId, session.UId, "Q-42062", "若出其中 星汉灿烂 幸甚至哉");
        Assert.Equal("Partial", first.Result);
        Assert.True(first.NeedsGuidance);
        Assert.False(first.ShowAnswer);
        Assert.Equal(1, first.AttemptCount);

        // 第 2 次 partial（另一 3/4 答案，AnswerHash 不同 → 幂等放行，attemptCount=2）→ 达上限 showAnswer=true
        var second = await SubmitAsync(userId, session.UId, "Q-42062", "若出其中 星汉灿烂 歌以咏志");
        Assert.Equal("Partial", second.Result);
        Assert.False(second.NeedsGuidance);
        Assert.True(second.ShowAnswer);
        Assert.Equal(2, second.AttemptCount);
        Assert.Equal(2, second.MaxAttempts);
    }

    /// <summary>T4：wrong 第 1 次 → needsGuidance=true, hint 非空（决策表 #5，hintLevel=None 亦带 S1 线索，BR-29 ≤20 字）</summary>
    [Fact]
    public async Task ExecuteAsync_WrongFirstAttempt_GuidanceWithHint()
    {
        var userId = SetUser(42064);
        var session = await SeedSessionAsync(userId);
        await SeedQuestionAsync("Q-42064"); // Hint 默认 "首字：若"

        var result = await SubmitAsync(userId, session.UId, "Q-42064", WrongAnswer); // hintLevel 默认 None

        Assert.True(result.Success);
        Assert.Equal("Wrong", result.Result);
        Assert.True(result.NeedsGuidance);
        Assert.False(result.ShowAnswer);
        Assert.Equal(1, result.AttemptCount);
        Assert.False(string.IsNullOrWhiteSpace(result.Hint)); // BR-29 引导分支必带线索（S1 首字/意象）
        Assert.True(result.Hint.Length <= 20); // BR-29 ≤20 字
    }

    /// <summary>Hint 去桩回归：引导分支 hint 来自真实 Questions.Hint；空 Hint → 空串（不假造 hint，前端兜底鼓励语）</summary>
    [Fact]
    public async Task ExecuteAsync_WrongFirstAttempt_EmptyHint_ReturnsEmpty()
    {
        var userId = SetUser(42069);
        var session = await SeedSessionAsync(userId);
        await SeedQuestionAsync("Q-42069", hint: null); // 存量题 Hint 可空（bank.v1.json 暂无背景钩子）

        var result = await SubmitAsync(userId, session.UId, "Q-42069", WrongAnswer); // hintLevel 默认 None

        Assert.True(result.Success);
        Assert.Equal("Wrong", result.Result);
        Assert.True(result.NeedsGuidance);
        Assert.Equal(string.Empty, result.Hint); // 空 Hint → 空串（不致命，前端兜底）
    }

    /// <summary>T5：wrong 重试达上限 → showAnswer=true（防死循环，展示答案必进下一题）</summary>
    [Fact]
    public async Task ExecuteAsync_WrongRetryReachesLimit_ShowAnswer()
    {
        var userId = SetUser(42065);
        var session = await SeedSessionAsync(userId);
        await SeedQuestionAsync("Q-42065", keywords: FullKeywords);

        // 第 1 次答错（无关内容）→ needsGuidance=true
        var first = await SubmitAsync(userId, session.UId, "Q-42065", "完全无关的第一种答案");
        Assert.Equal("Wrong", first.Result);
        Assert.True(first.NeedsGuidance);
        Assert.False(first.ShowAnswer);
        Assert.Equal(1, first.AttemptCount);

        // 第 2 次重试（另一种错误答案，幂等键不同放行）→ 达上限 showAnswer=true
        var second = await SubmitAsync(userId, session.UId, "Q-42065", "完全无关的第二种答案");
        Assert.Equal("Wrong", second.Result);
        Assert.False(second.NeedsGuidance);
        Assert.True(second.ShowAnswer);
        Assert.Equal(2, second.AttemptCount);
    }

    /// <summary>T6：hintLevel=Full（看过答案）→ showAnswer=true（对齐 BR-16 不迁移状态）</summary>
    [Fact]
    public async Task ExecuteAsync_FullHint_ShowAnswer()
    {
        var userId = SetUser(42066);
        var session = await SeedSessionAsync(userId);
        await SeedQuestionAsync("Q-42066");
        await SeedStateAsync(userId, "Q-42066", MemoryState.Mastered, cc: 1);

        var result = await SubmitAsync(userId, session.UId, "Q-42066", WrongAnswer, hintLevel: "Full");

        Assert.True(result.Success);
        Assert.True(result.ShowAnswer);
        Assert.False(result.NeedsGuidance);
        Assert.Equal("Mastered", result.PostState); // 状态不变（BR-16）
    }

    /// <summary>T8：LLM 降级 → isDegraded 透出（无启用 AI 模型 → 平台-BR-04 Degraded → 保守 Partial）</summary>
    [Fact]
    public async Task ExecuteAsync_NoAiModel_IsDegradedTrue()
    {
        var userId = SetUser(42068);
        var session = await SeedSessionAsync(userId);
        await SeedQuestionAsync("Q-42068", keywords: FullKeywords);

        // 0.50 命中率 < 0.6 → PreferLlm 触发 LLM 路径 → 无启用模型 → 降级
        var result = await SubmitAsync(userId, session.UId, "Q-42068", "若出其中 星汉灿烂");

        Assert.True(result.Success);
        Assert.True(result.IsDegraded); // 降级标记透出
        Assert.Equal("Partial", result.Result); // BR-34 保守 Partial
    }

    // ── BR-24：TaskAssignments.Progress 同步派生（ADR-010，V0.6.0） ──

    /// <summary>BR-24：任务会话首次作答 Correct → Progress 推进 + Pending→InProgress + SessionId/StartedAt 接线</summary>
    [Fact]
    public async Task ExecuteAsync_TaskAttempt_ProgressAdvancesAndInProgress()
    {
        var userId = SetUser(43001);
        await SeedBankAsync("bank-ch-7a");
        await SeedQuestionAsync("Q-43001a");
        await SeedQuestionAsync("Q-43001b");
        var task = await SeedTaskAsync(userId, questionCount: 2, questionIds: ["Q-43001a", "Q-43001b"]);
        await SeedTaskAssignmentAsync(task.Id, userId);
        var session = await SeedTaskSessionAsync(userId, task.Id);

        // 1 题 Correct（QuestionCount=2）→ 50%
        var result = await SubmitAsync(userId, session.UId, "Q-43001a", FullAnswer());

        Assert.True(result.Success);
        var assignment = await GetAssignmentAsync(task.Id, userId);
        Assert.NotNull(assignment);
        Assert.Equal(50, assignment.Progress);
        Assert.Equal(AssignmentStatus.InProgress, assignment.Status); // Pending → InProgress
        Assert.Equal(session.Id, assignment.SessionId); // 最新会话指针
        Assert.NotNull(assignment.StartedAt); // 首次推进时间
    }

    /// <summary>BR-24：全部消费 → Progress=100 + Status=Completed + CompletedAt</summary>
    [Fact]
    public async Task ExecuteAsync_TaskAllConsumed_CompletesAssignment()
    {
        var userId = SetUser(43002);
        await SeedBankAsync("bank-ch-7a");
        await SeedQuestionAsync("Q-43002a");
        await SeedQuestionAsync("Q-43002b");
        var task = await SeedTaskAsync(userId, questionCount: 2, questionIds: ["Q-43002a", "Q-43002b"]);
        await SeedTaskAssignmentAsync(task.Id, userId);
        var session = await SeedTaskSessionAsync(userId, task.Id);

        await SubmitAsync(userId, session.UId, "Q-43002a", FullAnswer()); // 50%
        await SubmitAsync(userId, session.UId, "Q-43002b", FullAnswer()); // 100%

        var assignment = await GetAssignmentAsync(task.Id, userId);
        Assert.NotNull(assignment);
        Assert.Equal(100, assignment.Progress);
        Assert.Equal(AssignmentStatus.Completed, assignment.Status);
        Assert.NotNull(assignment.CompletedAt);
    }

    /// <summary>BR-24：个人会话（TaskId=null）→ 不推进（无任务归属，SyncTaskProgress 直接 return）</summary>
    [Fact]
    public async Task ExecuteAsync_PersonalSession_DoesNotAdvanceProgress()
    {
        var userId = SetUser(43003);
        var session = await SeedSessionAsync(userId); // TaskId=null（个人会话）
        await SeedQuestionAsync("Q-43003x");
        var task = await SeedTaskAsync(userId, questionCount: 2, questionIds: ["Q-43003x", "Q-43003y"]);

        _ = await SubmitAsync(userId, session.UId, "Q-43003x", FullAnswer()); // 个人会话作答正常

        var assignment = await GetAssignmentAsync(task.Id, userId);
        Assert.Null(assignment); // 个人会话不产生任务分配（TaskId=null 早退，无副作用）
    }

    /// <summary>BR-24：Full 早退 → 不推进（BR-16 不入 Attempts）</summary>
    [Fact]
    public async Task ExecuteAsync_FullHint_DoesNotAdvanceProgress()
    {
        var userId = SetUser(43004);
        await SeedBankAsync("bank-ch-7a");
        await SeedQuestionAsync("Q-43004", keywords: FullKeywords);
        var task = await SeedTaskAsync(userId, questionCount: 1, questionIds: ["Q-43004"]);
        await SeedTaskAssignmentAsync(task.Id, userId);
        var session = await SeedTaskSessionAsync(userId, task.Id);

        await SubmitAsync(userId, session.UId, "Q-43004", WrongAnswer, hintLevel: "Full"); // Full 早退

        var assignment = await GetAssignmentAsync(task.Id, userId);
        Assert.NotNull(assignment);
        Assert.Equal(0, assignment.Progress); // 未推进
        Assert.Equal(AssignmentStatus.Pending, assignment.Status); // 保持 Pending
    }

    /// <summary>BR-24：幂等命中（同答案重发）→ 不重复推进</summary>
    [Fact]
    public async Task ExecuteAsync_IdempotentHit_DoesNotReAdvanceProgress()
    {
        var userId = SetUser(43005);
        await SeedBankAsync("bank-ch-7a");
        await SeedQuestionAsync("Q-43005", keywords: FullKeywords);
        var task = await SeedTaskAsync(userId, questionCount: 1, questionIds: ["Q-43005"]);
        await SeedTaskAssignmentAsync(task.Id, userId);
        var session = await SeedTaskSessionAsync(userId, task.Id);

        await SubmitAsync(userId, session.UId, "Q-43005", FullAnswer()); // 首次 → 100%/Completed
        await SubmitAsync(userId, session.UId, "Q-43005", FullAnswer()); // 幂等命中（同答案重发）

        var assignment = await GetAssignmentAsync(task.Id, userId);
        Assert.NotNull(assignment);
        Assert.Equal(100, assignment.Progress);
        Assert.Equal(AssignmentStatus.Completed, assignment.Status); // 不重复变化
    }

    /// <summary>BR-24：Wrong 未达上限（NeedsGuidance=true）→ 不计数（学生还在重试中）</summary>
    [Fact]
    public async Task ExecuteAsync_WrongUnderLimit_DoesNotConsume()
    {
        var userId = SetUser(43006);
        await SeedBankAsync("bank-ch-7a");
        await SeedQuestionAsync("Q-43006", keywords: FullKeywords);
        var task = await SeedTaskAsync(userId, questionCount: 1, questionIds: ["Q-43006"]);
        await SeedTaskAssignmentAsync(task.Id, userId);
        var session = await SeedTaskSessionAsync(userId, task.Id);

        await SubmitAsync(userId, session.UId, "Q-43006", WrongAnswer); // Wrong 第 1 次，未达上限

        var assignment = await GetAssignmentAsync(task.Id, userId);
        Assert.NotNull(assignment);
        Assert.Equal(0, assignment.Progress); // 未消费
        Assert.Equal(AssignmentStatus.InProgress, assignment.Status); // 首次作答已推进状态（Pending→InProgress）
    }

    /// <summary>BR-24：达上限（第 2 次作答，不同答案触发重试）→ 消费（Progress 推进）</summary>
    [Fact]
    public async Task ExecuteAsync_WrongAtLimit_Consumes()
    {
        var userId = SetUser(43007);
        await SeedBankAsync("bank-ch-7a");
        await SeedQuestionAsync("Q-43007", keywords: FullKeywords);
        var task = await SeedTaskAsync(userId, questionCount: 1, questionIds: ["Q-43007"]);
        await SeedTaskAssignmentAsync(task.Id, userId);
        var session = await SeedTaskSessionAsync(userId, task.Id);

        await SubmitAsync(userId, session.UId, "Q-43007", WrongAnswer); // 第 1 次 Wrong（未达上限）
        // 第 2 次必须用不同答案（同答案会命中 BR-27 幂等不新增 Attempt），触发重试放行
        await SubmitAsync(userId, session.UId, "Q-43007", "另一个完全错误的答案"); // 第 2 次 Wrong（达上限，showAnswer）

        var assignment = await GetAssignmentAsync(task.Id, userId);
        Assert.NotNull(assignment);
        Assert.Equal(100, assignment.Progress); // 达上限 → 消费 → 100%
        Assert.Equal(AssignmentStatus.Completed, assignment.Status);
    }

    // ── BR-24：V0.6.1 增量集合 + 完成时校验（Oracle 评审闭环 #6） ──

    /// <summary>V0.6.1 T4-1：增量累积——3 题任务跨 2 会话续做，集合正确排序序列化，33→66→100 + Completed</summary>
    [Fact]
    public async Task ExecuteAsync_IncrementalAcrossSessions_AccumulatesAndCompletes()
    {
        var userId = SetUser(43008);
        await SeedBankAsync("bank-ch-7a");
        await SeedQuestionAsync("Q-43008a");
        await SeedQuestionAsync("Q-43008b");
        await SeedQuestionAsync("Q-43008c");
        var task = await SeedTaskAsync(userId, questionCount: 3, questionIds: ["Q-43008a", "Q-43008b", "Q-43008c"]);
        await SeedTaskAssignmentAsync(task.Id, userId);
        var s1 = await SeedTaskSessionAsync(userId, task.Id);
        var s2 = await SeedTaskSessionAsync(userId, task.Id); // 续做新会话

        await SubmitAsync(userId, s1.UId, "Q-43008a", FullAnswer()); // 33%
        var a1 = await GetAssignmentAsync(task.Id, userId);
        Assert.Equal(33, a1!.Progress);
        Assert.Equal("Q-43008a", a1.ConsumedQuestionIds);

        await SubmitAsync(userId, s2.UId, "Q-43008b", FullAnswer()); // 67%（2/3 Round 进位）
        var a2 = await GetAssignmentAsync(task.Id, userId);
        Assert.Equal(67, a2!.Progress);
        Assert.Equal("Q-43008a,Q-43008b", a2.ConsumedQuestionIds); // 排序序列化

        await SubmitAsync(userId, s2.UId, "Q-43008c", FullAnswer()); // 100%
        var a3 = await GetAssignmentAsync(task.Id, userId);
        Assert.Equal(100, a3!.Progress);
        Assert.Equal(AssignmentStatus.Completed, a3.Status);
        Assert.NotNull(a3.CompletedAt);
        Assert.Equal("Q-43008a,Q-43008b,Q-43008c", a3.ConsumedQuestionIds);
    }

    /// <summary>V0.6.1 T4-2：同题重复作答（Correct 后再次 Correct，不同答案防幂等）→ 集合幂等不重复 +1</summary>
    [Fact]
    public async Task ExecuteAsync_SameQuestionRepeated_DoesNotDoubleCount()
    {
        var userId = SetUser(43009);
        await SeedBankAsync("bank-ch-7a");
        await SeedQuestionAsync("Q-43009a");
        await SeedQuestionAsync("Q-43009b");
        var task = await SeedTaskAsync(userId, questionCount: 2, questionIds: ["Q-43009a", "Q-43009b"]);
        await SeedTaskAssignmentAsync(task.Id, userId);
        var session = await SeedTaskSessionAsync(userId, task.Id);

        await SubmitAsync(userId, session.UId, "Q-43009a", FullAnswer()); // 50%
        // 同题再次 Correct（不同措辞不命中幂等）→ 集合 Contains 判定不再 +1
        await SubmitAsync(userId, session.UId, "Q-43009a", "星汉灿烂，若出其中，幸甚至哉，歌以咏志");

        var assignment = await GetAssignmentAsync(task.Id, userId);
        Assert.NotNull(assignment);
        Assert.Equal(50, assignment.Progress); // 不重复推进
        Assert.Equal("Q-43009a", assignment.ConsumedQuestionIds);
    }

    /// <summary>V0.6.1 T4-3：达 MaxAttempts 消费后再次 Correct → 不再 +1（集合幂等）</summary>
    [Fact]
    public async Task ExecuteAsync_WrongAtLimitThenCorrect_NoDoubleCount()
    {
        var userId = SetUser(43010);
        await SeedBankAsync("bank-ch-7a");
        await SeedQuestionAsync("Q-43010a");
        await SeedQuestionAsync("Q-43010b");
        var task = await SeedTaskAsync(userId, questionCount: 2, questionIds: ["Q-43010a", "Q-43010b"]);
        await SeedTaskAssignmentAsync(task.Id, userId);
        var session = await SeedTaskSessionAsync(userId, task.Id);

        await SubmitAsync(userId, session.UId, "Q-43010a", WrongAnswer); // 第1次 Wrong
        await SubmitAsync(userId, session.UId, "Q-43010a", "另一个完全错误的答案"); // 第2次 Wrong 达上限 → 消费
        var a1 = await GetAssignmentAsync(task.Id, userId);
        Assert.Equal(50, a1!.Progress);
        Assert.Equal("Q-43010a", a1.ConsumedQuestionIds);

        await SubmitAsync(userId, session.UId, "Q-43010a", FullAnswer()); // 达上限后再答 Correct → 仍不重复
        var a2 = await GetAssignmentAsync(task.Id, userId);
        Assert.Equal(50, a2!.Progress);
        Assert.Equal("Q-43010a", a2.ConsumedQuestionIds);
    }

    /// <summary>V0.6.1 T4-4：幽灵题重建——seed 集合含幽灵（Attempts 无对应）→ 近完成校验重建为真实消费集</summary>
    [Fact]
    public async Task ExecuteAsync_GhostConsumed_ReconstructedByFullCheck()
    {
        var userId = SetUser(43011);
        await SeedBankAsync("bank-ch-7a");
        await SeedQuestionAsync("Q-43011a");
        await SeedQuestionAsync("Q-43011b");
        var task = await SeedTaskAsync(userId, questionCount: 2, questionIds: ["Q-43011a", "Q-43011b"]);
        // 幽灵集合：2 个 Attempts 不存在的题 → count>=total-1 触发全量校验
        await SeedTaskAssignmentAsync(task.Id, userId, consumed: "G-ghost1,G-ghost2");
        var session = await SeedTaskSessionAsync(userId, task.Id);

        await SubmitAsync(userId, session.UId, "Q-43011a", FullAnswer()); // 触发校验 → 重建 = {Q-43011a}

        var assignment = await GetAssignmentAsync(task.Id, userId);
        Assert.NotNull(assignment);
        Assert.Equal(50, assignment.Progress); // 幽灵不计入分母（重建后仅 1 真实）
        Assert.Equal("Q-43011a", assignment.ConsumedQuestionIds); // 幽灵被清除
    }

    /// <summary>V0.6.1 T4-5：损坏集合串（尾逗号/空元素）→ ParseConsumedSet 安全降级，继续累积不抛异常</summary>
    [Fact]
    public async Task ExecuteAsync_CorruptedConsumedSet_SafelyDegrades()
    {
        var userId = SetUser(43012);
        await SeedBankAsync("bank-ch-7a");
        await SeedQuestionAsync("Q-43012a");
        await SeedQuestionAsync("Q-43012b");
        var task = await SeedTaskAsync(userId, questionCount: 2, questionIds: ["Q-43012a", "Q-43012b"]);
        // 损坏串：尾逗号/空元素（合法题 Q-43012a 保留；幽灵题会由校验重建清除，故此处只放合法题）
        await SeedTaskAssignmentAsync(task.Id, userId, consumed: ",,Q-43012a,,,");
        var session = await SeedTaskSessionAsync(userId, task.Id);

        // Q-43012a 合法题：先作答真实消费（避免幽灵）→ 集合含它
        await SubmitAsync(userId, session.UId, "Q-43012a", FullAnswer()); // 损坏串解析保留 Q-43012a → 50%

        var a1 = await GetAssignmentAsync(task.Id, userId);
        Assert.NotNull(a1);
        Assert.Equal(50, a1.Progress); // 损坏串解析成功（Q-43012a 已消费）
        Assert.Equal("Q-43012a", a1.ConsumedQuestionIds); // 规范化去空元素

        // 续作 Q-43012b → 100%（集合幂等不重复 + 累积）
        await SubmitAsync(userId, session.UId, "Q-43012b", FullAnswer());
        var a2 = await GetAssignmentAsync(task.Id, userId);
        Assert.Equal(100, a2!.Progress);
        Assert.Equal("Q-43012a,Q-43012b", a2.ConsumedQuestionIds);
    }

    /// <summary>V0.6.1 T4-6：历史数据升级兼容——空集合 + 预存旧版 Attempts（Q1 已答）→ 首次作答 Q2 触发校验收敛为全量一致</summary>
    [Fact]
    public async Task ExecuteAsync_UpgradeFromEmptySet_ConvergesViaFullCheck()
    {
        var userId = SetUser(43013);
        await SeedBankAsync("bank-ch-7a");
        await SeedQuestionAsync("Q-43013a");
        await SeedQuestionAsync("Q-43013b");
        var task = await SeedTaskAsync(userId, questionCount: 2, questionIds: ["Q-43013a", "Q-43013b"]);
        var assignment = await SeedTaskAssignmentAsync(task.Id, userId); // 空集合（默认）——模拟旧版无字段
        var session = await SeedTaskSessionAsync(userId, task.Id);

        // 模拟旧版：Q1 已消费（直接插 Attempts，空集合）
        var attemptsDs = User.Use<AttemptsDataService>();
        await attemptsDs.EntityCreateAsync(new Attempts
        {
            UId = UidGenerator.NewId(),
            UserId = userId,
            SessionId = session.Id,
            QuestionId = "Q-43013a",
            BankId = "bank-ch-7a",
            Scenario = LearningScenario.Memorize,
            QType = "R1",
            PreState = MemoryState.NotMastered,
            PostState = MemoryState.Mastered,
            Result = JudgmentResult.Correct,
            Confidence = 0.98,
            HintLevel = HintLevel.None,
            TimeCostMs = 1000,
            AnswerHash = "old-hash-a",
            AnsweredAt = DateTime.UtcNow.AddDays(-1),
        }, TestContext.Current.CancellationToken);

        // 首次作答 Q2（新版本代码）→ 集合 {Q2} count=1 >= total-1 → 全量校验重建 {Q1,Q2} → 100%
        await SubmitAsync(userId, session.UId, "Q-43013b", FullAnswer());

        var updated = await GetAssignmentAsync(task.Id, userId);
        Assert.NotNull(updated);
        Assert.Equal(100, updated.Progress); // 与全量重算一致
        Assert.Equal("Q-43013a,Q-43013b", updated.ConsumedQuestionIds); // 含旧版 Q1
        Assert.Equal(AssignmentStatus.Completed, updated.Status);
    }

    // ── V0.6.2：HistoryAccuracy 透出（走查缺口④ masteryLevel，PRD L187 间隔系数数据基础）──

    /// <summary>V0.6.2 T4-1：正常路径加权均值——同题 3 次（Correct/Partial/Wrong）→ 第 3 次透出近 20 次正确率均值 (1+0.5+0)/3=0.5</summary>
    [Fact]
    public async Task ExecuteAsync_HistoryAccuracy_WeightedAverage()
    {
        var userId = SetUser(43021);
        var session = await SeedSessionAsync(userId);
        await SeedQuestionAsync("Q-43021", keywords: FullKeywords);

        await SubmitAsync(userId, session.UId, "Q-43021", FullAnswer()); // Correct = 1.0
        await SubmitAsync(userId, session.UId, "Q-43021", "若出其中 星汉灿烂 幸甚至哉"); // Partial = 0.5
        var third = await SubmitAsync(userId, session.UId, "Q-43021", WrongAnswer); // Wrong = 0

        Assert.True(third.Success);
        Assert.Equal("Wrong", third.Result);
        Assert.Equal(0.5, third.HistoryAccuracy, 4); // (1+0.5+0)/3，Math.Round(,4) 口径
    }

    /// <summary>V0.6.2 T4-2：新题首次（无 MemoryStates）→ 中性 0.8（与 L175 间隔计算口径一致，单次样本不代表性）</summary>
    [Fact]
    public async Task ExecuteAsync_HistoryAccuracy_NewQuestionNeutral()
    {
        var userId = SetUser(43022);
        var session = await SeedSessionAsync(userId);
        await SeedQuestionAsync("Q-43022");

        var result = await SubmitAsync(userId, session.UId, "Q-43022", FullAnswer());

        Assert.True(result.Success);
        Assert.Equal("Correct", result.Result);
        Assert.Equal(0.8, result.HistoryAccuracy); // state==null → 中性 0.8（即便本次答对）
    }

    /// <summary>V0.6.2 T4-3：Full 早退路径（直接看答案）→ 透出存量 HistoryAccuracy（不迁移不更新）</summary>
    [Fact]
    public async Task ExecuteAsync_HistoryAccuracy_FullViewAnswer()
    {
        var userId = SetUser(43023);
        var session = await SeedSessionAsync(userId);
        await SeedQuestionAsync("Q-43023");
        await SeedStateAsync(userId, "Q-43023", MemoryState.Mastered, cc: 2, accuracy: 0.6);

        var result = await SubmitAsync(userId, session.UId, "Q-43023", FullAnswer(), hintLevel: "Full");

        Assert.True(result.Success);
        Assert.Equal(0.6, result.HistoryAccuracy); // 存量值透出，Full 不更新 MemoryStates
    }

    /// <summary>V0.6.2 T4-4：Play 路径（PK 场景）→ 透出存量 HistoryAccuracy（Play 不更新状态）</summary>
    [Fact]
    public async Task ExecuteAsync_HistoryAccuracy_PlayScenario()
    {
        var userId = SetUser(43024);
        var session = await SeedSessionAsync(userId, scenario: LearningScenario.PlayPk);
        await SeedQuestionAsync("Q-43024");
        await SeedStateAsync(userId, "Q-43024", MemoryState.Mastered, cc: 3, accuracy: 0.6);

        var result = await SubmitAsync(userId, session.UId, "Q-43024", FullAnswer());

        Assert.True(result.Success);
        Assert.Equal(0.6, result.HistoryAccuracy); // Play 不更新 MemoryStates → 存量值
    }

    /// <summary>V0.6.2 T4-5：连续作答收敛——4 次不同措辞但均 Correct → HistoryAccuracy 升至 1.0（均值上限；答案变体避开 BR-27 幂等键）</summary>
    [Fact]
    public async Task ExecuteAsync_HistoryAccuracy_ConvergesToPerfect()
    {
        var userId = SetUser(43025);
        var session = await SeedSessionAsync(userId);
        await SeedQuestionAsync("Q-43025");

        // 4 个 Correct 答案变体（均含全部关键词 → 判题 Correct；AnswerHash 不同 → 幂等放行）
        string[] correctVariants =
        [
            "若出其中 星汉灿烂 幸甚至哉 歌以咏志",
            "若出其中，星汉灿烂，幸甚至哉，歌以咏志",
            "若出其中 幸甚至哉 歌以咏志 星汉灿烂",
            "歌以咏志 星汉灿烂 若出其中 幸甚至哉",
        ];

        SubmitAttemptResDto? final = null;
        foreach (var answer in correctVariants)
        {
            final = await SubmitAsync(userId, session.UId, "Q-43025", answer);
            Assert.True(final.Success);
            Assert.Equal("Correct", final.Result);
        }

        // 第 4 次后：4 条全 Correct → 均值 1.0（第 4 次响应已含本次）
        Assert.Equal(1.0, final!.HistoryAccuracy);
    }
}

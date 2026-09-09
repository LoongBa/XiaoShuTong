using XiaoShuTong.DataServices.Learning;
using XiaoShuTong.Entities.Learning;
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
    : DomainXunitTestBase<XiaoShuTongUserInfo, XiaoShuTongDomainTestFixture>(fixture, output)
{
    private static readonly string[] FullKeywords = ["若出其中", "星汉灿烂", "幸甚至哉", "歌以咏志"];

    private long SetUser(long id)
    {
        User.UserInfo!.Id = id;
        User.UserInfo.UserIdString = id.ToString();
        return id;
    }

    private static void RegisterQuestion(string questionId, string bankId = "bank-ch-7a", string[]? keywords = null)
    {
        LearningQuestionRegistry.Register(new LearningQuestionMeta(
            QuestionId: questionId,
            BankId: bankId,
            Subject: "chinese",
            KnowledgePoint: "岳阳楼记-背诵",
            QType: "R1",
            AnswerKeywords: keywords ?? FullKeywords,
            Hint: "首字：若"));
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

    private async Task SeedStateAsync(long userId, string questionId, MemoryState state, int cc = 0, double accuracy = 0.8)
    {
        var ds = User.Use<MemoryStatesDataService>();
        await ds.EntityCreateAsync(new MemoryStates
        {
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
        RegisterQuestion("Q-42003");

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
        RegisterQuestion("Q-42004", bankId: "bank-other-9x"); // 属于其他题库

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
        RegisterQuestion("Q-42005", keywords: FullKeywords);

        var full = await SubmitAsync(userId, session.UId, "Q-42005", FullAnswer());
        Assert.Equal("Correct", full.Result);
        Assert.NotNull(full.Confidence);

        RegisterQuestion("Q-42005b", keywords: FullKeywords);
        var partial = await SubmitAsync(userId, session.UId, "Q-42005b", "若出其中 星汉灿烂 幸甚至哉");
        Assert.Equal("Partial", partial.Result);

        RegisterQuestion("Q-42005c", keywords: FullKeywords);
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
        RegisterQuestion("Q-42011");

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
        RegisterQuestion("Q-42012");
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
        RegisterQuestion("Q-42013");
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
        RegisterQuestion("Q-42014");
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

        RegisterQuestion("Q-42015a");
        await SeedStateAsync(userId, "Q-42015a", MemoryState.Mastered);
        var m = await SubmitAsync(userId, session.UId, "Q-42015a", WrongAnswer);
        Assert.Equal("Fuzzy", m.PostState);

        RegisterQuestion("Q-42015b");
        await SeedStateAsync(userId, "Q-42015b", MemoryState.Proficient);
        var p = await SubmitAsync(userId, session.UId, "Q-42015b", WrongAnswer);
        Assert.Equal("Mastered", p.PostState);

        RegisterQuestion("Q-42015c");
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
        RegisterQuestion("Q-42016");
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
        RegisterQuestion("Q-42018");
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
        RegisterQuestion("Q-42020");
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
        RegisterQuestion("Q-42021");
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
        RegisterQuestion("Q-42022a");
        RegisterQuestion("Q-42022b");

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
        RegisterQuestion("Q-42023");

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

    /// <summary>BR-27：同会话同题重复提交 → 返回首次结果，不重复写 Attempts</summary>
    [Fact]
    public async Task ExecuteAsync_SameQuestionTwice_Idempotent()
    {
        var userId = SetUser(42027);
        var session = await SeedSessionAsync(userId);
        RegisterQuestion("Q-42027");

        var first = await SubmitAsync(userId, session.UId, "Q-42027", FullAnswer());
        var second = await SubmitAsync(userId, session.UId, "Q-42027", WrongAnswer); // 第二次换答案

        Assert.True(second.Success);
        Assert.Equal(first.Result, second.Result); // 返回首次结果
        Assert.Equal(first.PostState, second.PostState);

        var attemptsDs = User.Use<AttemptsDataService>();
        var attempts = await attemptsDs.EntitySelectAsync(
            x => x.SessionId == session.Id && x.QuestionId == "Q-42027", ct: TestContext.Current.CancellationToken);
        Assert.Single(attempts);
    }

    /// <summary>BR-25：响应含状态迁移结果（PreState/PostState/NextReviewAt）</summary>
    [Fact]
    public async Task ExecuteAsync_ResponseContainsMigrationResult()
    {
        var userId = SetUser(42025);
        var session = await SeedSessionAsync(userId);
        RegisterQuestion("Q-42025");

        var result = await SubmitAsync(userId, session.UId, "Q-42025", FullAnswer());

        Assert.True(result.Success);
        Assert.False(string.IsNullOrEmpty(result.PreState));
        Assert.False(string.IsNullOrEmpty(result.PostState));
        Assert.True(result.NextReviewAt > DateTime.UtcNow);
        Assert.Equal(4, result.MatchedKeywords.Length); // 全命中
    }
}

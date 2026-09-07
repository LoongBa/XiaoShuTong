using XiaoShuTong.DataServices.Learning;
using XiaoShuTong.Entities.Learning;
using XiaoShuTong.Services.Learning;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.Learning;

/// <summary>
/// UC-4.1 开始学习会话（CreateStudySessionService）Contract 测试
/// 覆盖 BR：BR-01 枚举校验 | BR-03 题库校验（桩）| BR-04 题量 10/20/30/50 | BR-05 同任务续做幂等复用
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class CreateStudySessionServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : DomainXunitTestBase<XiaoShuTongUserInfo, XiaoShuTongDomainTestFixture>(fixture, output)
{
    private long SetUser(long id)
    {
        User.UserInfo!.Id = id;
        User.UserInfo.UserIdString = id.ToString();
        return id;
    }

    /// <summary>主流程：有效请求 → 建会话 + SessionUid + 题数</summary>
    [Fact]
    public async Task ExecuteAsync_ValidRequest_CreatesSession()
    {
        SetUser(41001);
        var svc = User.Use<CreateStudySessionService>();

        var result = await svc.ExecuteAsync(new CreateStudySessionReqDto
        {
            Scenario = "Memorize",
            BankId = "bank-ch-7a",
            SessionType = "Free",
            QuestionCount = 10,
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.False(string.IsNullOrEmpty(result.SessionUid));
        Assert.Equal(10, result.QuestionCount);

        var sessionsDs = User.Use<StudySessionsDataService>();
        var session = await sessionsDs.EntityGetAsync(x => x.UId == result.SessionUid, TestContext.Current.CancellationToken);
        Assert.NotNull(session);
        Assert.Equal("bank-ch-7a", session.BankId);
    }

    /// <summary>BR-01：非法枚举 → PARAM_INVALID</summary>
    [Fact]
    public async Task ExecuteAsync_InvalidScenario_ReturnsParamInvalid()
    {
        SetUser(41002);
        var svc = User.Use<CreateStudySessionService>();

        var result = await svc.ExecuteAsync(new CreateStudySessionReqDto
        {
            Scenario = "NotARealScenario",
            BankId = "bank-ch-7a",
            SessionType = "Free",
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(LearningErrorCodes.ParamInvalid, result.ErrorCode);
    }

    /// <summary>BR-01：非法 SessionType → PARAM_INVALID</summary>
    [Fact]
    public async Task ExecuteAsync_InvalidSessionType_ReturnsParamInvalid()
    {
        SetUser(41003);
        var svc = User.Use<CreateStudySessionService>();

        var result = await svc.ExecuteAsync(new CreateStudySessionReqDto
        {
            Scenario = "Memorize",
            BankId = "bank-ch-7a",
            SessionType = "BadType",
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(LearningErrorCodes.ParamInvalid, result.ErrorCode);
    }

    /// <summary>BR-04：题量 15（非 10/20/30/50）→ PARAM_INVALID</summary>
    [Fact]
    public async Task ExecuteAsync_InvalidQuestionCount_ReturnsParamInvalid()
    {
        SetUser(41004);
        var svc = User.Use<CreateStudySessionService>();

        var result = await svc.ExecuteAsync(new CreateStudySessionReqDto
        {
            Scenario = "Memorize",
            BankId = "bank-ch-7a",
            SessionType = "Free",
            QuestionCount = 15,
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(LearningErrorCodes.ParamInvalid, result.ErrorCode);
    }

    /// <summary>BR-03：空题库键 → BANK_NOT_FOUND</summary>
    [Fact]
    public async Task ExecuteAsync_EmptyBankId_ReturnsBankNotFound()
    {
        SetUser(41005);
        var svc = User.Use<CreateStudySessionService>();

        var result = await svc.ExecuteAsync(new CreateStudySessionReqDto
        {
            Scenario = "Memorize",
            BankId = "",
            SessionType = "Free",
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(LearningErrorCodes.BankNotFound, result.ErrorCode);
    }

    /// <summary>BR-05：同任务续做幂等——二次发起同任务返回原 SessionUid，不重复建</summary>
    [Fact]
    public async Task ExecuteAsync_SameTaskTwice_ReturnsSameSession()
    {
        SetUser(41006);
        var taskId = 40000006L; // 测试独立任务号
        var svc = User.Use<CreateStudySessionService>();

        var first = await svc.ExecuteAsync(new CreateStudySessionReqDto
        {
            Scenario = "Memorize",
            BankId = "bank-ch-7a",
            SessionType = "Progressive",
            TaskId = taskId,
            QuestionCount = 20,
        }, TestContext.Current.CancellationToken);
        Assert.True(first.Success);

        var second = await svc.ExecuteAsync(new CreateStudySessionReqDto
        {
            Scenario = "Memorize",
            BankId = "bank-ch-7a",
            SessionType = "Progressive",
            TaskId = taskId,
            QuestionCount = 20,
        }, TestContext.Current.CancellationToken);

        Assert.True(second.Success);
        Assert.Equal(first.SessionUid, second.SessionUid);
    }
}

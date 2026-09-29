using XiaoShuTong.DataServices.Learning;
using XiaoShuTong.Entities.Learning;
using XiaoShuTong.Services.Learning;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.Learning;

/// <summary>
/// UC-4.9 会话超时收尾（SessionTimeoutJob）Contract 测试
/// 覆盖：超时会话置 EndedAt（释放 BR-05 复用）| 进行中会话不处理 | 已收尾不重复 | 幂等
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class SessionTimeoutJobTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : XiaoShuTongTestBase(fixture, output)
{
    private async Task<StudySessions> SeedSessionAsync(long userId, DateTime startedAt, DateTime? endedAt = null)
    {
        var ds = User.Use<StudySessionsDataService>();
        return await ds.EntityCreateAsync(new StudySessions
        {
            UId = UidGenerator.NewId(),
            UserId = userId,
            Scenario = LearningScenario.Memorize,
            BankId = "bank-ch-7a",
            SessionType = SessionType.Free,
            QuestionCount = 10,
            TaskId = null,
            StartedAt = startedAt,
            EndedAt = endedAt,
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>主流程：超时会话（StartedAt 超 24h）→ EndedAt 置值（StartedAt+24h 业务时间语义）</summary>
    [Fact]
    public async Task ExecuteAsync_StaleSession_SetsEndedAt()
    {
        var startedAt = DateTime.UtcNow.AddHours(-25); // 超时 25h
        var session = await SeedSessionAsync(49001, startedAt);
        var job = User.Use<SessionTimeoutJob>();

        await job.ExecuteAsync(TestContext.Current.CancellationToken);

        var ds = User.Use<StudySessionsDataService>();
        var updated = await ds.EntityGetAsync(x => x.Id == session.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        Assert.NotNull(updated.EndedAt);
        // M1-A：EndedAt = StartedAt+24h（业务时间语义，非扫描时刻）
        Assert.Equal(startedAt.AddHours(24), updated.EndedAt);
    }

    /// <summary>进行中会话（StartedAt 24h 内）→ 不处理（EndedAt 保持 null，BR-05 复用不破坏）</summary>
    [Fact]
    public async Task ExecuteAsync_ActiveSession_NotTouched()
    {
        var session = await SeedSessionAsync(49002, DateTime.UtcNow.AddHours(-1));
        var job = User.Use<SessionTimeoutJob>();

        await job.ExecuteAsync(TestContext.Current.CancellationToken);

        var ds = User.Use<StudySessionsDataService>();
        var updated = await ds.EntityGetAsync(x => x.Id == session.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        Assert.Null(updated.EndedAt); // 进行中会话保持
    }

    /// <summary>已收尾会话（EndedAt 有值）→ 不重复处理（幂等，不覆盖原收尾值）</summary>
    [Fact]
    public async Task ExecuteAsync_AlreadyEnded_NotOverwritten()
    {
        var endedAt = DateTime.UtcNow.AddHours(-1); // 正常收尾（用户主动）
        var session = await SeedSessionAsync(49003, DateTime.UtcNow.AddHours(-30), endedAt);
        var job = User.Use<SessionTimeoutJob>();

        await job.ExecuteAsync(TestContext.Current.CancellationToken);

        var ds = User.Use<StudySessionsDataService>();
        var updated = await ds.EntityGetAsync(x => x.Id == session.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        Assert.Equal(endedAt, updated.EndedAt); // 未被 Job 覆盖（用户主动收尾优先）
    }

    /// <summary>幂等：重复执行只扫未收尾会话，已置值的不重复处理</summary>
    [Fact]
    public async Task ExecuteAsync_Rerun_Idempotent()
    {
        var startedAt = DateTime.UtcNow.AddHours(-25);
        await SeedSessionAsync(49004, startedAt);
        var job = User.Use<SessionTimeoutJob>();

        await job.ExecuteAsync(TestContext.Current.CancellationToken);
        await job.ExecuteAsync(TestContext.Current.CancellationToken); // 二次

        var ds = User.Use<StudySessionsDataService>();
        var rows = await ds.EntitySelectAsync(x => x.UserId == 49004, ct: TestContext.Current.CancellationToken);
        var row = Assert.Single(rows);
        Assert.Equal(startedAt.AddHours(24), row.EndedAt); // 值稳定（未被二次覆盖为扫描时刻）
    }
}

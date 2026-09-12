using XiaoShuTong.DataServices.Learning;
using XiaoShuTong.Entities.Learning;
using XiaoShuTong.Services.Learning;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.Learning;

/// <summary>
/// UC-4.5 记忆状态查询（GetMemoryStatesService）Contract 测试
/// 覆盖 BR：BR-36 仅当前用户（RLS）| BR-37 过滤条件（bankId/state）可空组合 | BR-38 分页参数校验（page≥1 归一、size 1~100）
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class GetMemoryStatesServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : XiaoShuTongTestBase(fixture, output)
{
    private long SetUser(long id)
    {
        User.UserInfo!.Id = id;
        User.UserInfo.UserIdString = id.ToString();
        return id;
    }

    private async Task SeedStateAsync(long userId, string questionId, string bankId, MemoryState state, DateTime nextReviewAt, double accuracy = 0.8)
    {
        var ds = User.Use<MemoryStatesDataService>();
        await ds.EntityCreateAsync(new MemoryStates
        {
            UId = UidGenerator.NewId(),
            UserId = userId,
            QuestionId = questionId,
            BankId = bankId,
            State = state,
            ConsecutiveCorrect = 0,
            HistoryAccuracy = accuracy,
            NextReviewAt = nextReviewAt,
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>主流程 + BR-37：空条件 → 全量分页返回（按 NextReviewAt 倒序）</summary>
    [Fact]
    public async Task ExecuteAsync_NoFilter_ReturnsAllPaged()
    {
        var userId = SetUser(54001);
        var now = DateTime.UtcNow;
        await SeedStateAsync(userId, "Q-54001a", "bank-ch-54001", MemoryState.NotMastered, now.AddDays(1));
        await SeedStateAsync(userId, "Q-54001b", "bank-ch-54001", MemoryState.Mastered, now.AddDays(3));
        await SeedStateAsync(userId, "Q-54001c", "bank-ma-54001", MemoryState.Fuzzy, now.AddDays(2));
        var svc = User.Use<GetMemoryStatesService>();

        var result = await svc.ExecuteAsync(new GetMemoryStatesReqDto(), TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(3, result.Items.Count);
        // NextReviewAt 倒序：b(3天) → c(2天) → a(1天)
        Assert.Equal("Q-54001b", result.Items[0].QuestionId);
        Assert.Equal("Q-54001c", result.Items[1].QuestionId);
        Assert.Equal("Q-54001a", result.Items[2].QuestionId);
    }

    /// <summary>BR-36：仅返回当前用户状态（RLS，他人数据不可见）</summary>
    [Fact]
    public async Task ExecuteAsync_OnlyOwnStates()
    {
        var userId = SetUser(54002);
        var now = DateTime.UtcNow;
        await SeedStateAsync(userId, "Q-54002a", "bank-ch-54002", MemoryState.Mastered, now.AddDays(1));
        await SeedStateAsync(54999, "Q-54002b", "bank-ch-54002", MemoryState.NotMastered, now.AddDays(1)); // 他人
        var svc = User.Use<GetMemoryStatesService>();

        var result = await svc.ExecuteAsync(new GetMemoryStatesReqDto(), TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Single(result.Items);
        Assert.Equal("Q-54002a", result.Items[0].QuestionId);
    }

    /// <summary>BR-37：按 BankId 过滤</summary>
    [Fact]
    public async Task ExecuteAsync_FilterByBankId()
    {
        var userId = SetUser(54003);
        var now = DateTime.UtcNow;
        await SeedStateAsync(userId, "Q-54003a", "bank-ch-54003", MemoryState.Mastered, now.AddDays(1));
        await SeedStateAsync(userId, "Q-54003b", "bank-ma-54003", MemoryState.Fuzzy, now.AddDays(1));
        var svc = User.Use<GetMemoryStatesService>();

        var result = await svc.ExecuteAsync(new GetMemoryStatesReqDto { BankId = "bank-ch-54003" }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Single(result.Items);
        Assert.Equal("Q-54003a", result.Items[0].QuestionId);
    }

    /// <summary>BR-37：按 State 过滤</summary>
    [Fact]
    public async Task ExecuteAsync_FilterByState()
    {
        var userId = SetUser(54004);
        var now = DateTime.UtcNow;
        await SeedStateAsync(userId, "Q-54004a", "bank-ch-54004", MemoryState.Mastered, now.AddDays(1));
        await SeedStateAsync(userId, "Q-54004b", "bank-ch-54004", MemoryState.NotMastered, now.AddDays(1));
        var svc = User.Use<GetMemoryStatesService>();

        var result = await svc.ExecuteAsync(new GetMemoryStatesReqDto { State = MemoryState.NotMastered }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Single(result.Items);
        Assert.Equal("Q-54004b", result.Items[0].QuestionId);
    }

    /// <summary>主流程：分页生效（PageSize=1 → 仅第一页，TotalCount 仍为 2）</summary>
    [Fact]
    public async Task ExecuteAsync_Paging_ReturnsOnlyPage()
    {
        var userId = SetUser(54005);
        var now = DateTime.UtcNow;
        await SeedStateAsync(userId, "Q-54005a", "bank-ch-54005", MemoryState.Mastered, now.AddDays(1));
        await SeedStateAsync(userId, "Q-54005b", "bank-ch-54005", MemoryState.Mastered, now.AddDays(2));
        var svc = User.Use<GetMemoryStatesService>();

        var result = await svc.ExecuteAsync(new GetMemoryStatesReqDto { PageIndex = 1, PageSize = 1 }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(2, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal("Q-54005b", result.Items[0].QuestionId); // NextReviewAt 倒序第一
    }

    /// <summary>BR-38：PageSize=0 → 1002</summary>
    [Fact]
    public async Task ExecuteAsync_InvalidPageSize_ReturnsParamInvalid()
    {
        SetUser(54006);
        var svc = User.Use<GetMemoryStatesService>();

        var result = await svc.ExecuteAsync(new GetMemoryStatesReqDto { PageSize = 0 }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(LearningErrorCodes.ParamInvalid, result.ErrorCode);
    }

    /// <summary>BR-38：PageSize>100 → 1002</summary>
    [Fact]
    public async Task ExecuteAsync_OverPageSizeLimit_ReturnsParamInvalid()
    {
        SetUser(54007);
        var svc = User.Use<GetMemoryStatesService>();

        var result = await svc.ExecuteAsync(new GetMemoryStatesReqDto { PageSize = 101 }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(LearningErrorCodes.ParamInvalid, result.ErrorCode);
    }

    /// <summary>BR-38：PageIndex<1 → 归一为 1（成功，不报错）</summary>
    [Fact]
    public async Task ExecuteAsync_ZeroPageIndex_NormalizesTo1()
    {
        SetUser(54008);
        var svc = User.Use<GetMemoryStatesService>();

        var result = await svc.ExecuteAsync(new GetMemoryStatesReqDto { PageIndex = 0, PageSize = 20 }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(1, result.PageIndex);
    }
}

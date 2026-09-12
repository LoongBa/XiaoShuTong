using XiaoShuTong.DataServices.Learning;
using XiaoShuTong.Entities.Learning;
using XiaoShuTong.Services.Learning;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.Learning;

/// <summary>
/// UC-4.4 复习队列查询（GetReviewQueueService）Contract 测试
/// 覆盖 BR：BR-31 空队列正常返回 | BR-32 过滤 Proficient | BR-33 逾期置顶 + △ 优先 ○ | BR-35 参数校验
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class GetReviewQueueServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : XiaoShuTongTestBase(fixture, output)
{
    private long SetUser(long id)
    {
        User.UserInfo!.Id = id;
        User.UserInfo.UserIdString = id.ToString();
        return id;
    }

    private async Task SeedStateAsync(long userId, string questionId, MemoryState state, DateTime nextReviewAt)
    {
        var ds = User.Use<MemoryStatesDataService>();
        await ds.EntityCreateAsync(new MemoryStates
        {
            UId = UidGenerator.NewId(),
            UserId = userId,
            QuestionId = questionId,
            BankId = "bank-ch-7a",
            State = state,
            ConsecutiveCorrect = 0,
            HistoryAccuracy = 0.8,
            NextReviewAt = nextReviewAt,
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>BR-31：空队列正常返回（非错误）</summary>
    [Fact]
    public async Task ExecuteAsync_NoDueItems_ReturnsEmptyQueue()
    {
        SetUser(44001);
        var svc = User.Use<GetReviewQueueService>();

        var result = await svc.ExecuteAsync(new GetReviewQueueReqDto
        {
            Date = DateTime.UtcNow,
            PageIndex = 1,
            PageSize = 20,
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.OverdueCount);
    }

    /// <summary>BR-32：Proficient 到期不出现；NotMastered 到期出现</summary>
    [Fact]
    public async Task ExecuteAsync_ProficientExcluded_DueIncluded()
    {
        var userId = SetUser(44002);
        var now = DateTime.UtcNow;
        await SeedStateAsync(userId, "Q-44002a", MemoryState.Proficient, now.AddMinutes(-1)); // ★ 到期 → 排除
        await SeedStateAsync(userId, "Q-44002b", MemoryState.NotMastered, now.AddMinutes(-1)); // ✕ 到期 → 出现
        var svc = User.Use<GetReviewQueueService>();

        var result = await svc.ExecuteAsync(new GetReviewQueueReqDto { Date = now }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.DoesNotContain(result.Items, i => i.QuestionId == "Q-44002a");
        Assert.Contains(result.Items, i => i.QuestionId == "Q-44002b" && i.State == MemoryState.NotMastered);
    }

    /// <summary>BR-33：逾期置顶（逾期题排在前）+ △ 优先于 ○</summary>
    [Fact]
    public async Task ExecuteAsync_OverdueFirst_ThenStatePriority()
    {
        var userId = SetUser(44003);
        var now = DateTime.UtcNow;
        // 逾期 Mastered（今日之前到期） vs 今日到期 NotMastered（未逾期）
        await SeedStateAsync(userId, "Q-44003a", MemoryState.Mastered, now.AddDays(-2)); // 逾期
        await SeedStateAsync(userId, "Q-44003b", MemoryState.NotMastered, now.AddMinutes(-5)); // 今日到期（未逾期）
        await SeedStateAsync(userId, "Q-44003c", MemoryState.Fuzzy, now.AddDays(-1)); // 逾期
        var svc = User.Use<GetReviewQueueService>();

        var result = await svc.ExecuteAsync(new GetReviewQueueReqDto { Date = now, PageSize = 50 }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(2, result.OverdueCount);
        // 逾期题（a/c）排在最前
        var ids = result.Items.Select(i => i.QuestionId).ToList();
        Assert.Equal("Q-44003c", ids[0]); // 逾期且状态更差（Fuzzy < Mastered）→ 第一
        Assert.Equal("Q-44003a", ids[1]); // 逾期 Mastered → 第二
        Assert.Equal("Q-44003b", ids[2]); // 非逾期 → 最后
    }

    /// <summary>BR-35：PageSize=0 → PARAM_INVALID</summary>
    [Fact]
    public async Task ExecuteAsync_InvalidPageSize_ReturnsParamInvalid()
    {
        SetUser(44004);
        var svc = User.Use<GetReviewQueueService>();

        var result = await svc.ExecuteAsync(new GetReviewQueueReqDto
        {
            Date = DateTime.UtcNow,
            PageSize = 0,
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(LearningErrorCodes.ParamInvalid, result.ErrorCode);
    }

    /// <summary>BR-34：响应不含答案正文（仅 QuestionId/State/NextReviewAt 字段）</summary>
    [Fact]
    public async Task ExecuteAsync_ResponseHasNoAnswerContent()
    {
        var userId = SetUser(44005);
        var now = DateTime.UtcNow;
        await SeedStateAsync(userId, "Q-44005a", MemoryState.Fuzzy, now.AddMinutes(-1));
        var svc = User.Use<GetReviewQueueService>();

        var result = await svc.ExecuteAsync(new GetReviewQueueReqDto { Date = now }, TestContext.Current.CancellationToken);

        var json = System.Text.Json.JsonSerializer.Serialize(result);
        Assert.DoesNotContain("answer", json, StringComparison.OrdinalIgnoreCase);
    }
}

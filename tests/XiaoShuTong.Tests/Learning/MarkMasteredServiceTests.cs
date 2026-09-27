using XiaoShuTong.DataServices.Learning;
using XiaoShuTong.Entities.Learning;
using XiaoShuTong.Services.Learning;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.Learning;

/// <summary>
/// UC-4.7 联动：手动标记错题已掌握/移回（MarkMasteredService）Contract 测试
/// 覆盖学习-BR-52：翻转 Mastered（true=标已掌握 / false=移回）| 幂等（同值重复返回当前值）
/// RLS（无当前用户记录 → WRONG_NOT_FOUND）| BR-23 共存语义（手动标记后作答事件仍权威覆写）
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class MarkMasteredServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : XiaoShuTongTestBase(fixture, output)
{
    private long SetUser(long id)
    {
        User.UserInfo!.Id = id;
        User.UserInfo.UserIdString = id.ToString();
        return id;
    }

    private async Task<WrongQuestions> SeedWrongAsync(long userId, string questionId, bool mastered)
    {
        var ds = User.Use<WrongQuestionsDataService>();
        return await ds.EntityCreateAsync(new WrongQuestions
        {
            UId = UidGenerator.NewId(),
            UserId = userId,
            QuestionId = questionId,
            BankId = "bank-learning-48001",
            Subject = "chinese",
            WrongCount = 2,
            LastWrongAt = DateTime.UtcNow,
            Mastered = mastered,
        }, TestContext.Current.CancellationToken);
    }

    private async Task<MarkMasteredResDto> ExecuteAsync(string questionId, bool mastered)
    {
        var svc = User.Use<MarkMasteredService>();
        return await svc.ExecuteAsync(new MarkMasteredReqDto
        {
            QuestionId = questionId,
            Mastered = mastered,
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>BR-52 主路径：标记已掌握 → Mastered=true</summary>
    [Fact]
    public async Task ExecuteAsync_MarkMastered_TurnsTrue()
    {
        var userId = SetUser(48001);
        await SeedWrongAsync(userId, "Q-48001a", mastered: false);

        var result = await ExecuteAsync("Q-48001a", mastered: true);

        Assert.True(result.Success);
        Assert.True(result.Mastered);
    }

    /// <summary>BR-52 主路径：移回错题本 → Mastered=false</summary>
    [Fact]
    public async Task ExecuteAsync_MoveBack_TurnsFalse()
    {
        var userId = SetUser(48002);
        await SeedWrongAsync(userId, "Q-48002a", mastered: true);

        var result = await ExecuteAsync("Q-48002a", mastered: false);

        Assert.True(result.Success);
        Assert.False(result.Mastered);
    }

    /// <summary>BR-52 幂等：同值重复标记 → Success=true 返回当前状态（不重复 Update）</summary>
    [Fact]
    public async Task ExecuteAsync_IdempotentSameValue_ReturnsCurrent()
    {
        var userId = SetUser(48003);
        await SeedWrongAsync(userId, "Q-48003a", mastered: false);

        var first = await ExecuteAsync("Q-48003a", mastered: true);
        var second = await ExecuteAsync("Q-48003a", mastered: true);

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.True(second.Mastered); // 同值幂等，仍为 true
    }

    /// <summary>BR-52 RLS：无当前用户错题记录 → WRONG_NOT_FOUND（4007）</summary>
    [Fact]
    public async Task ExecuteAsync_NoWrongRecord_ReturnsWrongNotFound()
    {
        var userId = SetUser(48004);
        await SeedWrongAsync(999999, "Q-48004a", mastered: false); // 他人记录，当前用户不可见

        var result = await ExecuteAsync("Q-48004a", mastered: true);

        Assert.False(result.Success);
        Assert.Equal(LearningErrorCodes.WrongNotFound, result.ErrorCode);
    }

    /// <summary>BR-52 + BR-23 共存：手动标记已掌握后，错题记录仍存在（作答事件权威覆写语义基础）</summary>
    [Fact]
    public async Task ExecuteAsync_AfterManualMark_RecordStillExistsForBr23()
    {
        var userId = SetUser(48005);
        await SeedWrongAsync(userId, "Q-48005a", mastered: false);

        await ExecuteAsync("Q-48005a", mastered: true);

        // 记录仍在（未删除——选项 A 语义：手动标记仅翻转布尔，BR-23 后续作答可覆写/重置）
        var ds = User.Use<WrongQuestionsDataService>();
        var wrong = await ds.EntityGetAsync(
            x => x.UserId == userId && x.QuestionId == "Q-48005a", TestContext.Current.CancellationToken);
        Assert.NotNull(wrong);
        Assert.True(wrong.Mastered);
    }
}

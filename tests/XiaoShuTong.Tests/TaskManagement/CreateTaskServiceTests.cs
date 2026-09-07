using XiaoShuTong.DataServices.Bank;
using XiaoShuTong.DataServices.GroupManagement;
using XiaoShuTong.DataServices.TaskManagement;
using XiaoShuTong.Entities.Bank;
using XiaoShuTong.Entities.GroupManagement;
using XiaoShuTong.Entities.TaskManagement;
using XiaoShuTong.Services.TaskManagement;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.TaskManagement;

/// <summary>
/// UC-2.1 布置任务（CreateTaskService）Contract 测试
/// 覆盖 BR：BR-01 群组存在且 Owner | BR-02 题库存在 | BR-03 无学生成员 | BR-04 题目归属 | BR-05 全组自动分配
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class CreateTaskServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : DomainXunitTestBase<XiaoShuTongUserInfo, XiaoShuTongDomainTestFixture>(fixture, output)
{
    private long SetUser(long id)
    {
        User.UserInfo!.Id = id;
        User.UserInfo.UserIdString = id.ToString();
        return id;
    }

    private async Task<Groups> SeedGroupAsync(long ownerId, string groupUid, params long[] studentIds)
    {
        var ds = User.Use<GroupsDataService>();
        var group = await ds.EntityCreateAsync(new Groups
        {
            UId = groupUid,
            OwnerId = ownerId,
            Name = "七(3)班",
            Subject = "chinese",
            Status = GroupStatus.Active,
            RankEnabled = true,
        }, TestContext.Current.CancellationToken);

        var membersDs = User.Use<GroupMembersDataService>();
        foreach (var studentId in studentIds)
        {
            await membersDs.EntityCreateAsync(new GroupMembers
            {
                UId = UidGenerator.NewId(),
                GroupId = group.Id,
                UserId = studentId,
                Role = MemberRole.Student,
                JoinedAt = DateTime.UtcNow,
            }, TestContext.Current.CancellationToken);
        }
        return group;
    }

    private async Task<(Banks Bank, List<string> QuestionIds)> SeedBankWithQuestionsAsync(string bankId, int count = 2)
    {
        var bankDs = User.Use<BanksDataService>();
        var bank = await bankDs.EntityCreateAsync(new Banks
        {
            UId = UidGenerator.NewId(),
            BankId = bankId,
            Name = "题库",
            Subject = Subject.Chinese,
            Purpose = BankPurpose.Memorize,
            Privacy = BankPrivacy.Public,
            OwnerId = null,
            JsonPath = $"bank.{bankId}.json",
            Tags = [],
            Status = BankStatus.Active,
        }, TestContext.Current.CancellationToken);

        var qDs = User.Use<QuestionsDataService>();
        var ids = new List<string>();
        for (var i = 1; i <= count; i++)
        {
            var qid = $"Q-{bankId}-{i}";
            await qDs.EntityCreateAsync(new Questions
            {
                UId = UidGenerator.NewId(),
                QuestionId = qid,
                BankId = bankId,
                QType = QuestionType.R1,
                Content = $"{{\"questionId\":\"{qid}\"}}",
                Keywords = "[]",
                KnowledgePoints = [],
                Difficulty = 0,
                Status = QuestionStatus.Active,
            }, TestContext.Current.CancellationToken);
            ids.Add(qid);
        }
        return (bank, ids);
    }

    /// <summary>主流程 + BR-05：任务创建 + 全组学生自动分配</summary>
    [Fact]
    public async Task ExecuteAsync_ValidRequest_CreatesTaskAndAssignments()
    {
        var ownerId = SetUser(21001);
        var group = await SeedGroupAsync(ownerId, $"group-{21001}", 21011, 21012);
        var (_, questionIds) = await SeedBankWithQuestionsAsync("bank-task-21001");
        var svc = User.Use<CreateTaskService>();

        var result = await svc.ExecuteAsync(new CreateTaskReqDto
        {
            GroupUid = group.UId,
            BankId = "bank-task-21001",
            Title = "背诵《岳阳楼记》",
            QuestionIds = questionIds.ToArray(),
            Scenario = "Memorize",
            DeadlineAt = DateTime.UtcNow.AddDays(3),
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.False(string.IsNullOrEmpty(result.TaskUid));
        Assert.Equal(2, result.AssignedCount); // BR-05：2 名学生 → 2 条分配

        var tasksDs = User.Use<TasksDataService>();
        var task = await tasksDs.EntityGetAsync(x => x.UId == result.TaskUid, TestContext.Current.CancellationToken);
        Assert.NotNull(task);
        Assert.Equal(ownerId, task.OwnerId);
        Assert.Equal(group.Id, task.GroupId);
        Assert.Equal(TaskStatus.Active, task.Status);

        var assignmentsDs = User.Use<TaskAssignmentsDataService>();
        var assignments = await assignmentsDs.EntitySelectAsync(x => x.TaskId == task.Id, ct: TestContext.Current.CancellationToken);
        Assert.Equal(2, assignments.Count);
        Assert.All(assignments, a => Assert.Equal(AssignmentStatus.Pending, a.Status));
    }

    /// <summary>BR-01：群组不存在 → GROUP_NOT_FOUND</summary>
    [Fact]
    public async Task ExecuteAsync_UnknownGroup_ReturnsGroupNotFound()
    {
        SetUser(21002);
        var svc = User.Use<CreateTaskService>();

        var result = await svc.ExecuteAsync(new CreateTaskReqDto
        {
            GroupUid = "no-such-group",
            Title = "任务",
            QuestionIds = ["Q-1"],
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(TaskErrorCodes.GroupNotFound, result.ErrorCode);
    }

    /// <summary>BR-01：非 Owner 群组 → FORBIDDEN</summary>
    [Fact]
    public async Task ExecuteAsync_NotOwnerGroup_ReturnsForbidden()
    {
        await SeedGroupAsync(999901, "group-others-21003", 21031);
        SetUser(21003); // 非该群 Owner
        var svc = User.Use<CreateTaskService>();

        var result = await svc.ExecuteAsync(new CreateTaskReqDto
        {
            GroupUid = "group-others-21003",
            Title = "任务",
            QuestionIds = ["Q-1"],
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(TaskErrorCodes.Forbidden, result.ErrorCode);
    }

    /// <summary>BR-02：题库不存在 → BANK_NOT_FOUND</summary>
    [Fact]
    public async Task ExecuteAsync_UnknownBank_ReturnsBankNotFound()
    {
        var ownerId = SetUser(21004);
        var group = await SeedGroupAsync(ownerId, $"group-{21004}", 21041);
        var svc = User.Use<CreateTaskService>();

        var result = await svc.ExecuteAsync(new CreateTaskReqDto
        {
            GroupUid = group.UId,
            BankId = "no-such-bank",
            Title = "任务",
            QuestionIds = ["Q-1"],
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(TaskErrorCodes.BankNotFound, result.ErrorCode);
    }

    /// <summary>BR-04：题目不属于题库 → QUESTION_NOT_IN_BANK</summary>
    [Fact]
    public async Task ExecuteAsync_QuestionNotInBank_ReturnsQuestionNotInBank()
    {
        var ownerId = SetUser(21005);
        var group = await SeedGroupAsync(ownerId, $"group-{21005}", 21051);
        var (_, _) = await SeedBankWithQuestionsAsync("bank-task-21005");
        var svc = User.Use<CreateTaskService>();

        var result = await svc.ExecuteAsync(new CreateTaskReqDto
        {
            GroupUid = group.UId,
            BankId = "bank-task-21005",
            Title = "任务",
            QuestionIds = ["Q-EXTERNAL-001"], // 不属于该题库
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(TaskErrorCodes.QuestionNotInBank, result.ErrorCode);
    }

    /// <summary>BR-03：群组无学生成员 → PARAM_INVALID</summary>
    [Fact]
    public async Task ExecuteAsync_NoStudents_ReturnsParamInvalid()
    {
        var ownerId = SetUser(21006);
        var group = await SeedGroupAsync(ownerId, $"group-{21006}"); // 无学生成员
        var svc = User.Use<CreateTaskService>();

        var result = await svc.ExecuteAsync(new CreateTaskReqDto
        {
            GroupUid = group.UId,
            Title = "任务",
            QuestionIds = ["Q-1"],
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(TaskErrorCodes.ParamInvalid, result.ErrorCode);
    }
}
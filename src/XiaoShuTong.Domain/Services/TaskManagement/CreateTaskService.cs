using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using TKW.Framework.Domain.Transactions;
using XiaoShuTong.DataServices.Bank;
using XiaoShuTong.DataServices.GroupManagement;
using XiaoShuTong.DataServices.TaskManagement;
using XiaoShuTong.Entities.GroupManagement;
using XiaoShuTong.Entities.TaskManagement;
using XiaoShuTong.Tools;

namespace XiaoShuTong.Services.TaskManagement;

/// <summary>
/// UC-2.1：布置任务（三步向导 = 前端编排，后端单次提交）
/// </summary>
/// <remarks>
/// CROSS：写 Tasks + 批量创建 TaskAssignments（任务与全组分配同生共死）。
/// BR-01 群组存在且 Owner | BR-02 题库存在 | BR-03 群组须有学生成员 | BR-04 题目归属题库 | BR-05 全组自动分配
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
[Transactional]
internal class CreateTaskService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private GroupsDataService? _groupsDs;
    private GroupsDataService GroupsDs => _groupsDs ??= User.Use<GroupsDataService>();

    private GroupMembersDataService? _membersDs;
    private GroupMembersDataService MembersDs => _membersDs ??= User.Use<GroupMembersDataService>();

    private BanksDataService? _banksDs;
    private BanksDataService BanksDs => _banksDs ??= User.Use<BanksDataService>();

    private QuestionsDataService? _questionsDs;
    private QuestionsDataService QuestionsDs => _questionsDs ??= User.Use<QuestionsDataService>();

    private TasksDataService? _tasksDs;
    private TasksDataService TasksDs => _tasksDs ??= User.Use<TasksDataService>();

    private TaskAssignmentsDataService? _assignmentsDs;
    private TaskAssignmentsDataService AssignmentsDs => _assignmentsDs ??= User.Use<TaskAssignmentsDataService>();

    /// <summary>
    /// 创建任务 + 为全组学生自动创建分配
    /// </summary>
    public async Task<CreateTaskResDto> ExecuteAsync(CreateTaskReqDto request, CancellationToken ct = default)
    {
        var ownerId = User.UserInfo?.Id ?? 0;

        // BR-01：群组必须存在且当前群主为 Owner
        var group = await GroupsDs.EntityGetAsync(x => x.UId == request.GroupUid, ct);
        if (group == null)
            return new CreateTaskResDto { Success = false, ErrorCode = TaskErrorCodes.GroupNotFound };
        if (group.OwnerId != ownerId)
            return new CreateTaskResDto { Success = false, ErrorCode = TaskErrorCodes.Forbidden };

        // BR-02：关联题库必须存在（选题源引用时）
        if (!string.IsNullOrWhiteSpace(request.BankId))
        {
            var bank = await BanksDs.EntityGetAsync(x => x.BankId == request.BankId, ct);
            if (bank == null)
                return new CreateTaskResDto { Success = false, ErrorCode = TaskErrorCodes.BankNotFound };

            // BR-04：题目必须属于题库（QuestionIds ⊆ 题库）
            var bankQuestionIds = (await QuestionsDs.EntitySelectAsync(
                x => x.BankId == request.BankId, ct: ct))
                .Select(q => q.QuestionId)
                .ToHashSet(StringComparer.Ordinal);
            if (request.QuestionIds.Any(q => !bankQuestionIds.Contains(q)))
                return new CreateTaskResDto { Success = false, ErrorCode = TaskErrorCodes.QuestionNotInBank };
        }

        // BR-03：群组须有学生成员
        var students = await MembersDs.EntitySelectAsync(
            x => x.GroupId == group.Id && x.Role == MemberRole.Student, ct: ct);
        if (students.Count == 0)
            return new CreateTaskResDto { Success = false, ErrorCode = TaskErrorCodes.ParamInvalid };

        // 创建任务（Active，OwnerId/GroupId/题集）
        var scenario = Enum.TryParse<TaskScenario>(request.Scenario, true, out var parsedScenario)
            ? parsedScenario : TaskScenario.Memorize;
        var sessionType = Enum.TryParse<TaskSessionType>(request.SessionType, true, out var parsedSessionType)
            ? parsedSessionType : TaskSessionType.Progressive;

        var task = await TasksDs.EntityCreateAsync(new Tasks
        {
            UId = UidGenerator.NewId(),
            OwnerId = ownerId,
            GroupId = group.Id,
            BankId = request.BankId,
            Title = request.Title,
            Description = request.Description,
            QuestionIds = request.QuestionIds,
            QuestionCount = request.QuestionIds.Length,
            Scenario = scenario,
            SessionType = sessionType,
            AllowRedo = request.AllowRedo,
            StartedAt = DateTime.UtcNow,
            DeadlineAt = request.DeadlineAt,
            Status = TaskStatus.Active,
        }, ct);

        // BR-05：批量创建全组学生分配（Pending，TaskId+UserId 唯一）
        var now = DateTime.UtcNow;
        var assignedCount = 0;
        foreach (var student in students)
        {
            var existing = await AssignmentsDs.EntityGetAsync(
                x => x.TaskId == task.Id && x.UserId == student.UserId, ct);
            if (existing != null)
                continue; // UNIQUE 防重
            await AssignmentsDs.EntityCreateAsync(new TaskAssignments
            {
                UId = UidGenerator.NewId(),
                TaskId = task.Id,
                UserId = student.UserId,
                Status = AssignmentStatus.Pending,
                Progress = 0,
                AssignedAt = now,
            }, ct);
            assignedCount++;
        }

        return new CreateTaskResDto { Success = true, TaskUid = task.UId, AssignedCount = assignedCount };
    }
}

/// <summary>布置任务请求 DTO</summary>
public sealed record CreateTaskReqDto
{
    /// <summary>群组外部键</summary>
    public string GroupUid { get; init; } = string.Empty;

    /// <summary>题库业务键（自由编排时为空）</summary>
    public string? BankId { get; init; }

    /// <summary>任务名（≤128）</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>任务说明</summary>
    public string? Description { get; init; }

    /// <summary>题目 ID 列表</summary>
    public string[] QuestionIds { get; init; } = [];

    /// <summary>场景（Memorize/Assess）</summary>
    public string? Scenario { get; init; }

    /// <summary>会话类型（Progressive/Free/Assembled）</summary>
    public string? SessionType { get; init; }

    /// <summary>是否允许重做</summary>
    public bool AllowRedo { get; init; }

    /// <summary>截止时间</summary>
    public DateTime? DeadlineAt { get; init; }
}

/// <summary>布置任务响应 DTO</summary>
public sealed record CreateTaskResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>任务外部键</summary>
    public string TaskUid { get; init; } = string.Empty;

    /// <summary>自动分配的学生数</summary>
    public int AssignedCount { get; init; }
}
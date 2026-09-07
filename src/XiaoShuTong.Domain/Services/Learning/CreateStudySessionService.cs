using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using TKW.Framework.Domain.Transactions;
using XiaoShuTong.DataServices.Learning;
using XiaoShuTong.Entities.Learning;
using XiaoShuTong.Tools;

namespace XiaoShuTong.Services.Learning;

/// <summary>
/// UC-4.1：开始学习会话
/// </summary>
/// <remarks>
/// BR-01 枚举校验 | BR-02 任务校验（跨模块，任务域切片暂以桩通过）| BR-03 题库校验（跨模块桩）
/// BR-04 题量 10/20/30/50 默认 20 | BR-05 同任务续做幂等复用
/// 任务进度推进（TaskAssignments）为跨模块（切片 04），本切片不实施。
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
[Transactional]
internal class CreateStudySessionService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private static readonly int[] AllowedQuestionCounts = [10, 20, 30, 50];

    private StudySessionsDataService? _sessionsDs;
    private StudySessionsDataService SessionsDs => _sessionsDs ??= User.Use<StudySessionsDataService>();

    /// <summary>
    /// 创建学习会话（任务/复习队列/自由背诵入口）
    /// </summary>
    public async Task<CreateStudySessionResDto> ExecuteAsync(CreateStudySessionReqDto request, CancellationToken ct = default)
    {
        var userId = User.UserInfo?.Id ?? 0;

        // BR-01：Scenario/SessionType 必须为合法枚举值
        if (!Enum.TryParse<LearningScenario>(request.Scenario, true, out var scenario)
            || !Enum.TryParse<SessionType>(request.SessionType, true, out var sessionType))
            return new CreateStudySessionResDto { Success = false, ErrorCode = LearningErrorCodes.ParamInvalid };

        // BR-04：题量可选 10/20/30/50，默认 20
        var questionCount = request.QuestionCount <= 0 ? 20 : request.QuestionCount;
        if (!AllowedQuestionCounts.Contains(questionCount))
            return new CreateStudySessionResDto { Success = false, ErrorCode = LearningErrorCodes.ParamInvalid };

        // BR-02：任务校验（跨模块：任务域切片 04；本切片桩通过——任务会话仅记录 TaskId）
        // BR-03：题库校验（跨模块：题库域切片 03；本切片按注册表桩校验 BankId 非空）
        if (string.IsNullOrWhiteSpace(request.BankId))
            return new CreateStudySessionResDto { Success = false, ErrorCode = LearningErrorCodes.BankNotFound };

        // BR-05：同任务续做幂等——已存在进行中会话（未结束）则复用
        if (request.TaskId is { } taskId && taskId > 0)
        {
            var existing = await SessionsDs.EntityGetAsync(
                x => x.UserId == userId && x.TaskId == taskId && x.EndedAt == null,
                ct);
            if (existing != null)
                return new CreateStudySessionResDto { Success = true, SessionUid = existing.UId, QuestionCount = existing.QuestionCount };
        }

        // 创建会话
        var session = await SessionsDs.EntityCreateAsync(new StudySessions
        {
            UId = UidGenerator.NewId(),
            UserId = userId,
            Scenario = scenario,
            BankId = request.BankId,
            SessionType = sessionType,
            QuestionCount = questionCount,
            TaskId = request.TaskId,
            StartedAt = DateTime.UtcNow,
        }, ct);

        return new CreateStudySessionResDto
        {
            Success = true,
            SessionUid = session.UId,
            QuestionCount = session.QuestionCount,
        };
    }
}

/// <summary>创建学习会话请求 DTO</summary>
public sealed record CreateStudySessionReqDto
{
    /// <summary>场景（Memorize/Assess/PlayPk/PlayDaily）</summary>
    public string Scenario { get; init; } = string.Empty;

    /// <summary>题库业务键</summary>
    public string BankId { get; init; } = string.Empty;

    /// <summary>会话类型（Progressive/Free/Assembled/Level/Pk）</summary>
    public string SessionType { get; init; } = string.Empty;

    /// <summary>群组任务关联（任务会话必传）</summary>
    public long? TaskId { get; init; }

    /// <summary>计划题数（10/20/30/50，默认 20）</summary>
    public int QuestionCount { get; init; }
}

/// <summary>创建学习会话响应 DTO</summary>
public sealed record CreateStudySessionResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>会话外部键（uuid）</summary>
    public string SessionUid { get; init; } = string.Empty;

    /// <summary>本次题数</summary>
    public int QuestionCount { get; init; }
}

using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using XiaoShuTong.DataServices.Learning;
using XiaoShuTong.Entities.Learning;
using XiaoShuTong.Tools;

namespace XiaoShuTong.Services.Learning;

/// <summary>
/// UC-4.3：请求提示（背景钩子）
/// </summary>
/// <remarks>
/// BR-28 题目必须存在 → 1502 | BR-29 提示 ≤20 字、严禁给答案 | BR-30 难度档按状态路由（状态越低提示越深）
/// 提示生成服务（跨模块判题服务）以注册表 Hint 桩代替。
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
internal class GetHintService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private const int HintMaxLength = 20;

    private MemoryStatesDataService? _statesDs;
    private MemoryStatesDataService StatesDs => _statesDs ??= User.Use<MemoryStatesDataService>();

    /// <summary>
    /// 获取提示（按状态路由难度档，≤20 字）
    /// </summary>
    public async Task<GetHintResDto> ExecuteAsync(GetHintReqDto request, CancellationToken ct = default)
    {
        var userId = User.UserInfo?.Id ?? 0;

        // BR-28：题目必须存在 → 1502
        var question = LearningQuestionRegistry.Get(request.QuestionId);
        if (question == null)
            return new GetHintResDto { Success = false, ErrorCode = LearningErrorCodes.QuestionNotInBank };

        // BR-30：难度档未指定 → 按题目记忆状态路由（状态越低提示越深）
        string difficultySlot;
        if (!string.IsNullOrWhiteSpace(request.DifficultySlot))
        {
            if (request.DifficultySlot is not ("S1" or "S2" or "S3"))
                return new GetHintResDto { Success = false, ErrorCode = LearningErrorCodes.ParamInvalid };
            difficultySlot = request.DifficultySlot;
        }
        else
        {
            var state = await StatesDs.EntityGetAsync(
                x => x.UserId == userId && x.QuestionId == request.QuestionId, ct);
            difficultySlot = state?.State switch
            {
                MemoryState.NotMastered => "S3",
                MemoryState.Fuzzy => "S2",
                _ => "S1",
            };
        }

        // BR-29：提示 ≤20 字（从知识卡片提取，不从答案提取；不给出答案）
        var hint = SubmitAttemptService.TruncateHint(question.Hint, HintMaxLength);

        return new GetHintResDto
        {
            Success = true,
            Hint = hint,
            DifficultySlot = difficultySlot,
            HintSource = "memory-hook",
        };
    }
}

/// <summary>请求提示 DTO</summary>
public sealed record GetHintReqDto
{
    /// <summary>题目业务键</summary>
    public string QuestionId { get; init; } = string.Empty;

    /// <summary>难度档（S1/S2/S3，可空按状态路由）</summary>
    public string? DifficultySlot { get; init; }
}

/// <summary>请求提示响应 DTO</summary>
public sealed record GetHintResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>提示文本（≤20 字线索）</summary>
    public string Hint { get; init; } = string.Empty;

    /// <summary>难度档（S1/S2/S3）</summary>
    public string DifficultySlot { get; init; } = string.Empty;

    /// <summary>提示来源（记忆钩子）</summary>
    public string HintSource { get; init; } = string.Empty;
}

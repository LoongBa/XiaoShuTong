using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using TKW.Framework.Domain.Transactions;
using XiaoShuTong.DataServices.Learning;
using XiaoShuTong.Entities.Learning;
using XiaoShuTong.Services.Bank;

namespace XiaoShuTong.Services.Learning;

/// <summary>
/// UC-4.2a：会话取题（薄包装对外暴露，Callee 保持 Bank 域 internal）
/// </summary>
/// <remarks>
/// G-1（ADR-008 决策一）：Learning 域薄包装对外暴露 getSessionQuestion_Execute RPC，
/// Bank 域 GetNextQuestionService（BR-17~20）保持 internal Callee 不动。
/// 唯一业务逻辑：BR-06 会话必须存在且属于当前用户 → 3001（域边界职责，Bank Callee 无职责承载）；
/// 其余 BR 语义（题库-BR-17~20）全由 Callee 承载，本薄包装零业务逻辑。
/// 响应复用 Bank 域 GetNextQuestionResDto（跨域复用：变更需同步 Learning 域，不新建 Res POCO）。
/// [Transactional] 保留：跨 QuestionsDs+BanksDs+StatesDs+AttemptsDs 多 DataService 读，提供一致性读快照
/// （仅读不写，无提交副作用，ADR-008 指定）。EndedAt==null 校验随 G-4 EndStudySession 落地时同步补（本迭代不实现）。
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
[Transactional]
internal class GetSessionQuestionService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private StudySessionsDataService? _sessionsDs;
    private StudySessionsDataService SessionsDs => _sessionsDs ??= User.Use<StudySessionsDataService>();

    /// <summary>
    /// 按会话取下一题（转发 Bank GetNextQuestionService，不含答案与关键词）
    /// </summary>
    public async Task<GetNextQuestionResDto> ExecuteAsync(GetSessionQuestionReqDto request, CancellationToken ct = default)
    {
        var userId = User.UserInfo?.Id ?? 0;

        // BR-06：会话必须存在且属于当前用户 → 3001（薄包装唯一业务逻辑，域边界职责）
        var session = await SessionsDs.EntityGetAsync(x => x.UId == request.SessionUid, ct);
        if (session == null || session.UserId != userId)
            return Fail(LearningErrorCodes.SessionNotFound);

        // 题库-BR-17~20 全由 Callee 承载；SessionId ← SessionUid 转发
        return await User.Use<GetNextQuestionService>().ExecuteAsync(new GetNextQuestionReqDto
        {
            SessionId = request.SessionUid,
            BankId = request.BankId,
            Type = request.Type,
            KnowledgePoint = request.KnowledgePoint,
        }, ct);
    }

    private static GetNextQuestionResDto Fail(string errorCode)
        => new() { Success = false, ErrorCode = errorCode };
}

/// <summary>会话取题请求 DTO</summary>
public sealed record GetSessionQuestionReqDto
{
    /// <summary>会话外部键（createStudySession 产出）</summary>
    public string SessionUid { get; init; } = string.Empty;

    /// <summary>题库业务键</summary>
    public string BankId { get; init; } = string.Empty;

    /// <summary>题型过滤</summary>
    public string? Type { get; init; }

    /// <summary>知识点过滤</summary>
    public string? KnowledgePoint { get; init; }
}

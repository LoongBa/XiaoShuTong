using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using TKW.Framework.Domain.Transactions;
using XiaoShuTong.DataServices.Bank;
using XiaoShuTong.DataServices.Buddy;
using XiaoShuTong.DataServices.Pk;
using XiaoShuTong.Entities.Buddy;
using XiaoShuTong.Entities.Pk;
using XiaoShuTong.Tools;

namespace XiaoShuTong.Services.Pk;

/// <summary>
/// UC-9.1：发起 PK（方式 A：选对手）
/// </summary>
/// <remarks>
/// CROSS：创建 PkMatches（Pending + InviteCode）+ 发起方 PkPlayers（原子）。
/// BR-01 与对手互为 accepted 搭子 → 2005 | BR-02 题库存在 → 1501 | BR-03 题量 5/10/20 → 1002
/// BR-04 生成 4 位对战码 | BR-05 对局+参赛记录原子创建
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
[Transactional]
internal class CreatePkMatchService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private static readonly int[] AllowedQuestionCounts = [5, 10, 20];

    private StudyBuddiesDataService? _buddiesDs;
    private StudyBuddiesDataService BuddiesDs => _buddiesDs ??= User.Use<StudyBuddiesDataService>();

    private BanksDataService? _banksDs;
    private BanksDataService BanksDs => _banksDs ??= User.Use<BanksDataService>();

    private PkMatchesDataService? _matchesDs;
    private PkMatchesDataService MatchesDs => _matchesDs ??= User.Use<PkMatchesDataService>();

    private PkPlayersDataService? _playersDs;
    private PkPlayersDataService PlayersDs => _playersDs ??= User.Use<PkPlayersDataService>();

    /// <summary>
    /// 发起 PK（校验搭子资格 + 题库 + 参数 → 建对局 + 发起方参赛记录）
    /// </summary>
    public async Task<CreatePkMatchResDto> ExecuteAsync(CreatePkMatchReqDto request, CancellationToken ct = default)
    {
        var userId = User.UserInfo?.Id ?? 0;

        // BR-03：题量可选 5/10/20，默认 10
        var questionCount = request.QuestionCount <= 0 ? 10 : request.QuestionCount;
        if (!AllowedQuestionCounts.Contains(questionCount))
            return new CreatePkMatchResDto { Success = false, ErrorCode = PkErrorCodes.ParamInvalid };

        // BR-02：题库存在 → 1501
        var bank = await BanksDs.EntityGetAsync(x => x.BankId == request.BankId, ct);
        if (bank == null)
            return new CreatePkMatchResDto { Success = false, ErrorCode = PkErrorCodes.BankNotFound };

        // BR-01：与对手互为 accepted 搭子 → 2005
        var buddy = await BuddiesDs.EntityGetAsync(
            x => (x.InviterId == userId && x.InviteeId == request.OpponentUserId)
                 || (x.InviterId == request.OpponentUserId && x.InviteeId == userId), ct);
        if (buddy == null || buddy.Status != BuddyStatus.Accepted)
            return new CreatePkMatchResDto { Success = false, ErrorCode = PkErrorCodes.NotBuddyPk };

        var mode = Enum.TryParse<PkMode>(request.Mode, true, out var parsedMode) ? parsedMode : PkMode.Sync;

        // BR-04/BR-05：创建对局（Pending + 4 位对战码）+ 发起方参赛记录（原子）
        var match = await MatchesDs.EntityCreateAsync(new PkMatches
        {
            UId = UidGenerator.NewId(),
            Subject = bank.Subject,
            BankId = bank.BankId,
            Topic = request.Topic,
            QuestionCount = questionCount,
            Mode = mode,
            Status = PkMatchStatus.Pending,
            InviteCode = GenerateInviteCode(),
        }, ct);

        await PlayersDs.EntityCreateAsync(new PkPlayers
        {
            UId = UidGenerator.NewId(),
            MatchId = match.Id,
            UserId = userId,
            Score = 0,
            CorrectCount = 0,
            TotalTimeMs = 0,
        }, ct);

        return new CreatePkMatchResDto { Success = true, MatchUid = match.UId, InviteCode = match.InviteCode ?? string.Empty };
    }

    /// <summary>4 位数字对战码</summary>
    private static string GenerateInviteCode()
        => new Random().Next(1000, 10000).ToString("D4");
}

/// <summary>发起 PK 请求 DTO</summary>
public sealed record CreatePkMatchReqDto
{
    /// <summary>对手用户 Id（accepted 搭子）</summary>
    public long OpponentUserId { get; init; }

    /// <summary>题库业务键</summary>
    public string BankId { get; init; } = string.Empty;

    /// <summary>知识点/主题</summary>
    public string? Topic { get; init; }

    /// <summary>题量（5/10/20，默认 10）</summary>
    public int QuestionCount { get; init; }

    /// <summary>模式（Sync/Async，默认 Sync）</summary>
    public string? Mode { get; init; }
}

/// <summary>发起 PK 响应 DTO</summary>
public sealed record CreatePkMatchResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>对局外部键</summary>
    public string MatchUid { get; init; } = string.Empty;

    /// <summary>4 位对战码</summary>
    public string InviteCode { get; init; } = string.Empty;
}
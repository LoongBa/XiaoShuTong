using System.Text.Json;
using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using XiaoShuTong.DataServices.Bank;
using XiaoShuTong.DataServices.Pk;
using XiaoShuTong.Entities.Bank;
using XiaoShuTong.Entities.Pk;
using XiaoShuTong.Services.Judging;
using XiaoShuTong.Tools;

namespace XiaoShuTong.Services.Pk;

/// <summary>
/// UC-9.3：对战答题
/// </summary>
/// <remarks>
/// BR-11 非参赛者 → 2004 | BR-12 答对 +10、答错 0 | BR-13 Partial 计 0 分（IsCorrect=false）
/// BR-14 判题失败降级规则判定 | BR-15 同题防重复（UNQ）幂等 | BR-16 timeCostMs 客户端上送
/// 判题引擎（切片 03 JudgingEngineService 五键契约）跨模块消费；最后一题答完自动结算胜负。
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
internal class SubmitPkAnswerService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private PkMatchesDataService? _matchesDs;
    private PkMatchesDataService MatchesDs => _matchesDs ??= User.Use<PkMatchesDataService>();

    private PkPlayersDataService? _playersDs;
    private PkPlayersDataService PlayersDs => _playersDs ??= User.Use<PkPlayersDataService>();

    private PkAttemptsDataService? _attemptsDs;
    private PkAttemptsDataService AttemptsDs => _attemptsDs ??= User.Use<PkAttemptsDataService>();

    private QuestionsDataService? _questionsDs;
    private QuestionsDataService QuestionsDs => _questionsDs ??= User.Use<QuestionsDataService>();

    /// <summary>
    /// 提交 PK 作答（判题引擎 → 记分 → 写明细 → 更新分数 → 末题结算）
    /// </summary>
    public async Task<SubmitPkAnswerResDto> ExecuteAsync(SubmitPkAnswerReqDto request, CancellationToken ct = default)
    {
        var userId = User.UserInfo?.Id ?? 0;

        // BR-11：对局存在 + 参赛者 + ongoing
        var match = await MatchesDs.EntityGetAsync(x => x.UId == request.MatchUid, ct);
        if (match == null)
            return new SubmitPkAnswerResDto { Success = false, ErrorCode = PkErrorCodes.MatchNotFound };
        if (match.Status != PkMatchStatus.Ongoing)
            return new SubmitPkAnswerResDto { Success = false, ErrorCode = PkErrorCodes.MatchClosed };

        var player = await PlayersDs.EntityGetAsync(
            x => x.MatchId == match.Id && x.UserId == userId, ct);
        if (player == null)
            return new SubmitPkAnswerResDto { Success = false, ErrorCode = PkErrorCodes.NotParticipant };

        // BR-15：同题防重复（幂等返回已有结果）
        var existing = await AttemptsDs.EntityGetAsync(
            x => x.MatchId == match.Id && x.PlayerId == player.Id && x.QuestionId == request.QuestionId, ct);
        if (existing != null)
        {
            return new SubmitPkAnswerResDto
            {
                Success = true,
                IsCorrect = existing.IsCorrect,
                Result = existing.Result.ToString(),
                Score = existing.IsCorrect ? 10 : 0,
            };
        }

        // BR-14：判题引擎（跨模块五键契约；LLM 失败降级本地规则）
        var question = await QuestionsDs.EntityGetAsync(x => x.QuestionId == request.QuestionId, ct);
        if (question == null)
            return new SubmitPkAnswerResDto { Success = false, ErrorCode = PkErrorCodes.ParamInvalid };

        var engine = User.Use<JudgingEngineService>();
        var verdict = await engine.JudgeAsync(new JudgingRequestDto
        {
            QuestionId = request.QuestionId,
            QType = question.QType.ToString(),
            Keywords = question.Keywords,
            UserAnswer = request.UserAnswer,
            HintLevel = "None",
            LlmFailed = false,
            PreferLlm = false,
        }, ct);

        // BR-12/BR-13：Correct → +10；Partial/Wrong → 0（IsCorrect 仅 Correct）
        var isCorrect = verdict.Result == "Correct";
        var score = isCorrect ? 10 : 0;

        await AttemptsDs.EntityCreateAsync(new PkAttempts
        {
            UId = UidGenerator.NewId(),
            MatchId = match.Id,
            PlayerId = player.Id,
            UserId = userId,
            QuestionId = request.QuestionId,
            Answer = request.UserAnswer,
            Result = verdict.Result switch
            {
                "Correct" => PkAttemptResult.Correct,
                "Partial" => PkAttemptResult.Partial,
                _ => PkAttemptResult.Wrong,
            },
            IsCorrect = isCorrect,
            TimeCostMs = request.TimeCostMs, // BR-16：客户端上送
        }, ct);

        // 更新 PkPlayers 分数/答对数/总用时
        player.Score += score;
        if (isCorrect)
            player.CorrectCount++;
        player.TotalTimeMs += request.TimeCostMs;
        await PlayersDs.EntityUpdateAsync(player, ct);

        // 末题答完 → 结算胜负（同分比用时）
        var answeredCount = await AttemptsDs.CountAsync(
            x => x.PlayerId == player.Id, ct);
        if (answeredCount >= match.QuestionCount)
            await FinalizeMatchAsync(match, ct);

        return new SubmitPkAnswerResDto
        {
            Success = true,
            IsCorrect = isCorrect,
            Result = verdict.Result,
            Confidence = verdict.Confidence,
            Score = score,
        };
    }

    /// <summary>结算：双方分数对比（同分比用时）→ WinnerId + Finished + FinishReason=Score</summary>
    private async Task FinalizeMatchAsync(PkMatches match, CancellationToken ct)
    {
        var players = await PlayersDs.EntitySelectAsync(x => x.MatchId == match.Id, ct: ct);
        if (players.Count < 2)
            return;

        var sorted = players
            .OrderByDescending(p => p.Score)
            .ThenBy(p => p.TotalTimeMs)
            .ToList();

        match.WinnerId = sorted[0].Score == sorted[1].Score ? null : sorted[0].UserId; // 平局 null
        match.Status = PkMatchStatus.Finished;
        match.FinishReason = PkFinishReason.Score;
        match.FinishedAt = DateTime.UtcNow;
        await MatchesDs.EntityUpdateAsync(match, ct);
    }
}

/// <summary>提交 PK 作答请求 DTO</summary>
public sealed record SubmitPkAnswerReqDto
{
    /// <summary>对局外部键</summary>
    public string MatchUid { get; init; } = string.Empty;

    /// <summary>题目业务键</summary>
    public string QuestionId { get; init; } = string.Empty;

    /// <summary>用户答案</summary>
    public string UserAnswer { get; init; } = string.Empty;

    /// <summary>答题用时（毫秒，客户端上送）</summary>
    public int TimeCostMs { get; init; }
}

/// <summary>提交 PK 作答响应 DTO</summary>
public sealed record SubmitPkAnswerResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>是否答对</summary>
    public bool IsCorrect { get; init; }

    /// <summary>判题结果（Correct/Partial/Wrong）</summary>
    public string Result { get; init; } = string.Empty;

    /// <summary>判题置信度</summary>
    public double? Confidence { get; init; }

    /// <summary>本题得分（10/0）</summary>
    public int Score { get; init; }
}
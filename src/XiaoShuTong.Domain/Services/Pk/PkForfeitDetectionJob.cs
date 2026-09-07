using TKW.Framework.Domain;
using XiaoShuTong.DataServices.Pk;
using XiaoShuTong.Entities.Pk;
using XiaoShuTong.Tools;

namespace XiaoShuTong.Services.Pk;

/// <summary>
/// UC-9.5：掉线弃权检测（BackgroundJob，无对外接口）
/// </summary>
/// <remarks>
/// 调度：事件驱动（断线 30s 计时）+ Hangfire 定时兜底（切片验证：测试直接调用）。
/// BR-21 30s 内重连取消计时 | BR-22 单对局失败不中断整批（幂等）| BR-23 30s 未重连 → FinishReason=Forfeit + 对方胜
/// 断线计时以 PkDisconnectTracker（内存桩）记录；生产为事件驱动 + Redis。
/// </remarks>
internal class PkForfeitDetectionJob(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private const int ForfeitGraceSeconds = 30;

    private PkMatchesDataService? _matchesDs;
    private PkMatchesDataService MatchesDs => _matchesDs ??= User.Use<PkMatchesDataService>();

    private PkPlayersDataService? _playersDs;
    private PkPlayersDataService PlayersDs => _playersDs ??= User.Use<PkPlayersDataService>();

    /// <summary>
    /// 扫描断线超时的 ongoing 对局 → 判弃权（对方获胜）
    /// </summary>
    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        var ongoing = await MatchesDs.EntitySelectAsync(
            x => x.Status == PkMatchStatus.Ongoing, ct: ct);

        foreach (var match in ongoing)
        {
            try
            {
                var players = await PlayersDs.EntitySelectAsync(x => x.MatchId == match.Id, ct: ct);
                foreach (var player in players)
                {
                    // BR-21：30s 内重连（未在追踪器或已取消）→ 跳过
                    var disconnectedAt = PkDisconnectTracker.GetDisconnectedAt(match.Id, player.UserId);
                    if (disconnectedAt == null)
                        continue;

                    // BR-23：30s 未重连 → 判弃权，对方获胜
                    if (now - disconnectedAt.Value < TimeSpan.FromSeconds(ForfeitGraceSeconds))
                        continue;

                    var opponent = players.FirstOrDefault(p => p.UserId != player.UserId);
                    if (opponent == null)
                        continue;

                    match.Status = PkMatchStatus.Finished;
                    match.FinishReason = PkFinishReason.Forfeit;
                    match.WinnerId = opponent.UserId;
                    match.FinishedAt = now;
                    await MatchesDs.EntityUpdateAsync(match, ct);
                    PkDisconnectTracker.Cancel(match.Id, player.UserId);
                    break; // 该对局已结算
                }
            }
            catch
            {
                // BR-22：单对局失败记录日志（切片吞异常继续），不中断整批
            }
        }
    }
}
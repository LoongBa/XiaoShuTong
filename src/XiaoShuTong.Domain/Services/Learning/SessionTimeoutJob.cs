using TKW.Framework.Domain;
using XiaoShuTong.DataServices.Learning;
using XiaoShuTong.Entities.Learning;

namespace XiaoShuTong.Services.Learning;

/// <summary>
/// UC-4.9：会话超时收尾（BackgroundJob，无对外接口；ADR-009 后续待办落地）
/// </summary>
/// <remarks>
/// 调度：Hangfire 每日定时兜底（切片验证：测试直接调用）。
/// 背景：StudySessions.EndedAt 由 EndStudySessionService 写入（前端主动调用）——用户中途弃用
/// （不调 endStudySession）→ 会话永远 EndedAt==null → BR-05 幂等永远复用同一会话（用户无法开新会话、
/// 历史会话无法审计）。本 Job 兜底：扫描超时未收尾会话（EndedAt==null 且 StartedAt 超 24h）→ 置 EndedAt。
/// 幂等：已收尾会话不重复处理；单会话失败不中断整批（BR 模式对齐 TaskOverdueScanJob）。
/// </remarks>
internal class SessionTimeoutJob(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    /// <summary>超时窗口（小时）——超过视为弃用会话（Oracle C2：24h 合理默认，差异化需求登记 P3）</summary>
    private const int TimeoutWindowHours = 24;

    private StudySessionsDataService? _sessionsDs;
    private StudySessionsDataService SessionsDs => _sessionsDs ??= User.Use<StudySessionsDataService>();

    /// <summary>
    /// 扫描超时未收尾会话 → 置 EndedAt（释放 BR-05 幂等复用 + 会话生命周期闭环）
    /// </summary>
    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddHours(-TimeoutWindowHours);

        // 一次查全部超时未收尾会话（读侧一次查询，V0.7.3 范式）
        var stale = await SessionsDs.EntitySelectAsync(
            x => x.EndedAt == null && x.StartedAt < cutoff, ct: ct);
        if (stale.Count == 0)
            return; // 幂等：无超时会话跳过

        foreach (var session in stale)
        {
            try
            {
                // Oracle 风险补充②：竞态幂等检查——扫描后、写入前用户可能已调 EndStudySession 收尾
                if (session.EndedAt.HasValue)
                    continue;

                // M1-A（Oracle 采纳）：EndedAt = StartedAt+24h（业务时间语义——会话存活到超时点），
                // 不聚合 CorrectCount/TotalTimeMs（弃用会话统计不可信，聚合无业务价值且成本高）
                session.EndedAt = session.StartedAt.AddHours(TimeoutWindowHours);
                await SessionsDs.EntityUpdateAsync(session, ct);
            }
            catch
            {
                // 单会话失败继续（对齐 TaskOverdueScanJob BR-21 模式）
            }
        }
    }
}

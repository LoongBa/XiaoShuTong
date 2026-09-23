using XiaoShuTong.Entities.Learning;

namespace XiaoShuTong.Services.Learning;

/// <summary>
/// 会话结果聚合器（GetSessionResult 与 EndStudySession 共享口径，ADR-009 决策二）
/// </summary>
/// <remarks>
/// 学习域内 internal static（Oracle 评审闭环：聚合含 BR-41/42 学习域专属规则，不放 Tools 跨域目录；
/// ADR-009"私有静态"勘误为 internal——跨 Service 共享（EndStudy 在 Learning 另一文件）私有不可见）。
/// BlockedPoints 的 QuestionMetaProvider 富化仍留 GetSessionResultService（EndStudy 不消费该富化）。
/// </remarks>
internal static class SessionResultAggregator
{
    /// <summary>
    /// 从作答记录聚合会话统计（口径与 GetSessionResultService 一致）
    /// </summary>
    /// <param name="attempts">会话内作答记录</param>
    /// <returns>(答对数, 总耗时毫秒, 新增★数, 卡壳题目ID[])</returns>
    public static (int CorrectCount, long TotalTimeMs, int NewStarCount, string[] BlockedQuestionIds)
        AggregateFromAttempts(IReadOnlyList<Attempts> attempts)
    {
        // BR-40 口径：答对数 = Result==Correct
        var correctCount = attempts.Count(a => a.Result == JudgmentResult.Correct);

        // 总耗时 = Σ TimeCostMs（客户端上送，非服务端计时）
        var totalTimeMs = attempts.Sum(a => a.TimeCostMs ?? 0L);

        // BR-41：新增 ★ = PostState=Proficient 且 PreState!=Proficient 的次数
        var newStarCount = attempts.Count(a =>
            a.PostState == MemoryState.Proficient && a.PreState != MemoryState.Proficient);

        // BR-42：卡壳题目 = PostState ∈ {NotMastered, Fuzzy}（知识点富化由调用方做）
        var blockedQuestionIds = attempts
            .Where(a => a.PostState is MemoryState.NotMastered or MemoryState.Fuzzy)
            .Select(a => a.QuestionId)
            .Distinct()
            .ToArray();

        return (correctCount, totalTimeMs, newStarCount, blockedQuestionIds);
    }
}

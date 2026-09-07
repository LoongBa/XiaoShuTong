using TKW.Framework.Domain;
using XiaoShuTong.DataServices.Learning;
using XiaoShuTong.Entities.Learning;
using XiaoShuTong.Tools;

namespace XiaoShuTong.Services.Learning;

/// <summary>
/// UC-4.8：知识点掌握度聚合（BackgroundJob，无对外接口）
/// </summary>
/// <remarks>
/// 调度：Hangfire 每小时批量 / 家长报告按需触发（切片验证：测试直接调用）。
/// BR-47 无作答则跳过 | BR-48 单用户失败不影响整批 | BR-49 聚合口径（State 中位/最差、Accuracy 均值、AttemptCount 累计）| BR-50 按需触发
/// 幂等：全量重算 + Upsert（UserId+Subject+KnowledgePoint 唯一），重复执行不重复累计。
/// </remarks>
internal class KnowledgeMasteryAggregationJob(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private AttemptsDataService? _attemptsDs;
    private AttemptsDataService AttemptsDs => _attemptsDs ??= User.Use<AttemptsDataService>();

    private KnowledgeMasteryDataService? _masteryDs;
    private KnowledgeMasteryDataService MasteryDs => _masteryDs ??= User.Use<KnowledgeMasteryDataService>();

    /// <summary>
    /// 扫描作答 → 按 Subject×KnowledgePoint 聚合 → Upsert KnowledgeMastery
    /// </summary>
    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var attempts = await AttemptsDs.EntitySelectAsync(
            x => true, ct: ct);

        // BR-47：无作答则跳过
        if (attempts.Count == 0)
            return;

        foreach (var userGroup in attempts.GroupBy(a => a.UserId))
        {
            try
            {
                await AggregateUserAsync(userGroup.Key, userGroup.ToList(), ct);
            }
            catch
            {
                // BR-48：单用户失败记录日志（切片：吞异常继续），不中断整批
            }
        }
    }

    private async Task AggregateUserAsync(long userId, List<Attempts> userAttempts, CancellationToken ct)
    {
        var grouped = userAttempts
            .GroupBy(a => (Subject: LearningQuestionRegistry.Get(a.QuestionId)?.Subject ?? string.Empty,
                           Point: LearningQuestionRegistry.Get(a.QuestionId)?.KnowledgePoint ?? string.Empty))
            .Where(g => !string.IsNullOrEmpty(g.Key.Subject) && !string.IsNullOrEmpty(g.Key.Point));

        foreach (var group in grouped)
        {
            // BR-49：State 取最差、Accuracy 均值、AttemptCount 累计、LastReviewedAt 取最大
            var worstState = group.MinBy(a => StateSeverity(a.PostState))!.PostState;
            var accuracy = Math.Round(group.Average(a => AttemptAccuracy(a.Result)), 4);
            var lastReviewedAt = group.Max(a => a.AnsweredAt);

            var existing = await MasteryDs.EntityGetAsync(
                x => x.UserId == userId
                     && x.Subject == group.Key.Subject
                     && x.KnowledgePoint == group.Key.Point, ct);

            if (existing == null)
            {
                await MasteryDs.EntityCreateAsync(new KnowledgeMastery
                {
                    UserId = userId,
                    Subject = group.Key.Subject,
                    KnowledgePoint = group.Key.Point,
                    State = worstState,
                    Accuracy = accuracy,
                    AttemptCount = group.Count(),
                    LastReviewedAt = lastReviewedAt,
                }, ct);
            }
            else
            {
                existing.State = worstState;
                existing.Accuracy = accuracy;
                existing.AttemptCount = group.Count();
                existing.LastReviewedAt = lastReviewedAt;
                await MasteryDs.EntityUpdateAsync(existing, ct);
            }
        }
    }

    private static int StateSeverity(MemoryState state)
        => state switch
        {
            MemoryState.NotMastered => 0,
            MemoryState.Fuzzy => 1,
            MemoryState.Mastered => 2,
            MemoryState.Proficient => 3,
            _ => 4,
        };

    private static double AttemptAccuracy(JudgmentResult result)
        => result switch
        {
            JudgmentResult.Correct => 1d,
            JudgmentResult.Partial => 0.5d,
            _ => 0d,
        };
}

namespace XiaoShuTong.Tools;

/// <summary>题目元数据（题库域 D01 §7.6 跨模块桩，切片验证用）</summary>
public sealed record LearningQuestionMeta(
    string QuestionId,
    string BankId,
    string Subject,
    string KnowledgePoint,
    string QType,
    string[] AnswerKeywords,
    string Hint);

/// <summary>
/// 题库注册表（跨模块桩：题库域/判题服务在切片 03 实施，本切片以注册表代替）
/// </summary>
/// <remarks>
/// 测试通过 Register 预置题目元数据（答案关键词用于本地判题、知识点用于聚合/结果页）。
/// 全局静态字典 + 测试唯一 QuestionId 隔离。
/// </remarks>
public static class LearningQuestionRegistry
{
    private static readonly Dictionary<string, LearningQuestionMeta> Questions = new(StringComparer.Ordinal);

    /// <summary>注册题目元数据</summary>
    public static void Register(LearningQuestionMeta meta)
    {
        ArgumentNullException.ThrowIfNull(meta);
        Questions[meta.QuestionId] = meta;
    }

    /// <summary>取题目元数据（不存在返回 null）</summary>
    public static LearningQuestionMeta? Get(string questionId)
        => Questions.TryGetValue(questionId, out var meta) ? meta : null;

    /// <summary>题目是否存在（可带题库过滤，BR-26）</summary>
    public static bool Exists(string questionId, string? bankId = null)
    {
        if (!Questions.TryGetValue(questionId, out var meta))
            return false;
        return bankId == null || meta.BankId == bankId;
    }
}

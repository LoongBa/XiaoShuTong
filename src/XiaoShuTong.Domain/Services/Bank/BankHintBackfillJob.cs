using System.Text.Json.Nodes;
using TKW.Framework.Domain;
using XiaoShuTong.DataServices.Bank;
using XiaoShuTong.Entities.Bank;

namespace XiaoShuTong.Services.Bank;

/// <summary>
/// UC-B.4c：存量 Hint 回填（BackgroundJob，无对外接口；ADR-010 决策六辅线）
/// </summary>
/// <remarks>
/// 对齐 BankContentPreprocessJob 同域范式（Oracle 评审闭环 #4：Jobs/ 目录不存在，放 Services/Bank/）。
/// 遍历 Questions.Hint 为空的行，按 knowledge_card_id→卡片记忆钩子（语文优先）补齐；
/// 存量 txt/AI 草稿行无 knowledge_card_id → 按 KnowledgePoints[0] 反查（Oracle 评审闭环 #3）：
///   (a) 精确匹配卡片 id（含篇名）(b) 规范化去标点包含匹配 title (c) 多命中取首条 + 未命中计数上报；
/// 无卡片映射 → 留空 + 计数上报（前端兜底文案"再想想，回忆下要点"已生效）。
/// 幂等：已回填跳过；回填不覆盖已存在的 Hint。
/// </remarks>
internal class BankHintBackfillJob(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private QuestionsDataService? _questionsDs;
    private QuestionsDataService QuestionsDs => _questionsDs ??= User.Use<QuestionsDataService>();

    private const int BatchSize = 50;

    /// <summary>
    /// 回填 Hint 空行；返回 (回填数, 未命中数, 总数)
    /// </summary>
    public async Task<(int Filled, int Unmatched, int Total)> ExecuteAsync(
        string? bankId = null, string? knowledgeCardsJson = null, CancellationToken ct = default)
    {
        // 卡片映射（缺省空 → 只能按 knowledge_card_id 或 KnowledgePoints 反查，未命中留空）
        var cardHooks = new Dictionary<string, string>(StringComparer.Ordinal);
        var cardTitles = new List<string>();
        if (!string.IsNullOrWhiteSpace(knowledgeCardsJson))
            ParseCards(knowledgeCardsJson, cardHooks, cardTitles);

        // 遍历 Hint 空行（QuestionsConditions 无 ByHintIsNull → 直接谓词，Oracle 评审 #5 已注）
        var pending = await QuestionsDs.EntitySelectAsync(
            x => bankId == null || x.BankId == bankId, ct: ct);
        var emptyRows = pending.Where(x => string.IsNullOrWhiteSpace(x.Hint)).Take(1000).ToList();

        var filled = 0;
        var unmatched = 0;
        foreach (var chunk in emptyRows.Chunk(BatchSize))
        {
            foreach (var question in chunk)
            {
                // (a) 精确匹配卡片 id（若题目 KnowledgePoints[0] 与卡片 id/title 精确一致）
                var kp = question.KnowledgePoints.FirstOrDefault() ?? string.Empty;
                var hint = string.Empty;
                if (!string.IsNullOrWhiteSpace(kp))
                {
                    hint = cardHooks.GetValueOrDefault(kp)
                        ?? cardHooks.FirstOrDefault(kv => kv.Key.Trim() == kp.Trim()).Value
                        ?? string.Empty;
                }
                // (b) 规范化去标点包含匹配 title
                if (string.IsNullOrWhiteSpace(hint) && !string.IsNullOrWhiteSpace(kp))
                {
                    var normKp = Normalize(kp);
                    hint = cardTitles
                        .Where(t => normKp.Length > 0 && Normalize(t).Contains(normKp, StringComparison.OrdinalIgnoreCase))
                        .Select(t => cardHooks.GetValueOrDefault(t))
                        .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
                }

                if (string.IsNullOrWhiteSpace(hint))
                {
                    unmatched++;
                    continue; // 无卡片映射 → 留空 + 计数（前端兜底已生效）
                }

                question.Hint = hint;
                await QuestionsDs.EntityUpdateAsync(question, ct);
                filled++;
            }
        }

        return (filled, unmatched, emptyRows.Count);
    }

    /// <summary>解析卡片 JSON → id/title → 记忆钩子 双映射</summary>
    private static void ParseCards(string json, Dictionary<string, string> hooks, List<string> titles)
    {
        var root = JsonNode.Parse(json);
        if (root is not JsonArray array) return;
        foreach (var node in array)
        {
            if (node is not JsonObject obj) continue;
            var id = obj["id"]?.GetValue<string>();
            var title = obj["title"]?.GetValue<string>();
            var hook = obj["fields"]?["记忆钩子"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(hook))
            {
                if (!string.IsNullOrWhiteSpace(id)) hooks[id] = hook;
                if (!string.IsNullOrWhiteSpace(title)) hooks.TryAdd(title, hook); // ??= 在 key 不存在时先读会抛 KeyNotFoundException，改用 TryAdd
                if (!string.IsNullOrWhiteSpace(title)) titles.Add(title);
            }
        }
    }

    /// <summary>去标点/空白规范化（卡片 title 与知识点匹配容差）</summary>
    private static string Normalize(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (var c in s)
        {
            if (char.IsLetterOrDigit(c))
                sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }
}
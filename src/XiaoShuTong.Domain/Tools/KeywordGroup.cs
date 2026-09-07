namespace XiaoShuTong.Tools;

/// <summary>
/// 判题关键词组（KeywordGroup[]，存于 Questions.Keywords JSON）
/// </summary>
/// <param name="Aliases">同义词组：组内任一命中即该组命中（BR-33）</param>
/// <param name="Weight">组权重（参与加权命中率）</param>
/// <param name="Required">必中要点：未命中强制 Partial（BR-32）</param>
public sealed record KeywordGroup(
    string[] Aliases,
    double Weight = 1d,
    bool Required = false);

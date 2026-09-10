using System.Text.Json.Serialization;
using FreeSql.DataAnnotations;
using TKW.Framework.CodeGeneration;

namespace XiaoShuTong.Entities.Platform;

/// <summary>
/// AI 模型配置（统一 AI 网关路由表）
/// </summary>
/// <remarks>
/// 平台运营维护；LlmGateway 按 Enabled + SortOrder 升序 round-robin 轮询调用。
/// ApiKey 为敏感字段（[DtoFieldIgnore][JsonIgnore]）：不下发前端、不入日志（平台-BR-01）。
/// </remarks>
[Table(Name = nameof(AiModelConfig), DisableSyncStructure = false)]
[DomainGenerateCode(DefaultPageSize = 50)]
[Index("idx_aimodelconfig_uid", nameof(UId), IsUnique = true)]
public partial class AiModelConfig
{
    /// <summary>自增主键</summary>
    [Column(IsPrimary = true, IsIdentity = true, Position = 1)]
    public long Id { get; set; }

    /// <summary>外部业务键（uuid，API/DTO 暴露）</summary>
    [Column(Position = 2, StringLength = 32)]
    [DtoField(IsSearchable = true)]
    public string UId { get; set; } = string.Empty;

    /// <summary>配置名（≤64）</summary>
    [Column(Position = 3, StringLength = 64)]
    [DtoField(IsSearchable = true)]
    public string Name { get; set; } = string.Empty;

    /// <summary>供应商（OpenAiCompatible）</summary>
    [Column(Position = 4, MapType = typeof(string), StringLength = 20)]
    public AiProvider Provider { get; set; } = AiProvider.OpenAiCompatible;

    /// <summary>在线 API BaseUrl（如 https://api.openai.com/v1）</summary>
    [Column(Position = 5, StringLength = 256)]
    [DtoField(IsSearchable = true)]
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>API Key（敏感：不下发前端、不入日志）</summary>
    [Column(Position = 6, StringLength = 256)]
    [DtoFieldIgnore]
    [JsonIgnore]
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>模型名（如 gpt-4o-mini）</summary>
    [Column(Position = 7, StringLength = 64)]
    public string ModelName { get; set; } = string.Empty;

    /// <summary>是否启用（默认 true）</summary>
    [Column(Position = 8)]
    public bool Enabled { get; set; } = true;

    /// <summary>轮询顺序（升序优先，默认 0）</summary>
    [Column(Position = 9)]
    public int SortOrder { get; set; }

    /// <summary>请求超时秒数（默认 30）</summary>
    [Column(Position = 10)]
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>备注（可空）</summary>
    [Column(Position = 11, StringLength = 256)]
    public string? Remark { get; set; }

    /// <summary>创建时间</summary>
    [Column(Position = 12)]
    [DtoField(CanModify = false)]
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

    /// <summary>更新时间</summary>
    [Column(Position = 13, CanUpdate = true)]
    public DateTime UpdateTime { get; set; } = DateTime.UtcNow;
}

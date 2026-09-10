using TKW.Framework.CodeGeneration;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;
using TKW.Framework.Domain.Transactions;
using XiaoShuTong.DataServices.Platform;
using XiaoShuTong.Entities.Platform;
using XiaoShuTong.Entities.Platform.DTOs;
using XiaoShuTong.Tools;

namespace XiaoShuTong.Services.Platform;

/// <summary>
/// UC-M04：AI 模型配置管理（统一 AI 网关路由表 CRUD + 启停 + 轮询排序）
/// </summary>
/// <remarks>
/// 平台-BR-01 ApiKey 敏感字段：创建入库但不回显，列表/详情 DTO 不含 ApiKey
/// 平台-BR-02 多模型轮询（SortOrder 升序）由 LlmGateway 消费
/// </remarks>
[GenerateController]
[AuthorityFilter<XiaoShuTongUserInfo>]
[Transactional]
internal class AiModelConfigService(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private AiModelConfigDataService? _modelsDs;
    private AiModelConfigDataService ModelsDs => _modelsDs ??= User.Use<AiModelConfigDataService>();

    /// <summary>
    /// 模型配置分页列表（平台-BR-19：DTO 不含 ApiKey）
    /// </summary>
    public async Task<ListAiModelConfigResDto> ListModelsAsync(
        ListAiModelConfigReqDto request, CancellationToken ct = default)
    {
        if (request.PageSize is < 1 or > 100)
            return new ListAiModelConfigResDto { Success = false, ErrorCode = PlatformErrorCodes.ParamInvalid };

        var pageIndex = request.PageIndex < 1 ? 1 : request.PageIndex;
        var pageSize = request.PageSize;

        var items = await ModelsDs.EntitySelectAsync(
            x => string.IsNullOrWhiteSpace(request.Name) || x.Name.Contains(request.Name),
            (pageIndex - 1) * pageSize,
            pageSize,
            q => q.OrderBy(x => x.SortOrder).ThenBy(x => x.Id),
            ct);
        var totalCount = await ModelsDs.CountAsync(
            x => string.IsNullOrWhiteSpace(request.Name) || x.Name.Contains(request.Name), ct);

        // 复用自动生成 AiModelConfigDto（ApiKey 已 [DtoFieldIgnore]，天然不含敏感字段）
        return new ListAiModelConfigResDto
        {
            Success = true,
            Items = items.Select(x => x.ToDto()).ToList(),
            Total = (int)totalCount,
        };
    }

    /// <summary>
    /// 新增模型配置（UId 生成；ApiKey 入库但不回显——平台-BR-01）
    /// </summary>
    public async Task<CreateAiModelConfigResDto> CreateModelAsync(
        CreateAiModelConfigReqDto request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name)
            || string.IsNullOrWhiteSpace(request.BaseUrl)
            || string.IsNullOrWhiteSpace(request.ApiKey)
            || string.IsNullOrWhiteSpace(request.ModelName))
            return new CreateAiModelConfigResDto { Success = false, ErrorCode = PlatformErrorCodes.ParamInvalid };

        if (!Enum.TryParse<AiProvider>(request.Provider, true, out var provider))
            return new CreateAiModelConfigResDto { Success = false, ErrorCode = PlatformErrorCodes.ParamInvalid };

        var model = new AiModelConfig
        {
            UId = UidGenerator.NewId(),
            Name = request.Name.Trim(),
            Provider = provider,
            BaseUrl = request.BaseUrl.Trim().TrimEnd('/'),
            ApiKey = request.ApiKey.Trim(),
            ModelName = request.ModelName.Trim(),
            Enabled = request.Enabled,
            SortOrder = request.SortOrder,
            TimeoutSeconds = request.TimeoutSeconds > 0 ? request.TimeoutSeconds : 30,
            Remark = string.IsNullOrWhiteSpace(request.Remark) ? null : request.Remark.Trim(),
        };
        await ModelsDs.EntityCreateAsync(model, ct);

        return new CreateAiModelConfigResDto { Success = true, UId = model.UId };
    }

    /// <summary>
    /// 更新模型配置（空 ApiKey = 不改；其余字段空值 = 保留原值）
    /// </summary>
    public async Task<UpdateAiModelConfigResDto> UpdateModelAsync(
        UpdateAiModelConfigReqDto request, CancellationToken ct = default)
    {
        var model = await ModelsDs.EntityGetAsync(x => x.UId == request.UId, ct);
        if (model == null)
            return new UpdateAiModelConfigResDto { Success = false, ErrorCode = PlatformErrorCodes.ModelNotFound };

        if (!string.IsNullOrWhiteSpace(request.Name))
            model.Name = request.Name.Trim();
        if (!string.IsNullOrWhiteSpace(request.Provider)
            && Enum.TryParse<AiProvider>(request.Provider, true, out var provider))
            model.Provider = provider;
        if (!string.IsNullOrWhiteSpace(request.BaseUrl))
            model.BaseUrl = request.BaseUrl.Trim().TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(request.ApiKey))
            model.ApiKey = request.ApiKey.Trim(); // 空 ApiKey = 不改（不回显、不覆盖）
        if (!string.IsNullOrWhiteSpace(request.ModelName))
            model.ModelName = request.ModelName.Trim();
        if (request.Enabled.HasValue)
            model.Enabled = request.Enabled.Value;
        if (request.SortOrder.HasValue)
            model.SortOrder = request.SortOrder.Value;
        if (request.TimeoutSeconds.HasValue && request.TimeoutSeconds.Value > 0)
            model.TimeoutSeconds = request.TimeoutSeconds.Value;
        if (request.Remark is not null)
            model.Remark = string.IsNullOrWhiteSpace(request.Remark) ? null : request.Remark.Trim();

        await ModelsDs.EntityUpdateAsync(model, ct);

        return new UpdateAiModelConfigResDto { Success = true, UId = model.UId };
    }

    /// <summary>
    /// 删除模型配置（按 UId）
    /// </summary>
    public async Task<DeleteAiModelConfigResDto> DeleteModelAsync(
        DeleteAiModelConfigReqDto request, CancellationToken ct = default)
    {
        var model = await ModelsDs.EntityGetAsync(x => x.UId == request.UId, ct);
        if (model == null)
            return new DeleteAiModelConfigResDto { Success = false, ErrorCode = PlatformErrorCodes.ModelNotFound };

        var deleted = await ModelsDs.EntityDeleteBatchAsync(new[] { model.Id }, ct);
        return new DeleteAiModelConfigResDto { Success = deleted > 0, Removed = deleted > 0 };
    }

    /// <summary>
    /// 启停模型配置（Enabled 即时生效于 LlmGateway 轮询）
    /// </summary>
    public async Task<SetEnabledResDto> SetEnabledAsync(
        SetEnabledReqDto request, CancellationToken ct = default)
    {
        var model = await ModelsDs.EntityGetAsync(x => x.UId == request.UId, ct);
        if (model == null)
            return new SetEnabledResDto { Success = false, ErrorCode = PlatformErrorCodes.ModelNotFound };

        model.Enabled = request.Enabled;
        await ModelsDs.EntityUpdateAsync(model, ct);

        return new SetEnabledResDto { Success = true, UId = model.UId, Enabled = model.Enabled };
    }
}

/// <summary>模型配置分页列表请求 DTO</summary>
public sealed record ListAiModelConfigReqDto
{
    /// <summary>配置名筛选（模糊）</summary>
    public string? Name { get; init; }

    /// <summary>页码（默认 1）</summary>
    public int PageIndex { get; init; } = 1;

    /// <summary>每页数（默认 20）</summary>
    public int PageSize { get; init; } = 20;
}

/// <summary>模型配置分页列表响应 DTO（不含 ApiKey——平台-BR-19）</summary>
public sealed record ListAiModelConfigResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>模型配置列表（复用 AiModelConfigDto，天然不含 ApiKey）</summary>
    public List<AiModelConfigDto> Items { get; init; } = [];

    /// <summary>总条数</summary>
    public int Total { get; init; }
}

/// <summary>新增模型配置请求 DTO</summary>
public sealed record CreateAiModelConfigReqDto
{
    /// <summary>配置名（≤64）</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>供应商（默认 OpenAiCompatible）</summary>
    public string Provider { get; init; } = nameof(AiProvider.OpenAiCompatible);

    /// <summary>在线 API BaseUrl（如 https://api.openai.com/v1）</summary>
    public string BaseUrl { get; init; } = string.Empty;

    /// <summary>API Key（敏感：仅入库，不回显）</summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>模型名</summary>
    public string ModelName { get; init; } = string.Empty;

    /// <summary>是否启用（默认 true）</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>轮询顺序（升序优先，默认 0）</summary>
    public int SortOrder { get; init; }

    /// <summary>请求超时秒数（默认 30）</summary>
    public int TimeoutSeconds { get; init; } = 30;

    /// <summary>备注（可空）</summary>
    public string? Remark { get; init; }
}

/// <summary>新增模型配置响应 DTO（回显 UId，不含 ApiKey）</summary>
public sealed record CreateAiModelConfigResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>新建配置业务键</summary>
    public string UId { get; init; } = string.Empty;
}

/// <summary>更新模型配置请求 DTO（空值 = 保留原值；ApiKey 空 = 不改）</summary>
public sealed record UpdateAiModelConfigReqDto
{
    /// <summary>目标配置业务键</summary>
    public string UId { get; init; } = string.Empty;

    /// <summary>配置名（可空 = 不改）</summary>
    public string? Name { get; init; }

    /// <summary>供应商（可空 = 不改）</summary>
    public string? Provider { get; init; }

    /// <summary>在线 API BaseUrl（可空 = 不改）</summary>
    public string? BaseUrl { get; init; }

    /// <summary>API Key（空 = 不改）</summary>
    public string? ApiKey { get; init; }

    /// <summary>模型名（可空 = 不改）</summary>
    public string? ModelName { get; init; }

    /// <summary>是否启用（null = 不改）</summary>
    public bool? Enabled { get; init; }

    /// <summary>轮询顺序（null = 不改）</summary>
    public int? SortOrder { get; init; }

    /// <summary>请求超时秒数（null 或 ≤0 = 不改）</summary>
    public int? TimeoutSeconds { get; init; }

    /// <summary>备注（null = 不改；空串 = 清空）</summary>
    public string? Remark { get; init; }
}

/// <summary>更新模型配置响应 DTO（不含 ApiKey）</summary>
public sealed record UpdateAiModelConfigResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>更新配置业务键</summary>
    public string UId { get; init; } = string.Empty;
}

/// <summary>删除模型配置请求 DTO</summary>
public sealed record DeleteAiModelConfigReqDto
{
    /// <summary>目标配置业务键</summary>
    public string UId { get; init; } = string.Empty;
}

/// <summary>删除模型配置响应 DTO</summary>
public sealed record DeleteAiModelConfigResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>是否已删除</summary>
    public bool Removed { get; init; }
}

/// <summary>启停模型配置请求 DTO</summary>
public sealed record SetEnabledReqDto
{
    /// <summary>目标配置业务键</summary>
    public string UId { get; init; } = string.Empty;

    /// <summary>开关值</summary>
    public bool Enabled { get; init; }
}

/// <summary>启停模型配置响应 DTO</summary>
public sealed record SetEnabledResDto
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>错误码（失败时）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>目标配置业务键</summary>
    public string UId { get; init; } = string.Empty;

    /// <summary>更新后开关值</summary>
    public bool Enabled { get; init; }
}

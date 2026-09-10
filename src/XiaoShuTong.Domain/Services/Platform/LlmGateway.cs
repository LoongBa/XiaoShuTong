using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TKW.Framework.Domain;
using XiaoShuTong.DataServices.Platform;
using XiaoShuTong.Entities.Platform;

namespace XiaoShuTong.Services.Platform;

/// <summary>
/// LLM 统一网关（基础设施服务，非 Controller）：OpenAI 兼容协议 + round-robin 轮询 + 降级信号
/// </summary>
/// <remarks>
/// 平台-BR-02 多模型轮询（启用模型按 SortOrder 升序，单模型失败自动切换下一模型）
/// 平台-BR-03 全部模型失败 → 返回降级信号（IsDegraded），不抛错给调用方
/// 平台-BR-04 无启用模型 → 直接降级，不阻塞业务调用
/// 平台-BR-01 ApiKey 仅服务端使用，不下发前端、不入日志
/// </remarks>
internal class LlmGateway(DomainUser<XiaoShuTongUserInfo> user)
    : DomainServiceBase<XiaoShuTongUserInfo>(user)
{
    private const double DefaultTemperature = 0.2;

    /// <summary>静态共享 HttpClient（连接复用；每次调用的超时由 Linked CTS 控制）</summary>
    private static readonly HttpClient Http = new() { Timeout = Timeout.InfiniteTimeSpan };

    /// <summary>round-robin 全局游标（平台-BR-20：静态计数 + Interlocked 原子递增）</summary>
    private static int _roundRobinCursor = -1;

    private AiModelConfigDataService? _modelsDs;
    private AiModelConfigDataService ModelsDs => _modelsDs ??= User.Use<AiModelConfigDataService>();

    /// <summary>
    /// OpenAI 兼容补全：按启用模型 round-robin 轮询，任一成功即返回；全部失败返回降级信号
    /// </summary>
    public async Task<LlmCompletionResult> CompleteAsync(
        string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        // 平台-BR-04：无启用模型或网关未配置 → 直接降级，不阻塞业务
        var enabled = (await ModelsDs.EntitySelectAsync(x => x.Enabled, ct: ct))
            .OrderBy(x => x.SortOrder)
            .ToList();
        if (enabled.Count == 0)
            return LlmCompletionResult.Degraded;

        // 平台-BR-20：round-robin 起始位置（全局轮询，避免每次都从 SortOrder 最小模型开始）
        var start = (int)(((uint)Interlocked.Increment(ref _roundRobinCursor)) % (uint)enabled.Count);

        // 平台-BR-02：从起始位置按序尝试；单模型失败（网络/超时/5xx）→ 切换下一模型
        for (var i = 0; i < enabled.Count; i++)
        {
            var model = enabled[(start + i) % enabled.Count];
            var content = await TryCompleteWithModelAsync(model, systemPrompt, userPrompt, ct);
            if (content != null)
                return new LlmCompletionResult { Success = true, Content = content };
        }

        // 平台-BR-03：全部模型失败 → 降级信号（不抛错，调用方走本地兜底）
        return LlmCompletionResult.Degraded;
    }

    /// <summary>
    /// 单模型 OpenAI 兼容调用（POST {BaseUrl}/chat/completions，Bearer 鉴权）
    /// </summary>
    /// <returns>模型返回正文；调用失败（网络/超时/非 2xx/响应不可解析）返回 null → 切换下一模型</returns>
    private static async Task<string?> TryCompleteWithModelAsync(
        AiModelConfig model, string systemPrompt, string userPrompt, CancellationToken ct)
    {
        try
        {
            var endpoint = $"{model.BaseUrl.TrimEnd('/')}/chat/completions";
            var body = JsonSerializer.Serialize(new ChatRequestBody(
                model.ModelName,
                [new ChatMessage("system", systemPrompt), new ChatMessage("user", userPrompt)],
                DefaultTemperature));

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", model.ApiKey);
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");

            // 超时：按模型 TimeoutSeconds（默认 30s）控制本次请求
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(model.TimeoutSeconds > 0 ? model.TimeoutSeconds : 30));

            using var response = await Http.SendAsync(request, timeoutCts.Token);
            if (!response.IsSuccessStatusCode)
                return null; // 5xx / 4xx（鉴权失败等）→ 该模型不可用，切换下一模型

            var json = await response.Content.ReadAsStringAsync(timeoutCts.Token);
            return ExtractContent(json);
        }
        catch (Exception ex) when (ex is HttpRequestException
            or TaskCanceledException or OperationCanceledException or JsonException)
        {
            return null; // 网络错误/超时 → 切换下一模型
        }
    }

    /// <summary>解析响应 choices[0].message.content；无法解析返回 null</summary>
    private static string? ExtractContent(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
            return null;
        if (!choices[0].TryGetProperty("message", out var message))
            return null;
        if (!message.TryGetProperty("content", out var content))
            return null;
        var text = content.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }
}

/// <summary>LLM 补全结果（降级信号契约）</summary>
public sealed record LlmCompletionResult
{
    /// <summary>是否成功（任一启用模型返回内容）</summary>
    public bool Success { get; init; }

    /// <summary>模型返回正文</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>是否降级（全部失败 / 无启用模型 → true，调用方走本地兜底）</summary>
    public bool IsDegraded { get; init; }

    /// <summary>降级信号（Success=false, IsDegraded=true）</summary>
    public static LlmCompletionResult Degraded { get; } = new() { Success = false, IsDegraded = true };
}

/// <summary>OpenAI 兼容 chat/completions 请求体（HTTP wire 格式，文件内联私有）</summary>
internal sealed record ChatRequestBody(string Model, List<ChatMessage> Messages, double Temperature);

/// <summary>chat 消息（HTTP wire 格式，文件内联私有）</summary>
internal sealed record ChatMessage(string Role, string Content);

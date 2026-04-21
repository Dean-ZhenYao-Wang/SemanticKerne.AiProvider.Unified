using System.Text;
using System.Text.Json;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Logging;
using StressTest.Models;
using System.Globalization;

namespace StressTest.Services;

public class SseClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SseClient> _logger;
    private readonly CsvConfiguration _csvConfig;

    public SseClient(HttpClient httpClient, ILogger<SseClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            Delimiter = ","
        };
    }

    public async Task<RequestMetrics> StreamChatAsync(
        string baseUrl,
        string sessionId,
        string message,
        int userId,
        int requestId,
        int requestNumber,
        CancellationToken cancellationToken = default)
    {
        var metrics = new RequestMetrics
        {
            RequestId = requestId,
            UserId = userId,
            RequestNumber = requestNumber,
            StartTime = DateTime.UtcNow
        };

        _logger.LogInformation("[请求 #{RequestId}] 用户 {UserId} 发送第 {RequestNum} 个请求: {Message}",
            requestId, userId, requestNumber, message);

        try
        {
            var url = $"{baseUrl}/api/chat/sessions/{sessionId}/chat";
            var content = new StringContent(
                JsonSerializer.Serialize(new { message }),
                Encoding.UTF8,
                "application/json");

            using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };

            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            metrics.StatusCode = (int)response.StatusCode;

            if (!response.IsSuccessStatusCode)
            {
                metrics.ErrorMessage = await response.Content.ReadAsStringAsync(cancellationToken);
                metrics.IsSuccess = false;
                metrics.EndTime = DateTime.UtcNow;
                _logger.LogWarning("[请求 #{RequestId}] 失败: {StatusCode} - {Error}",
                    requestId, metrics.StatusCode, metrics.ErrorMessage);
                return metrics;
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream, Encoding.UTF8);

            string? line;
            var firstChunkReceived = false;

            while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                if (line.StartsWith("data: "))
                {
                    var data = line[6..].Trim();

                    if (data == "[DONE]")
                    {
                        break;
                    }

                    try
                    {
                        var json = JsonDocument.Parse(data);
                        var root = json.RootElement;

                        if (root.TryGetProperty("type", out var typeProp))
                        {
                            var type = typeProp.GetString();
                            metrics.MessageTypes[type ?? "unknown"] =
                                metrics.MessageTypes.GetValueOrDefault(type ?? "unknown", 0) + 1;

                            if (root.TryGetProperty("content", out var contentProp))
                            {
                                var contentText = contentProp.GetString();
                                if (!string.IsNullOrEmpty(contentText))
                                {
                                    if (!firstChunkReceived)
                                    {
                                        firstChunkReceived = true;
                                        metrics.FirstTokenTime = DateTime.UtcNow;
                                        var ttft = metrics.TtftMs!.Value;
                                        _logger.LogInformation("[请求 #{RequestId}] 首字延迟 (TTFT): {Ttft:F2} ms",
                                            requestId, ttft);
                                    }

                                    metrics.TotalCharacters += contentText.Length;
                                    metrics.ChunksReceived++;

                                    if (type == "thinking")
                                    {
                                        metrics.ThinkingCharacters += contentText.Length;
                                    }
                                    else if (type == "content")
                                    {
                                        metrics.ContentCharacters += contentText.Length;
                                    }
                                }
                            }

                            if (root.TryGetProperty("errorCode", out var errorCodeProp))
                            {
                                metrics.ErrorMessage = $"错误代码: {errorCodeProp.GetString()}";
                                metrics.IsSuccess = false;
                            }
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "[请求 #{RequestId}] 解析 SSE 数据失败: {Data}", requestId, data);
                    }
                }
            }

            metrics.IsSuccess = true;
            metrics.EndTime = DateTime.UtcNow;

            _logger.LogInformation("[请求 #{RequestId}] 完成 - 总时长: {Duration:F2} ms, 字符数: {Chars}, " +
                "TTFT: {Ttft:F2} ms, 吞吐量: {Throughput:F2} 字符/秒",
                requestId, metrics.TotalDurationMs, metrics.TotalCharacters,
                metrics.TtftMs, metrics.CharactersPerSecond);

            return metrics;
        }
        catch (Exception ex)
        {
            metrics.IsSuccess = false;
            metrics.ErrorMessage = ex.Message;
            metrics.EndTime = DateTime.UtcNow;
            _logger.LogError(ex, "[请求 #{RequestId}] 请求失败", requestId);
            return metrics;
        }
    }
}

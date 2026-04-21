using System.Diagnostics;
using System.Text.Json;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Logging;
using StressTest.Models;
using System.Globalization;

namespace StressTest.Services;

public class StressTestService
{
    private readonly HttpClient _httpClient;
    private readonly SseClient _sseClient;
    private readonly ILogger<StressTestService> _logger;
    private readonly StressTestOptions _options;
    private readonly CsvConfiguration _csvConfig;
    private readonly object _requestIdLock = new object();
    private int _nextRequestId = 1;

    public StressTestService(
        HttpClient httpClient,
        SseClient sseClient,
        ILogger<StressTestService> logger,
        StressTestOptions options)
    {
        _httpClient = httpClient;
        _sseClient = sseClient;
        _logger = logger;
        _options = options;
        _csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            Delimiter = ","
        };
    }

    public async Task<PerformanceReport> RunStressTestAsync(CancellationToken cancellationToken = default)
    {
        var report = new PerformanceReport
        {
            TestStartTime = DateTime.UtcNow
        };

        var stopwatch = Stopwatch.StartNew();
        var cts = new CancellationTokenSource(TimeSpan.FromMinutes(_options.TestDurationMinutes));
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);

        _logger.LogInformation("开始压力测试");
        _logger.LogInformation("并发用户数: {ConcurrentUsers}", _options.ConcurrentUsers);
        _logger.LogInformation("每用户请求数: {RequestsPerUser}", _options.RequestsPerUser);
        _logger.LogInformation("测试时长: {TestDurationMinutes} 分钟", _options.TestDurationMinutes);
        _logger.LogInformation("总请求数: {TotalRequests}",
            _options.ConcurrentUsers * _options.RequestsPerUser);

        var userTasks = new List<Task<List<RequestMetrics>>>();

        for (int i = 0; i < _options.ConcurrentUsers; i++)
        {
            var userId = i + 1;
            var userTask = SimulateUserAsync(userId, linkedCts.Token);
            userTasks.Add(userTask);
        }

        try
        {
            var allUserResults = await Task.WhenAll(userTasks);

            foreach (var userResults in allUserResults)
            {
                foreach (var result in userResults)
                {
                    report.TotalRequests++;
                    report.ResponseTimes.Add(result.TotalDurationMs);
                    report.AllMetrics.Add(result);

                    if (result.IsSuccess)
                    {
                        report.SuccessfulRequests++;
                        report.TotalCharacters += result.TotalCharacters;
                    }
                    else
                    {
                        report.FailedRequests++;
                        if (!string.IsNullOrEmpty(result.ErrorMessage))
                        {
                            report.ErrorMessages.Add(result.ErrorMessage);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "压力测试执行失败");
        }
        finally
        {
            stopwatch.Stop();
            report.TestEndTime = DateTime.UtcNow;
            report.CalculateMetrics();

            if (_options.EnableCsvLogging)
            {
                await ExportToCsvAsync(report);
            }
        }

        return report;
    }

    private async Task<List<RequestMetrics>> SimulateUserAsync(
        int userId,
        CancellationToken cancellationToken)
    {
        var userTasks = new List<Task<RequestMetrics>>();
        var sessionId = await CreateSessionAsync(cancellationToken);

        _logger.LogInformation("[用户 {UserId}] 开始测试，SessionId: {SessionId}", userId, sessionId);

        for (int i = 0; i < _options.RequestsPerUser; i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("[用户 {UserId}] 测试被取消", userId);
                break;
            }

            int currentRequestId;
            lock (_requestIdLock)
            {
                currentRequestId = _nextRequestId++;
            }

            var messageIndex = i % _options.TestMessages.Count;
            var message = _options.TestMessages[messageIndex];

            _logger.LogDebug("[用户 {UserId}] 发送第 {RequestNum} 个请求 (#{RequestId}): {Message}",
                userId, i + 1, currentRequestId, message);

            var task = _sseClient.StreamChatAsync(
                _options.BaseUrl,
                sessionId,
                message,
                userId,
                currentRequestId,
                i + 1,
                cancellationToken);

            userTasks.Add(task);

            await Task.Delay(100, cancellationToken);
        }

        var results = await Task.WhenAll(userTasks);
        return results.ToList();
    }

    private async Task<string> CreateSessionAsync(CancellationToken cancellationToken)
    {
        var url = $"{_options.BaseUrl}/api/chat/sessions";
        var response = await _httpClient.PostAsync(url, null, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"创建会话失败: {response.StatusCode}");
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var json = JsonDocument.Parse(content);
        var sessionId = json.RootElement.GetProperty("sessionId").GetString();

        return sessionId ?? Guid.NewGuid().ToString();
    }

    private async Task ExportToCsvAsync(PerformanceReport report)
    {
        try
        {
            var csvPath = _options.CsvOutputPath;
            _logger.LogInformation("正在导出 CSV 报告到: {Path}", csvPath);

            using var writer = new StreamWriter(csvPath, false, System.Text.Encoding.UTF8);
            using var csv = new CsvWriter(writer, _csvConfig);

            csv.WriteRecords(report.AllMetrics);

            _logger.LogInformation("CSV 报告导出成功，共 {Count} 条记录", report.AllMetrics.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导出 CSV 报告失败");
        }
    }
}

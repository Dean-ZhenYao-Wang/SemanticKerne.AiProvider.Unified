namespace StressTest.Models;

public class PerformanceReport
{
    public DateTime TestStartTime { get; set; }
    public DateTime TestEndTime { get; set; }
    public int TotalRequests { get; set; }
    public int SuccessfulRequests { get; set; }
    public int FailedRequests { get; set; }
    public double SuccessRate { get; set; }
    public double AverageResponseTimeMs { get; set; }
    public double MinResponseTimeMs { get; set; }
    public double MaxResponseTimeMs { get; set; }
    public double RequestsPerSecond { get; set; }
    public int TotalCharacters { get; set; }
    public double AverageCharactersPerSecond { get; set; }
    public List<double> ResponseTimes { get; set; } = new();
    public List<string> ErrorMessages { get; set; } = new();
    public List<RequestMetrics> AllMetrics { get; set; } = new();

    public TimeSpan Duration => TestEndTime - TestStartTime;

    public double AverageTtftMs { get; set; }
    public double MinTtftMs { get; set; }
    public double MaxTtftMs { get; set; }
    public double TtftP50Ms { get; set; }
    public double TtftP95Ms { get; set; }
    public double TtftP99Ms { get; set; }

    public void CalculateMetrics()
    {
        if (ResponseTimes.Count == 0) return;

        AverageResponseTimeMs = ResponseTimes.Average();
        MinResponseTimeMs = ResponseTimes.Min();
        MaxResponseTimeMs = ResponseTimes.Max();

        var ttftValues = AllMetrics
            .Where(m => m.TtftMs.HasValue)
            .Select(m => m.TtftMs!.Value)
            .ToList();

        if (ttftValues.Count > 0)
        {
            AverageTtftMs = ttftValues.Average();
            MinTtftMs = ttftValues.Min();
            MaxTtftMs = ttftValues.Max();

            var sorted = ttftValues.OrderBy(x => x).ToList();
            TtftP50Ms = CalculatePercentile(sorted, 0.50);
            TtftP95Ms = CalculatePercentile(sorted, 0.95);
            TtftP99Ms = CalculatePercentile(sorted, 0.99);
        }

        if (Duration.TotalSeconds > 0)
        {
            RequestsPerSecond = TotalRequests / Duration.TotalSeconds;
            AverageCharactersPerSecond = TotalCharacters / Duration.TotalSeconds;
        }
    }

    private double CalculatePercentile(List<double> sortedValues, double percentile)
    {
        if (sortedValues.Count == 0) return 0;
        int index = (int)Math.Ceiling(sortedValues.Count * percentile) - 1;
        return sortedValues[Math.Max(0, Math.Min(index, sortedValues.Count - 1))];
    }

    public void PrintSummary()
    {
        Console.WriteLine("\n" + new string('=', 100));
        Console.WriteLine("压力测试结果汇总");
        Console.WriteLine(new string('=', 100));
        Console.WriteLine($"测试时长: {Duration.TotalSeconds:F2} 秒");
        Console.WriteLine($"总请求数: {TotalRequests}");
        Console.WriteLine($"成功请求: {SuccessfulRequests}");
        Console.WriteLine($"失败请求: {FailedRequests}");
        Console.WriteLine($"成功率: {SuccessRate:F2}%");
        Console.WriteLine($"吞吐量: {RequestsPerSecond:F2} 请求/秒");
        Console.WriteLine();
        Console.WriteLine("响应时间统计:");
        Console.WriteLine($"  平均响应时间: {AverageResponseTimeMs:F2} ms");
        Console.WriteLine($"  最小响应时间: {MinResponseTimeMs:F2} ms");
        Console.WriteLine($"  最大响应时间: {MaxResponseTimeMs:F2} ms");
        Console.WriteLine();
        Console.WriteLine("TTFT（首字延迟）统计:");
        Console.WriteLine($"  平均 TTFT: {AverageTtftMs:F2} ms");
        Console.WriteLine($"  最小 TTFT: {MinTtftMs:F2} ms");
        Console.WriteLine($"  最大 TTFT: {MaxTtftMs:F2} ms");
        Console.WriteLine($"  TTFT P50: {TtftP50Ms:F2} ms");
        Console.WriteLine($"  TTFT P95: {TtftP95Ms:F2} ms");
        Console.WriteLine($"  TTFT P99: {TtftP99Ms:F2} ms");
        Console.WriteLine();
        Console.WriteLine("内容统计:");
        Console.WriteLine($"  总字符数: {TotalCharacters}");
        Console.WriteLine($"  平均字符速率: {AverageCharactersPerSecond:F2} 字符/秒");
        Console.WriteLine(new string('=', 100));

        if (ErrorMessages.Count > 0)
        {
            Console.WriteLine("\n错误信息:");
            foreach (var error in ErrorMessages.Take(10))
            {
                Console.WriteLine($"  - {error}");
            }
            if (ErrorMessages.Count > 10)
            {
                Console.WriteLine($"  ... 还有 {ErrorMessages.Count - 10} 个错误");
            }
        }
    }
}

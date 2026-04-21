using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StressTest.Models;
using StressTest.Services;

using IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((context, config) =>
    {
        config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
    })
    .ConfigureServices((context, services) =>
    {
        var configuration = context.Configuration;
        var options = configuration.GetSection("StressTest").Get<StressTestOptions>()
            ?? new StressTestOptions();

        services.AddSingleton(options);
        services.AddHttpClient();
        services.AddSingleton<SseClient>();
        services.AddSingleton<StressTestService>();
    })
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();
        logging.SetMinimumLevel(LogLevel.Information);
    })
    .Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
var options = host.Services.GetRequiredService<StressTestOptions>();
var stressTestService = host.Services.GetRequiredService<StressTestService>();

try
{
    Console.WriteLine("\n" + new string('=', 100));
    Console.WriteLine("AI 聊天服务压力测试工具 - TTFT（首字延迟）统计版");
    Console.WriteLine(new string('=', 100));
    Console.WriteLine($"目标地址: {options.BaseUrl}");
    Console.WriteLine($"并发用户数: {options.ConcurrentUsers}");
    Console.WriteLine($"每用户请求数: {options.RequestsPerUser}");
    Console.WriteLine($"测试时长: {options.TestDurationMinutes} 分钟");
    Console.WriteLine($"总请求数: {options.ConcurrentUsers * options.RequestsPerUser}");
    Console.WriteLine($"CSV 日志: {(options.EnableCsvLogging ? "启用" : "禁用")}");
    Console.WriteLine($"CSV 输出路径: {options.CsvOutputPath}");
    Console.WriteLine(new string('=', 100) + "\n");

    Console.WriteLine("按任意键开始测试...");
    Console.ReadKey();

    var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (sender, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
        Console.WriteLine("\n正在取消测试...");
    };

    var report = await stressTestService.RunStressTestAsync(cts.Token);

    report.PrintSummary();

    Console.WriteLine("\n按任意键退出...");
    Console.ReadKey();
}
catch (Exception ex)
{
    logger.LogError(ex, "压力测试执行失败");
    Console.WriteLine($"\n错误: {ex.Message}");
    Console.WriteLine("按任意键退出...");
    Console.ReadKey();
    Environment.Exit(1);
}

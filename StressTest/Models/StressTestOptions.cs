namespace StressTest.Models;

public class StressTestOptions
{
    public string BaseUrl { get; set; } = "http://localhost:5000";
    public int ConcurrentUsers { get; set; } = 10;
    public int RequestsPerUser { get; set; } = 5;
    public int TestDurationMinutes { get; set; } = 5;
    public List<string> TestMessages { get; set; } = new();
    public bool EnableCsvLogging { get; set; } = true;
    public string CsvOutputPath { get; set; } = "stresstest_results.csv";
}

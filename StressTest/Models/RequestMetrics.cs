namespace StressTest.Models;

public class RequestMetrics
{
    public int RequestId { get; set; }
    public int UserId { get; set; }
    public int RequestNumber { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? FirstTokenTime { get; set; }
    public DateTime EndTime { get; set; }
    public bool IsSuccess { get; set; }
    public int StatusCode { get; set; }
    public string? ErrorMessage { get; set; }
    public int ChunksReceived { get; set; }
    public int TotalCharacters { get; set; }
    public int ThinkingCharacters { get; set; }
    public int ContentCharacters { get; set; }
    public Dictionary<string, int> MessageTypes { get; set; } = new();

    public double TotalDurationMs => (EndTime - StartTime).TotalMilliseconds;
    public double? TtftMs => FirstTokenTime.HasValue ? (FirstTokenTime.Value - StartTime).TotalMilliseconds : null;
    public double CharactersPerSecond => TotalDurationMs > 0 ? (TotalCharacters / TotalDurationMs) * 1000 : 0;
}

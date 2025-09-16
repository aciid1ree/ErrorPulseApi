namespace ErrorPulseApi.Models;

public class ErrorInfo(
    DateTime timestamp,
    string severity,
    string product,
    string version,
    string errorCode)
{
    public DateTime Timestamp { get; } = timestamp;
    public string Severity { get; } = severity;
    public string Product { get; } = product;
    public string Version { get; } = version;
    public string ErrorCode { get; } = errorCode;
}
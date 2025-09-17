namespace ErrorPulseApi.Models;

public class ErrorInfo
{
    public ErrorInfo(
        DateTime timestamp, 
        string severity, 
        string product, 
        string version,
        string errorCode)
    {
        Timestamp = timestamp;
        Severity = severity;
        Product = product;
        Version = version;
        ErrorCode = errorCode;
    } 
    
    public ErrorInfo() { } 

    public DateTime Timestamp { get; set; }
    public string Severity { get; set; }
    public string Product { get; set; }
    public string Version { get; set; }
    public string ErrorCode { get; set; }
}

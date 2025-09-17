namespace ErrorPulseApi.Configuration;

public record DataFoldersOptions
{
    public string ErrorDirDataPath { get; init; } = string.Empty; 
    public string AnalyticsDirDataPath { get; init; } = string.Empty; 
}

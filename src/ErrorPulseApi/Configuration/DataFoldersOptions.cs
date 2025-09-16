namespace ErrorPulseApi.Configuration;

public record DataFoldersOptions
{
    public string CsvDataPath { get; init; } = string.Empty; 
}

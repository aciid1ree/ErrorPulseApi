namespace ErrorPulseApi.Configuration;

public record GenerationOptions
{
    public int Rows { get; init; } 
    public int RandomSeed { get; init; }
    public string[] Severity { get; init; } = [];
    public string ProductsPath { get; init; } = string.Empty;
    public string VersionsPath { get; init; } = string.Empty;
    public string ErrorCodesPath { get; init; } = string.Empty;
}
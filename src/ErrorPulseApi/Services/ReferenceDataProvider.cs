using System.Text.Json;
using ErrorPulseApi.Configuration;
using ErrorPulseApi.Models;
using Microsoft.Extensions.Options;

namespace ErrorPulseApi.Services;

public interface IReferenceDataProvider
{
    ReferenceData GetReferenceData();
}

public class ReferenceDataProvider : IReferenceDataProvider
{
    private readonly ReferenceData _referenceData;

    public ReferenceDataProvider(IOptions<GenerationOptions> generationOptions)
    {
        var options = generationOptions.Value;

        _referenceData = new ReferenceData(
            options.Rows,
            options.RandomSeed,
            options.Severity,
            LoadProducts(options.ProductsPath),
            LoadVersions(options.VersionsPath),
            LoadErrorCodes(options.ErrorCodesPath)
        );
    }

    public ReferenceData GetReferenceData() => _referenceData;
    
    private static string[] LoadProducts(string path)
        => JsonSerializer.Deserialize<string[]>(File.ReadAllText(path)) 
           ?? throw new InvalidOperationException($"Products file {path} is empty or invalid.");

    private static Dictionary<string, string[]> LoadVersions(string path)
        => JsonSerializer.Deserialize<Dictionary<string, string[]>>(File.ReadAllText(path))
           ?? throw new InvalidOperationException($"Versions file {path} is empty or invalid.");

    private static string[] LoadErrorCodes(string path)
        => JsonSerializer.Deserialize<string[]>(File.ReadAllText(path)) 
           ?? throw new InvalidOperationException($"ErrorCodes file {path} is empty or invalid.");
}
namespace ErrorPulseApi.Models;

public record ReferenceData(
    int Rows,
    int RandomSeed,
    string[] Severity,
    string[] Products,
    Dictionary<string, string[]> VersionsMap,
    string[] ErrorCodes
);
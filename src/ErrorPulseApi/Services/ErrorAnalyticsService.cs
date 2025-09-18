using System.Collections.Concurrent;
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using ErrorPulseApi.Configuration;
using ErrorPulseApi.Constants;
using ErrorPulseApi.Models;
using Microsoft.Extensions.Options;

namespace ErrorPulseApi.Services;

public interface IErrorAnalyticsService
{
    Task<bool> CreateAnalyticsFiles();
}

public class ErrorAnalyticsService : IErrorAnalyticsService
{
    private readonly DataFoldersOptions _dataFoldersOptions;
    private readonly ILogger<ErrorAnalyticsService> _logger;

    private readonly ConcurrentDictionary<string, int> _errorBySeverity;
    private readonly ConcurrentDictionary<string, int> _errorByProductVersion;
    private readonly ConcurrentDictionary<string, int> _topErrorCodes;
    private readonly ConcurrentDictionary<string, Dictionary<int, int>> _topErrorCodesByHour;

    public ErrorAnalyticsService(
        IOptions<DataFoldersOptions> dataFoldersOptions,
        ILogger<ErrorAnalyticsService> logger)
    {
        _errorBySeverity = new ConcurrentDictionary<string, int>();
        _errorByProductVersion = new ConcurrentDictionary<string, int>();
        _topErrorCodes = new ConcurrentDictionary<string, int>();
        _topErrorCodesByHour = new ConcurrentDictionary<string, Dictionary<int, int>>();

        _dataFoldersOptions = dataFoldersOptions.Value;
        _logger = logger;
    }

    public async Task<bool> CreateAnalyticsFiles()
    {
        var filePath = GetCsvFilePath();
        if (filePath == null) return false;

        await ProcessCsvFile(filePath);

        await SaveAnalytics();

        _logger.LogInformation("CSV analytics saved successfully.");
        return true;
    }

    private string? GetCsvFilePath()
    {
        if (string.IsNullOrWhiteSpace(_dataFoldersOptions.ErrorDirDataPath))
        {
            _logger.LogError("ErrorDirDataPath is not configured.");
            return null;
        }

        Directory.CreateDirectory(_dataFoldersOptions.ErrorDirDataPath);

        var filePath = Directory.GetFiles(_dataFoldersOptions.ErrorDirDataPath)
            .LastOrDefault(f => Path.GetExtension(f).Equals(".csv", StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrEmpty(filePath))
            _logger.LogError("No CSV data file found. Skipping analytics.");

        return filePath;
    }

    private async Task ProcessCsvFile(string filePath)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            TrimOptions = TrimOptions.Trim,
            MissingFieldFound = null,
            HeaderValidated = null
        };

        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, config);

        await foreach (var record in csv.GetRecordsAsync<ErrorInfo>())
        {
            _errorBySeverity.AddOrUpdate(record.Severity, 1, (_, old) => old + 1);
            
            var productKey = $"{record.Product}_{record.Version}";
            _errorByProductVersion.AddOrUpdate(productKey, 1, (_, old) => old + 1);
      
            var errorKey = $"{record.Product}_{record.Severity}_{record.ErrorCode}";
            _topErrorCodes.AddOrUpdate(errorKey, 1, (_, old) => old + 1);
            
            var hour = ((DateTime)record.Timestamp).Hour;
            var innerDict = _topErrorCodesByHour.GetOrAdd(errorKey, _ => new Dictionary<int, int>());
            lock (innerDict)
            {
                if (innerDict.ContainsKey(hour))
                    innerDict[hour]++;
                else
                    innerDict[hour] = 1;
            }
        }
    }

    private async Task SaveAnalytics()
    {
        await SaveSeverityResults(AnalyticsFileNames.ErrorBySeverityName, _errorBySeverity);
        await SaveProductsWithVersionResults(AnalyticsFileNames.ErrorByProductVersionName, _errorByProductVersion);
        await SaveTopErrorResults(AnalyticsFileNames.TopErrorCodesName, _topErrorCodes);
        await SaveTopErrorByHourResults(AnalyticsFileNames.TopErrorByHourCodesName, _topErrorCodesByHour);
    }

    private async Task SaveSeverityResults(string fileName, ConcurrentDictionary<string, int> data)
    {
        var path = GetPath(fileName);
        await using var writer = new StreamWriter(path);
        await writer.WriteLineAsync("Severity,Count");

        foreach (var kvp in data.OrderByDescending(x => x.Value))
        {
            await writer.WriteLineAsync($"{kvp.Key},{kvp.Value}");
        }

        _logger.LogInformation($"CSV analytics {fileName} saved at path: {path}.");
    }

    private async Task SaveProductsWithVersionResults(string fileName, ConcurrentDictionary<string, int> data)
    {
        var path = GetPath(fileName);
        await using var writer = new StreamWriter(path);
        await writer.WriteLineAsync("Product,Version,Count");

        foreach (var kvp in data.OrderByDescending(x => x.Value))
        {
            var parts = kvp.Key.Split('_');
            await writer.WriteLineAsync($"{parts[0]},{parts[1]},{kvp.Value}");
        }

        _logger.LogInformation($"CSV analytics {fileName} saved at path: {path}.");
    }

    private async Task SaveTopErrorResults(string fileName, ConcurrentDictionary<string, int> data)
    {
        var path = GetPath(fileName);
        var topErrors = _topErrorCodes.OrderByDescending(kv => kv.Value).Take(10).ToList();

        await using var writer = new StreamWriter(path);
        await writer.WriteLineAsync("Product,Severity,ErrorCode,Count");

        foreach (var kvp in topErrors)
        {
            var parts = kvp.Key.Split('_');
            await writer.WriteLineAsync($"{parts[0]},{parts[1]},{parts[2]},{kvp.Value}");
        }

        _logger.LogInformation($"CSV analytics {fileName} saved at path: {path}.");
    }

    private async Task SaveTopErrorByHourResults(string fileName, ConcurrentDictionary<string, Dictionary<int, int>> data)
    {
        var path = GetPath(fileName);
        await using var writer = new StreamWriter(path);
        await writer.WriteLineAsync("Period,Product,Severity,ErrorCode,Count");

        var result = data.ToArray()
            .Select(kvp =>
            {
                var key = kvp.Key;
                var hourly = kvp.Value;
                var best = hourly.Aggregate((max, cur) => cur.Value > max.Value ? cur : max);
                var periodLabel = $"{best.Key:00}:00 - {(best.Key + 1) % 24:00}:00";
                return (Key: key, Hour: periodLabel, Total: best.Value);
            })
            .OrderByDescending(x => x.Total)
            .ToList();

        foreach (var kvp in result)
        {
            var parts = kvp.Key.Split('_');
            await writer.WriteLineAsync($"{kvp.Hour},{parts[0]},{parts[1]},{parts[2]},{kvp.Total}");
        }

        _logger.LogInformation($"CSV analytics {fileName} saved at path: {path}.");
    }

    private string GetPath(string fileName)
    {
        fileName += $"_{DateTime.Now:yyyyMMddHHmmss}.csv";
        Directory.CreateDirectory(_dataFoldersOptions.AnalyticsDirDataPath);
        return Path.Combine(_dataFoldersOptions.AnalyticsDirDataPath, fileName);
    }
}

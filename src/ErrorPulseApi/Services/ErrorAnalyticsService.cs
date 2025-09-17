using System.Collections.Concurrent;
using System.Globalization;
using System.Threading.Channels;
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

    public ErrorAnalyticsService(
        IOptions<DataFoldersOptions> dataFoldersOptions,
        ILogger<ErrorAnalyticsService> logger)
    {
        _errorBySeverity = new ConcurrentDictionary<string, int>();
        _errorByProductVersion = new ConcurrentDictionary<string, int>();
        _topErrorCodes = new ConcurrentDictionary<string, int>();
        
        _dataFoldersOptions = dataFoldersOptions.Value;
        _logger = logger;   
    }

    public async Task<bool> CreateAnalyticsFiles()
    {
        var filePath = GetCsvFilePath();
        if (filePath == null) return false;

        await ProcessCsvFileParallel(filePath);

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

    private async Task ProcessCsvFileParallel(string filePath)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            TrimOptions = TrimOptions.Trim,
            MissingFieldFound = null,
            HeaderValidated = null
        };

        var severityChannel = Channel.CreateUnbounded<ErrorInfo>();
        var productChannel = Channel.CreateUnbounded<ErrorInfo>();
        var topErrorChannel = Channel.CreateUnbounded<ErrorInfo>();
        
        var severityTask = AggregateSeverity(severityChannel);
        var productTask = AggregateProductVersion(productChannel);
        var topErrorTask = AggregateTopErrors(topErrorChannel);
        
        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, config);

        await foreach (var record in csv.GetRecordsAsync<ErrorInfo>())
        {
            await severityChannel.Writer.WriteAsync(record);
            await productChannel.Writer.WriteAsync(record);
            await topErrorChannel.Writer.WriteAsync(record);
        }
        
        severityChannel.Writer.Complete();
        productChannel.Writer.Complete();
        topErrorChannel.Writer.Complete();
        
        await Task.WhenAll(severityTask, productTask, topErrorTask);
    }
    
    private async Task AggregateSeverity(Channel<ErrorInfo> channel)
    {
        await foreach (var record in channel.Reader.ReadAllAsync())
        {
            _errorBySeverity.AddOrUpdate(record.Severity, 1, (_, old) => old + 1);
        }
    }

    private async Task AggregateProductVersion(Channel<ErrorInfo> channel)
    {
        await foreach (var record in channel.Reader.ReadAllAsync())
        {
            var key = $"{record.Product}_{record.Version}";
            _errorByProductVersion.AddOrUpdate(key, 1, (_, old) => old + 1);
        }
    }

    private async Task AggregateTopErrors(Channel<ErrorInfo> channel)
    {
        await foreach (var record in channel.Reader.ReadAllAsync())
        {
            var key = $"{record.Product}_{record.Severity}_{record.ErrorCode}";
            _topErrorCodes.AddOrUpdate(key, 1, (_, old) => old + 1);
        }
    }
    
    private async Task SaveAnalytics()
    {
        var topErrorCodes = _topErrorCodes
            .OrderByDescending(kv => kv.Value)
            .Take(10)
            .ToList();

        await SaveSeverityResults(AnalyticsFileNames.ErrorBySeverityName, _errorBySeverity);
        await SavProductsWithVersionResults(AnalyticsFileNames.ErrorByProductVersionName, _errorByProductVersion);
        await SaveTopErrorResults(AnalyticsFileNames.TopErrorCodesName, topErrorCodes);
    }
    
    private async Task SaveSeverityResults(
        string fileName,
        ConcurrentDictionary<string, int> data)
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
    
    private async Task SavProductsWithVersionResults(
        string fileName,
        ConcurrentDictionary<string, int> data)
    {
        var path = GetPath(fileName);

        await using var writer = new StreamWriter(path);
        await writer.WriteLineAsync("Products,Version,Count");

        foreach (var kvp in data.OrderByDescending(x => x.Value))
        {
            var parts = kvp.Key.Split('_'); 
            await writer.WriteLineAsync($"{parts[0]},{parts[1]},{kvp.Value}");
        }
        
        _logger.LogInformation($"CSV analytics {fileName} saved at path: {path}.");
    }

    private async Task SaveTopErrorResults(
        string fileName, 
        IEnumerable<KeyValuePair<string,int>> data)
    {
        var path = GetPath(fileName);
        await using var writer = new StreamWriter(path);
        await writer.WriteLineAsync("Product,Severity,ErrorCode,Count");
        
        foreach (var kvp in data)
        {
            var parts = kvp.Key.Split('_'); 
            await writer.WriteLineAsync($"{parts[0]},{parts[1]},{parts[2]},{kvp.Value}");
        }
        
        _logger.LogInformation($"CSV analytics {fileName} saved to path: {path}.");
    }
    
    private string GetPath(string fileName)
    {
        fileName += $"_{DateTime.Now:yyyyMMddHHmmss}.csv";
        
        Directory.CreateDirectory(_dataFoldersOptions.AnalyticsDirDataPath);
        var path = Path.Combine(_dataFoldersOptions.AnalyticsDirDataPath, fileName);
        return path;
    }
}
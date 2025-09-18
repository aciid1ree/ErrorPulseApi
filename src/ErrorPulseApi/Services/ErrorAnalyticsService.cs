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
    private readonly ConcurrentDictionary<string, Dictionary<int,int>> _topErrorCodesByHour;


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
        var topErrorByHourChannel = Channel.CreateUnbounded<ErrorInfo>();

        var severityTask = AggregateSeverity(severityChannel);
        var productTask = AggregateProductVersion(productChannel);
        var topErrorTask = AggregateTopErrors(topErrorChannel);
        var topErrorByHourTask = AggregateTopErrorsByHour(topErrorByHourChannel);
        
        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, config);

        await foreach (var record in csv.GetRecordsAsync<ErrorInfo>())
        {
            await severityChannel.Writer.WriteAsync(record);
            await productChannel.Writer.WriteAsync(record);
            await topErrorChannel.Writer.WriteAsync(record);
            await topErrorByHourChannel.Writer.WriteAsync(record);  
        }
        
        severityChannel.Writer.Complete();
        productChannel.Writer.Complete();
        topErrorChannel.Writer.Complete();
        topErrorByHourChannel.Writer.Complete();    
        
        await Task.WhenAll(severityTask, productTask, topErrorTask, topErrorByHourTask);
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
    
    private async Task AggregateTopErrorsByHour(Channel<ErrorInfo> channel)
    {
        await foreach (var record in channel.Reader.ReadAllAsync())
        {
            var hour = ((DateTime)record.Timestamp).Hour;
            
            var key = $"{record.Product}_{record.Severity}_{record.ErrorCode}";
            var innerDict = _topErrorCodesByHour.GetOrAdd(key, _ => new Dictionary<int, int>());

            lock(innerDict)
            {
                if (innerDict.ContainsKey(hour))
                {
                    innerDict[hour]++;
                }
                else
                {
                    innerDict[hour] = 1; 
                }
            }
        }
    }
    
    private async Task SaveAnalytics()
    {
        await SaveSeverityResults(AnalyticsFileNames.ErrorBySeverityName, _errorBySeverity);
        await SavProductsWithVersionResults(AnalyticsFileNames.ErrorByProductVersionName, _errorByProductVersion);
        await SaveTopErrorResults(AnalyticsFileNames.TopErrorCodesName, _topErrorCodes);
        await SaveTopErrorByHourResults(AnalyticsFileNames.TopErrorByHourCodesName, _topErrorCodesByHour);
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
        ConcurrentDictionary<string,int> data)
    {
        var path = GetPath(fileName);
        
        var topErrorCodes = _topErrorCodes
            .OrderByDescending(kv => kv.Value)
            .Take(10)
            .ToList();

        await using var writer = new StreamWriter(path);
        await writer.WriteLineAsync("Product,Severity,ErrorCode,Count");
        
        foreach (var kvp in topErrorCodes)
        {
            var parts = kvp.Key.Split('_'); 
            await writer.WriteLineAsync($"{parts[0]},{parts[1]},{parts[2]},{kvp.Value}");
        }
        
        _logger.LogInformation($"CSV analytics {fileName} saved to path: {path}.");
    }
    
    private async Task SaveTopErrorByHourResults(
        string fileName, 
        ConcurrentDictionary<string, Dictionary<int, int>> data)
    {
        var path = GetPath(fileName);
        await using var writer = new StreamWriter(path);
        await writer.WriteLineAsync("Period,Product,Severity,ErrorCode,Count");
        
        var result = data
            .ToArray() 
            .Select(kvp =>
            {
                var key = kvp.Key;
                var hourly = kvp.Value;
                var best = hourly.Aggregate((max, cur) => cur.Value > max.Value ? cur : max);
                var periodLabel = $"{best.Key:00}:00 - {(best.Key + 1) % 24:00}:00";

                return (Key: key, Hour: periodLabel, Total: best.Value);

                return (Key: key, Hour: $"{best.Key:00}:00 - {(best.Key + 1):00}:00", Total: best.Value);
            })
            .OrderByDescending(x => x.Total) 
            .ToList(); 
        
        foreach (var kvp in result)
        {
            var parts = kvp.Key.Split('_'); 
            await writer.WriteLineAsync($"{kvp.Hour},{parts[0]},{parts[1]},{parts[2]},{kvp.Total}");
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
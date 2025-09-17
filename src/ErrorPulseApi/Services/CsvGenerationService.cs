using System.Globalization;
using CsvHelper;
using ErrorPulseApi.Configuration;
using ErrorPulseApi.Models;
using Microsoft.Extensions.Options;

namespace ErrorPulseApi.Services;

public interface ICsvGenerationService
{
    Task<bool> CreateCsvFile();
}

public class CsvGenerationService : ICsvGenerationService
{
    private readonly DataFoldersOptions _dataFoldersOptions;
    private readonly ReferenceData _referenceData;
    private readonly ILogger<CsvGenerationService> _logger;
    private readonly IHostEnvironment _hostEnvironment;

    private readonly Random _rnd = new();

    public CsvGenerationService(
        IOptions<DataFoldersOptions> dataFoldersOptions,
        IReferenceDataProvider referenceDataProvider,
        ILogger<CsvGenerationService> logger,
        IHostEnvironment hostEnvironment)
    {
        _dataFoldersOptions = dataFoldersOptions.Value;
        _referenceData = referenceDataProvider.GetReferenceData();
        _logger = logger;
        _hostEnvironment = hostEnvironment;
    }

    public async Task<bool> CreateCsvFile()
    {
        var dirPath = Path.Combine(_hostEnvironment.ContentRootPath, _dataFoldersOptions.ErrorDirDataPath);
        Directory.CreateDirectory(dirPath);

        var fileName = $"{Guid.NewGuid():N}.csv";
        var fullPath = Path.Combine(dirPath, fileName);

        await using var writer = new StreamWriter(fullPath);
        await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

        await csv.WriteRecordsAsync(GenerateErrors());
        await writer.FlushAsync();

        _logger.LogInformation("CSV file created successfully at {Path}", fullPath);
        return true;
    } 

    private IEnumerable<ErrorInfo> GenerateErrors(int count = 10_000)
    {
        var start = DateTime.UtcNow.Date;

        for (var i = 0; i < count; i++)
        {
            var product = _referenceData.Products[_rnd.Next(_referenceData.Products.Length)];
            var version = _referenceData.VersionsMap[product][_rnd.Next(_referenceData.VersionsMap[product].Length)];
            var severity = _referenceData.Severity[_rnd.Next(_referenceData.Severity.Length)];
            var errorCode = _referenceData.ErrorCodes[_rnd.Next(_referenceData.ErrorCodes.Length)];
            var timestamp = start.AddSeconds(_rnd.Next(0, 24 * 60 * 60));

            yield return new ErrorInfo(timestamp, severity, product, version, errorCode);
        }
    }
}
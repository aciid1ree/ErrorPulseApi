using System.Globalization;
using System.Text.Json;
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
    private readonly Random _rnd = new();

    public CsvGenerationService(
        IOptions<DataFoldersOptions> dataFoldersOptions,
        IReferenceDataProvider referenceDataProvider)
    {
        _dataFoldersOptions = dataFoldersOptions.Value;
        _referenceData = referenceDataProvider.GetReferenceData(); 
    }

    public async Task<bool> CreateCsvFile()
    {
        try
        {
            Directory.CreateDirectory(_dataFoldersOptions.CsvDataPath);

            var fileName = $"{Guid.NewGuid():N}.csv";
            var fullPath = Path.Combine(_dataFoldersOptions.CsvDataPath, fileName);

            await using var writer = new StreamWriter(fullPath);
            await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

            await csv.WriteRecordsAsync(GenerateErrors());
            await writer.FlushAsync();

            return true;
        }
        catch
        {
            return false;
        }
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
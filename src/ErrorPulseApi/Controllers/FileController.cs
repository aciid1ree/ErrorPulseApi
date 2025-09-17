using ErrorPulseApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace ErrorPulseApi.Controllers;

[ApiController]
[Route("api/files")]
public class FileController : ControllerBase
{
    private readonly ICsvGenerationService _csvGenerationService;
    private readonly IErrorAnalyticsService _errorAnalyticsService;

    public FileController(
        ICsvGenerationService csvGenerationService,
        IErrorAnalyticsService errorAnalyticsService)
    {
        _csvGenerationService = csvGenerationService;
        _errorAnalyticsService = errorAnalyticsService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateFile()
    {
        try
        {
            var success = await _csvGenerationService.CreateCsvFile();
            if (!success)
                return BadRequest(new { success = false, message = "CSV file was not created." });

            return Ok(new { success = true, message = "CSV file created successfully." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }
    
    [HttpPost("analytics")]
    public async Task<IActionResult> CreateAnalyticsFiles()
    {
        try
        {
            var success = await _errorAnalyticsService.CreateAnalyticsFiles();
            if (!success)
                return BadRequest(new { success = false, message = "Analytics CSV files were not created." });

            return Ok(new { success = true, message = "Analytics CSV files created successfully." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }
}
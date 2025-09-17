using ErrorPulseApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace ErrorPulseApi.Controllers;

[ApiController]
[Route("api/files")]
public class FileController : ControllerBase
{
    private readonly ICsvGenerationService _csvGenerationService;

    public FileController(ICsvGenerationService csvGenerationService)
    {
        _csvGenerationService = csvGenerationService;
    }

    [HttpPost]
    public async Task<ActionResult> CreateFile()
    {
        var fileIsCreated = await _csvGenerationService.CreateCsvFile();
        
        if (fileIsCreated)
            return Ok();
        return BadRequest();
    }
}
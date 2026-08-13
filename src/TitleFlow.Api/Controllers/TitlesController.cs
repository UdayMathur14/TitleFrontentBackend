using Microsoft.AspNetCore.Mvc;
using TitleFlow.Api.Application.Abstractions;
using TitleFlow.Api.Contracts.Titles;

namespace TitleFlow.Api.Controllers;

[ApiController]
[Route("api/titles")]
public sealed class TitlesController(ITitleService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<TitleResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<TitleResponse>>> Search([FromQuery] TitleFilter filter, CancellationToken ct) => Ok(await service.SearchAsync(filter, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TitleResponse>> Get(int id, CancellationToken ct) => await service.GetAsync(id, ct) is { } title ? Ok(title) : NotFound();

    [HttpPost]
    public async Task<ActionResult<TitleResponse>> Create(CreateTitleRequest request, CancellationToken ct)
    {
        var result = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<TitleResponse>> Update(int id, UpdateTitleRequest request, CancellationToken ct) => await service.UpdateAsync(id, request, ct) is { } title ? Ok(title) : NotFound();

    [HttpDelete]
    public async Task<IActionResult> Delete(DeleteTitlesRequest request, CancellationToken ct) => Ok(new { deletedCount = await service.DeleteAsync(request.Ids, ct) });

    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardResponse>> Dashboard(CancellationToken ct) => Ok(await service.GetDashboardAsync(ct));

    [HttpGet("dropdowns")]
    public async Task<ActionResult<DropdownData>> Dropdowns(CancellationToken ct) => Ok(await service.GetDropdownsAsync(ct));

    [HttpPost("import/preview")]
    [RequestSizeLimit(200 * 1024 * 1024)]
    public async Task<ActionResult<ImportPreview>> Preview(IFormFile file, CancellationToken ct) => Ok(await service.PreviewImportAsync(file, ct));

    [HttpPost("import/commit")]
    public async Task<IActionResult> Commit(CommitImportRequest request, CancellationToken ct) => Ok(new { savedCount = await service.CommitImportAsync(request.ImportToken, ct) });

    [HttpGet("template")]
    public async Task<IActionResult> Template(CancellationToken ct) => File(await service.CreateTemplateAsync(ct), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "UploadTitles.xlsx");

    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] TitleFilter filter, CancellationToken ct) => File(await service.ExportAsync(filter, ct), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"TitleRecords-{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx");
}

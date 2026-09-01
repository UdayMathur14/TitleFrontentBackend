using Microsoft.AspNetCore.Mvc;
using TitleFlow.Api.Application.Abstractions;
using TitleFlow.Api.Contracts.PublicationTitles;

namespace TitleFlow.Api.Controllers;

[ApiController]
[Route("api/publication-titles")]
public sealed class PublicationTitlesController(IPublicationTitleService service) : ControllerBase
{
    private const string ExcelContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    [HttpGet]
    public async Task<ActionResult<PublicationPagedResult<PublicationTitleResponse>>> Search(
        [FromQuery] PublicationTitleFilter filter, CancellationToken ct) =>
        Ok(await service.SearchAsync(filter, false, ct));

    [HttpGet("modified")]
    public async Task<ActionResult<PublicationPagedResult<PublicationTitleResponse>>> Modified(
        [FromQuery] PublicationTitleFilter filter, CancellationToken ct) =>
        Ok(await service.SearchAsync(filter, true, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PublicationTitleResponse>> Get(int id, CancellationToken ct) =>
        await service.GetAsync(id, ct) is { } title ? Ok(title) : NotFound();

    [HttpDelete]
    public async Task<IActionResult> Delete(DeletePublicationTitlesRequest request, CancellationToken ct) =>
        Ok(new { deletedCount = await service.DeleteAsync(request.Ids, ct) });

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteOne(int id, CancellationToken ct) =>
        Ok(new { deletedCount = await service.DeleteAsync([id], ct) });

    [HttpGet("dashboard")]
    public async Task<ActionResult<PublicationDashboardResponse>> Dashboard(CancellationToken ct) =>
        Ok(await service.GetDashboardAsync(ct));

    [HttpGet("overview")]
    public async Task<ActionResult<PublicationOverviewResponse>> Overview(CancellationToken ct) =>
        Ok(await service.GetOverviewAsync(ct));

    [HttpGet("dropdowns")]
    public async Task<ActionResult<PublicationDropdownData>> Dropdowns(CancellationToken ct) =>
        Ok(await service.GetDropdownsAsync(ct));

    [HttpPost("import/preview")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 50 * 1024 * 1024)]
    public async Task<ActionResult<PublicationImportPreview>> Preview(
        IFormFile file, CancellationToken ct) =>
        Ok(await service.PreviewImportAsync(file, ct));

    [HttpPost("import/commit")]
    public async Task<IActionResult> Commit(CommitPublicationImportRequest request, CancellationToken ct) =>
        Ok(new { savedCount = await service.CommitImportAsync(request, ct) });

    [HttpPost("modified/import/preview")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 50 * 1024 * 1024)]
    public async Task<ActionResult<ModifiedPublicationImportPreview>> PreviewModified(
        IFormFile file, CancellationToken ct) =>
        Ok(await service.PreviewModifiedImportAsync(file, ct));

    [HttpPost("modified/import/commit")]
    public async Task<IActionResult> CommitModified(
        CommitModifiedPublicationImportRequest request, CancellationToken ct) =>
        Ok(new { updatedCount = await service.CommitModifiedImportAsync(request, ct) });

    [HttpGet("template")]
    public async Task<IActionResult> Template(CancellationToken ct) =>
        File(await service.CreateTemplateAsync(ct), ExcelContentType, "UploadPublisherTitle.xlsx");

    [HttpGet("modified/template")]
    public async Task<IActionResult> ModifiedTemplate(CancellationToken ct) =>
        File(await service.CreateModifiedTemplateAsync(ct), ExcelContentType,
            "UploadModifiedPublisherTitle.xlsx");

    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery] PublicationTitleFilter filter, CancellationToken ct) =>
        File(await service.ExportAsync(filter, false, ct), ExcelContentType,
            $"PublicationTitles-{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx");

    [HttpGet("modified/export")]
    public async Task<IActionResult> ExportModified(
        [FromQuery] PublicationTitleFilter filter, CancellationToken ct) =>
        File(await service.ExportAsync(filter, true, ct), ExcelContentType,
            $"ModifiedPublicationTitles-{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx");
}

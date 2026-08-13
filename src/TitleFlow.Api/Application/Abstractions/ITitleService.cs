using TitleFlow.Api.Contracts.Titles;

namespace TitleFlow.Api.Application.Abstractions;

public interface ITitleService
{
    Task<PagedResult<TitleResponse>> SearchAsync(TitleFilter filter, CancellationToken ct);
    Task<TitleResponse?> GetAsync(int id, CancellationToken ct);
    Task<TitleResponse> CreateAsync(CreateTitleRequest request, CancellationToken ct);
    Task<TitleResponse?> UpdateAsync(int id, UpdateTitleRequest request, CancellationToken ct);
    Task<int> DeleteAsync(IEnumerable<int> ids, CancellationToken ct);
    Task<DropdownData> GetDropdownsAsync(CancellationToken ct);
    Task<DashboardResponse> GetDashboardAsync(CancellationToken ct);
    Task<ImportPreview> PreviewImportAsync(IFormFile file, CancellationToken ct);
    Task<int> CommitImportAsync(string token, CancellationToken ct);
    Task<byte[]> CreateTemplateAsync(CancellationToken ct);
    Task<byte[]> ExportAsync(TitleFilter filter, CancellationToken ct);
}

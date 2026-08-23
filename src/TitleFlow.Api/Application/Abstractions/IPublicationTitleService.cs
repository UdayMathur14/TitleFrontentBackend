using TitleFlow.Api.Contracts.PublicationTitles;

namespace TitleFlow.Api.Application.Abstractions;

public interface IPublicationTitleService
{
    Task<PublicationPagedResult<PublicationTitleResponse>> SearchAsync(PublicationTitleFilter filter,
        bool modifiedOnly, CancellationToken ct);
    Task<PublicationTitleResponse?> GetAsync(int id, CancellationToken ct);
    Task<int> DeleteAsync(IEnumerable<int> ids, CancellationToken ct);
    Task<PublicationDropdownData> GetDropdownsAsync(CancellationToken ct);
    Task<PublicationDashboardResponse> GetDashboardAsync(CancellationToken ct);
    Task<PublicationImportPreview> PreviewImportAsync(IFormFile file, CancellationToken ct);
    Task<int> CommitImportAsync(CommitPublicationImportRequest request, CancellationToken ct);
    Task<ModifiedPublicationImportPreview> PreviewModifiedImportAsync(IFormFile file, CancellationToken ct);
    Task<int> CommitModifiedImportAsync(CommitModifiedPublicationImportRequest request, CancellationToken ct);
    Task<byte[]> CreateTemplateAsync(CancellationToken ct);
    Task<byte[]> CreateModifiedTemplateAsync(CancellationToken ct);
    Task<byte[]> ExportAsync(PublicationTitleFilter filter, bool modifiedOnly, CancellationToken ct);
}

using TitleFlow.Api.Contracts.PublicationTitles;
using TitleFlow.Api.Domain.Entities;

namespace TitleFlow.Api.Application.Abstractions;

public interface IPublicationTitleRepository
{
    Task<(IReadOnlyList<PublicationTitleRecord> Items, int Total)> SearchAsync(
        PublicationTitleFilter filter, bool modifiedOnly, CancellationToken ct);
    Task<PublicationTitleRecord?> GetAsync(int id, bool tracking, CancellationToken ct);
    Task<IReadOnlyList<PublicationTitleRecord>> GetTrackedAsync(IReadOnlyCollection<int> ids, CancellationToken ct);
    Task<IReadOnlyList<ExistingPublicationTitle>> GetExistingAsync(CancellationToken ct);
    Task<IReadOnlyList<PublicationTitleRecord>> GetForExportAsync(PublicationTitleFilter filter,
        bool modifiedOnly, CancellationToken ct);
    Task<IReadOnlyList<PublicationTitleRecord>> GetRecentAsync(int count, CancellationToken ct);
    Task<(int Total, int Clean, int Modified, int ThisMonth)> GetDashboardCountsAsync(
        DateOnly monthStart, CancellationToken ct);
    Task<PublicationDropdownData> GetDropdownsAsync(CancellationToken ct);
    Task AddRangeAsync(IEnumerable<PublicationTitleRecord> records, CancellationToken ct);
    Task<int> DeleteAsync(IReadOnlyCollection<int> ids, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

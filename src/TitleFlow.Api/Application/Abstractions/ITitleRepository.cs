using TitleFlow.Api.Contracts.Titles;
using TitleFlow.Api.Domain.Entities;

namespace TitleFlow.Api.Application.Abstractions;

public interface ITitleRepository
{
    Task<(IReadOnlyList<TitleRecord> Items, int Total)> SearchAsync(TitleFilter filter, CancellationToken ct);
    Task<TitleRecord?> GetAsync(int id, CancellationToken ct);
    Task<int> DeleteAsync(IReadOnlyCollection<int> ids, CancellationToken ct);
    Task<IReadOnlyList<TitleRecord>> GetForExportAsync(TitleFilter filter, CancellationToken ct);
    Task<IReadOnlyList<TitleRecord>> GetRecentAsync(int count, CancellationToken ct);
    Task<DashboardCounts> GetDashboardCountsAsync(DateOnly monthStart, CancellationToken ct);
    Task<TitleRecord?> FindByReferenceTitleAsync(string referenceTitle, int? excludingId, CancellationToken ct);
    Task<IReadOnlyList<ExistingTitle>> GetExistingTitlesAsync(CancellationToken ct);
    Task<DropdownData> GetDropdownsAsync(string? query, int limit, CancellationToken ct);
    Task AddAsync(TitleRecord title, CancellationToken ct);
    Task AddRangeAsync(IEnumerable<TitleRecord> titles, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

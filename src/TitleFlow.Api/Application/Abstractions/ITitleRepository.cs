using TitleFlow.Api.Contracts.Titles;
using TitleFlow.Api.Domain.Entities;

namespace TitleFlow.Api.Application.Abstractions;

public interface ITitleRepository
{
    Task<(IReadOnlyList<TitleRecord> Items, int Total)> SearchAsync(TitleFilter filter, CancellationToken ct);
    Task<TitleRecord?> GetAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<TitleRecord>> GetAllAsync(CancellationToken ct);
    Task<IReadOnlyList<TitleRecord>> GetRecentAsync(int count, CancellationToken ct);
    Task<int> CountAsync(string? status, DateOnly? from, CancellationToken ct);
    Task<TitleRecord?> FindByReferenceTitleAsync(string referenceTitle, CancellationToken ct);
    Task<bool> InvoiceCombinationExistsAsync(string invoiceNumber, string codeReference, string titleYear, int? excludingId, CancellationToken ct);
    Task<DropdownData> GetDropdownsAsync(CancellationToken ct);
    Task AddAsync(TitleRecord title, CancellationToken ct);
    Task AddRangeAsync(IEnumerable<TitleRecord> titles, CancellationToken ct);
    void RemoveRange(IEnumerable<TitleRecord> titles);
    Task SaveChangesAsync(CancellationToken ct);
}

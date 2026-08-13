using Microsoft.EntityFrameworkCore;
using TitleFlow.Api.Application.Abstractions;
using TitleFlow.Api.Contracts.Titles;
using TitleFlow.Api.Domain.Entities;
using TitleFlow.Api.Infrastructure.Persistence;

namespace TitleFlow.Api.Infrastructure.Repositories;

public sealed class TitleRepository(AppDbContext db) : ITitleRepository
{
    public async Task<(IReadOnlyList<TitleRecord> Items, int Total)> SearchAsync(TitleFilter filter, CancellationToken ct)
    {
        var query = db.Titles.AsNoTracking();
        if (filter.Id.HasValue) query = query.Where(x => x.Id == filter.Id);
        if (!string.IsNullOrWhiteSpace(filter.CodeReference)) query = query.Where(x => x.CodeReference != null && x.CodeReference.Contains(filter.CodeReference));
        if (!string.IsNullOrWhiteSpace(filter.InvoiceNumber)) query = query.Where(x => x.InvoiceNumber != null && x.InvoiceNumber.Contains(filter.InvoiceNumber));
        if (!string.IsNullOrWhiteSpace(filter.Title)) query = query.Where(x => x.Title != null && x.Title.Contains(filter.Title));
        if (!string.IsNullOrWhiteSpace(filter.TitleYear)) query = query.Where(x => x.TitleYear == filter.TitleYear);
        if (!string.IsNullOrWhiteSpace(filter.Status)) query = query.Where(x => x.Status == filter.Status);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.Id).Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToListAsync(ct);
        return (items, total);
    }

    public Task<TitleRecord?> GetAsync(int id, CancellationToken ct) => db.Titles.FirstOrDefaultAsync(x => x.Id == id, ct);
    public async Task<IReadOnlyList<TitleRecord>> GetAllAsync(CancellationToken ct) => await db.Titles.AsNoTracking().OrderByDescending(x => x.Id).ToListAsync(ct);
    public async Task<IReadOnlyList<TitleRecord>> GetRecentAsync(int count, CancellationToken ct) => await db.Titles.AsNoTracking().OrderByDescending(x => x.Id).Take(count).ToListAsync(ct);
    public Task<int> CountAsync(string? status, DateOnly? from, CancellationToken ct) => db.Titles.CountAsync(x => (status == null || x.Status == status) && (from == null || x.CreatedOn >= from), ct);
    public Task<TitleRecord?> FindByReferenceTitleAsync(string value, CancellationToken ct) => db.Titles.AsNoTracking().FirstOrDefaultAsync(x => x.ReferenceTitle == value, ct);
    public Task<bool> InvoiceCombinationExistsAsync(string invoice, string code, string year, int? excludingId, CancellationToken ct) => db.Titles.AnyAsync(x => x.InvoiceNumber == invoice && x.CodeReference == code && x.TitleYear == year && (!excludingId.HasValue || x.Id != excludingId), ct);
    public async Task<DropdownData> GetDropdownsAsync(CancellationToken ct) => new(
        await db.Titles.Where(x => x.CodeReference != null).Select(x => x.CodeReference!).Distinct().OrderBy(x => x).ToListAsync(ct),
        await db.Titles.Where(x => x.InvoiceNumber != null).Select(x => x.InvoiceNumber!).Distinct().OrderBy(x => x).ToListAsync(ct),
        await db.Titles.Where(x => x.Title != null).Select(x => x.Title!).Distinct().OrderBy(x => x).ToListAsync(ct),
        await db.Titles.Where(x => x.TitleYear != null).Select(x => x.TitleYear!).Distinct().OrderByDescending(x => x).ToListAsync(ct));
    public Task AddAsync(TitleRecord title, CancellationToken ct) => db.Titles.AddAsync(title, ct).AsTask();
    public Task AddRangeAsync(IEnumerable<TitleRecord> titles, CancellationToken ct) => db.Titles.AddRangeAsync(titles, ct);
    public void RemoveRange(IEnumerable<TitleRecord> titles) => db.Titles.RemoveRange(titles);
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

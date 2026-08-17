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
        var query = ApplyNonIdFilters(db.Titles.AsNoTracking(), filter);
        if (filter.Id.HasValue)
        {
            if (filter.Id.Value <= 0) return ([], 0);
            var byDatabaseId = await query.FirstOrDefaultAsync(x => x.Id == filter.Id.Value, ct);
            if (byDatabaseId is not null) return ([byDatabaseId], 1);

            var byDisplayPosition = await query.OrderByDescending(x => x.Id)
                .Skip(filter.Id.Value - 1).Take(1).ToListAsync(ct);
            return (byDisplayPosition, byDisplayPosition.Count);
        }

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.Id).Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToListAsync(ct);
        return (items, total);
    }

    public Task<TitleRecord?> GetAsync(int id, CancellationToken ct) => db.Titles.FirstOrDefaultAsync(x => x.Id == id, ct);
    public Task<int> DeleteAsync(IReadOnlyCollection<int> ids, CancellationToken ct) =>
        db.Titles.Where(x => ids.Contains(x.Id)).ExecuteDeleteAsync(ct);
    public async Task<IReadOnlyList<TitleRecord>> GetForExportAsync(TitleFilter filter, CancellationToken ct) =>
        await ApplyExportFilters(db.Titles.AsNoTracking(), filter).OrderByDescending(x => x.Id).ToListAsync(ct);
    public async Task<IReadOnlyList<TitleRecord>> GetRecentAsync(int count, CancellationToken ct) => await db.Titles.AsNoTracking().OrderByDescending(x => x.Id).Take(count).ToListAsync(ct);
    public async Task<DashboardCounts> GetDashboardCountsAsync(DateOnly monthStart, CancellationToken ct) =>
        await db.Titles.AsNoTracking().GroupBy(_ => 1).Select(group => new DashboardCounts(
            group.Count(),
            group.Count(x => x.Status == "Clean"),
            group.Count(x => x.Status == "Blocked"),
            group.Count(x => x.CreatedOn >= monthStart))).SingleOrDefaultAsync(ct) ?? new(0, 0, 0, 0);
    public Task<TitleRecord?> FindByReferenceTitleAsync(string value, int? excludingId, CancellationToken ct) =>
        db.Titles.AsNoTracking().FirstOrDefaultAsync(x => x.ReferenceTitle == value && (!excludingId.HasValue || x.Id != excludingId), ct);
    public async Task<IReadOnlyList<ExistingTitle>> GetExistingTitlesAsync(CancellationToken ct) =>
        await db.Titles.AsNoTracking().Select(x => new ExistingTitle(
            x.Id, x.ReferenceTitle ?? "", x.InvoiceNumber ?? "", x.CodeReference ?? "", x.TitleYear ?? "")).ToListAsync(ct);
    public async Task<DropdownData> GetDropdownsAsync(string? search, int limit, CancellationToken ct)
    {
        var codes = db.Titles.AsNoTracking().Where(x => x.CodeReference != null).Select(x => x.CodeReference!);
        var invoices = db.Titles.AsNoTracking().Where(x => x.InvoiceNumber != null).Select(x => x.InvoiceNumber!);
        var titles = db.Titles.AsNoTracking().Where(x => x.Title != null).Select(x => x.Title!);
        var years = db.Titles.AsNoTracking().Where(x => x.TitleYear != null).Select(x => x.TitleYear!);
        if (!string.IsNullOrWhiteSpace(search))
        {
            codes = codes.Where(x => x.StartsWith(search));
            invoices = invoices.Where(x => x.StartsWith(search));
            titles = titles.Where(x => x.StartsWith(search));
            years = years.Where(x => x.StartsWith(search));
        }

        return new(
            await codes.Distinct().OrderBy(x => x).Take(limit).ToListAsync(ct),
            await invoices.Distinct().OrderBy(x => x).Take(limit).ToListAsync(ct),
            await titles.Distinct().OrderBy(x => x).Take(limit).ToListAsync(ct),
            await years.Distinct().OrderByDescending(x => x).Take(limit).ToListAsync(ct));
    }
    public Task AddAsync(TitleRecord title, CancellationToken ct) => db.Titles.AddAsync(title, ct).AsTask();
    public Task AddRangeAsync(IEnumerable<TitleRecord> titles, CancellationToken ct) => db.Titles.AddRangeAsync(titles, ct);
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    private static IQueryable<TitleRecord> ApplyNonIdFilters(IQueryable<TitleRecord> query, TitleFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.CodeReference)) query = query.Where(x => x.CodeReference != null && x.CodeReference.Contains(filter.CodeReference));
        if (!string.IsNullOrWhiteSpace(filter.InvoiceNumber)) query = query.Where(x => x.InvoiceNumber != null && x.InvoiceNumber.Contains(filter.InvoiceNumber));
        if (!string.IsNullOrWhiteSpace(filter.Title)) query = query.Where(x => x.Title != null && x.Title.Contains(filter.Title));
        if (!string.IsNullOrWhiteSpace(filter.TitleYear)) query = query.Where(x => x.TitleYear != null && x.TitleYear.Contains(filter.TitleYear));
        if (!string.IsNullOrWhiteSpace(filter.Status)) query = query.Where(x => x.Status == filter.Status);
        return query;
    }

    private static IQueryable<TitleRecord> ApplyExportFilters(IQueryable<TitleRecord> query, TitleFilter filter)
    {
        query = ApplyNonIdFilters(query, filter);
        return filter.Id.HasValue ? query.Where(x => x.Id == filter.Id.Value) : query;
    }
}

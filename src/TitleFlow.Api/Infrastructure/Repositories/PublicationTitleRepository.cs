using Microsoft.EntityFrameworkCore;
using TitleFlow.Api.Application.Abstractions;
using TitleFlow.Api.Contracts.PublicationTitles;
using TitleFlow.Api.Domain.Entities;
using TitleFlow.Api.Infrastructure.Persistence;

namespace TitleFlow.Api.Infrastructure.Repositories;

public sealed class PublicationTitleRepository(AppDbContext db) : IPublicationTitleRepository
{
    public async Task<(IReadOnlyList<PublicationTitleRecord> Items, int Total)> SearchAsync(
        PublicationTitleFilter filter, bool modifiedOnly, CancellationToken ct)
    {
        var query = ApplyFilter(db.PublicationTitles.AsNoTracking(), filter, modifiedOnly);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.Id)
            .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToListAsync(ct);
        return (items, total);
    }

    public Task<PublicationTitleRecord?> GetAsync(int id, bool tracking, CancellationToken ct)
    {
        var query = tracking ? db.PublicationTitles : db.PublicationTitles.AsNoTracking();
        return query.FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<IReadOnlyList<PublicationTitleRecord>> GetTrackedAsync(
        IReadOnlyCollection<int> ids, CancellationToken ct) =>
        await db.PublicationTitles.Where(x => ids.Contains(x.Id)).ToListAsync(ct);

    public async Task<IReadOnlyList<ExistingPublicationTitle>> GetExistingAsync(CancellationToken ct) =>
        await db.PublicationTitles.AsNoTracking().Select(x => new ExistingPublicationTitle(
            x.Id, x.RowNumber, x.InvoiceNumber ?? "", x.PaperId ?? "", x.CodeReference ?? "",
            x.Title ?? "", x.ReferenceTitle ?? "", x.TitleYear ?? "", x.UpdatedTitle,
            x.UpdatedReferenceTitle)).ToListAsync(ct);

    public async Task<IReadOnlyList<PublicationTitleRecord>> GetForExportAsync(
        PublicationTitleFilter filter, bool modifiedOnly, CancellationToken ct) =>
        await ApplyFilter(db.PublicationTitles.AsNoTracking(), filter, modifiedOnly)
            .OrderByDescending(x => x.Id).ToListAsync(ct);

    public async Task<IReadOnlyList<PublicationTitleRecord>> GetRecentAsync(int count, CancellationToken ct) =>
        await db.PublicationTitles.AsNoTracking().OrderByDescending(x => x.Id).Take(count).ToListAsync(ct);

    public async Task<(int Total, int Clean, int Modified, int ThisMonth)> GetDashboardCountsAsync(
        DateOnly monthStart, CancellationToken ct)
    {
        var counts = await db.PublicationTitles.AsNoTracking().GroupBy(_ => 1).Select(group => new
        {
            Total = group.Count(),
            Clean = group.Count(x => x.Status == "Clean"),
            Modified = group.Count(x => x.UpdatedTitle != null),
            ThisMonth = group.Count(x => x.CreatedOn >= monthStart)
        }).SingleOrDefaultAsync(ct);
        return counts is null ? (0, 0, 0, 0) : (counts.Total, counts.Clean, counts.Modified, counts.ThisMonth);
    }

    public async Task<PublicationDropdownData> GetDropdownsAsync(CancellationToken ct) => new(
        await db.PublicationTitles.AsNoTracking().Where(x => x.CodeReference != null)
            .Select(x => x.CodeReference!).Distinct().OrderBy(x => x).ToListAsync(ct),
        await db.PublicationTitles.AsNoTracking().Where(x => x.InvoiceNumber != null)
            .Select(x => x.InvoiceNumber!).Distinct().OrderBy(x => x).ToListAsync(ct),
        await db.PublicationTitles.AsNoTracking().Where(x => x.Title != null)
            .Select(x => x.Title!).Distinct().OrderBy(x => x).ToListAsync(ct),
        await db.PublicationTitles.AsNoTracking().Where(x => x.PaperId != null && x.UpdatedTitle != null)
            .Select(x => x.PaperId!).Distinct().OrderBy(x => x).ToListAsync(ct),
        await db.PublicationTitles.AsNoTracking().Where(x => x.TitleYear != null)
            .Select(x => x.TitleYear!).Distinct().OrderByDescending(x => x).ToListAsync(ct));

    public Task AddRangeAsync(IEnumerable<PublicationTitleRecord> records, CancellationToken ct) =>
        db.PublicationTitles.AddRangeAsync(records, ct);

    public Task<int> DeleteAsync(IReadOnlyCollection<int> ids, CancellationToken ct) =>
        db.PublicationTitles.Where(x => ids.Contains(x.Id)).ExecuteDeleteAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    private static IQueryable<PublicationTitleRecord> ApplyFilter(IQueryable<PublicationTitleRecord> query,
        PublicationTitleFilter filter, bool modifiedOnly)
    {
        if (modifiedOnly) query = query.Where(x => x.UpdatedTitle != null);
        if (filter.Id.HasValue) query = query.Where(x => x.Id == filter.Id.Value);
        if (!string.IsNullOrWhiteSpace(filter.CodeReference)) query = query.Where(x =>
            x.CodeReference != null && x.CodeReference.Contains(filter.CodeReference));
        if (!string.IsNullOrWhiteSpace(filter.LotNumber)) query = query.Where(x =>
            x.InvoiceNumber != null && x.InvoiceNumber.Contains(filter.LotNumber));
        if (!string.IsNullOrWhiteSpace(filter.TitleYear)) query = query.Where(x =>
            x.TitleYear != null && x.TitleYear.Contains(filter.TitleYear));
        if (!string.IsNullOrWhiteSpace(filter.Title)) query = query.Where(x =>
            x.Title != null && x.Title.Contains(filter.Title));
        if (!string.IsNullOrWhiteSpace(filter.PaperId)) query = query.Where(x =>
            x.PaperId != null && x.PaperId.Contains(filter.PaperId));
        if (!string.IsNullOrWhiteSpace(filter.Status)) query = query.Where(x => x.Status == filter.Status);
        return query;
    }
}

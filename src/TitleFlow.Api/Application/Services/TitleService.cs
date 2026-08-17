using ClosedXML.Excel;
using Microsoft.Extensions.Caching.Memory;
using TitleFlow.Api.Application.Abstractions;
using TitleFlow.Api.Contracts.Titles;
using TitleFlow.Api.Domain.Entities;

namespace TitleFlow.Api.Application.Services;

public sealed class TitleService(ITitleRepository repository, IMemoryCache cache, TitleCache titleCache) : ITitleService
{
    private const string ImportCachePrefix = "title-import:";
    private const int MaximumImportRows = 50_000;
    private const int MaximumBulkDeleteIds = 1_000;

    public Task<PagedResult<TitleResponse>> SearchAsync(TitleFilter filter, CancellationToken ct)
    {
        var safe = NormalizeFilter(filter);
        var key = string.Join('|', "search", safe.Page, safe.PageSize, safe.Id,
            safe.CodeReference?.ToLowerInvariant(), safe.InvoiceNumber?.ToLowerInvariant(),
            safe.Title?.ToLowerInvariant(), safe.TitleYear?.ToLowerInvariant(), safe.Status?.ToLowerInvariant());

        return titleCache.GetOrCreateAsync(key, TimeSpan.FromSeconds(20), async () =>
        {
            var (items, total) = await repository.SearchAsync(safe, ct);
            return new PagedResult<TitleResponse>(items.Select(Map).ToList(), safe.Page, safe.PageSize, total,
                total == 0 ? 0 : (int)Math.Ceiling(total / (double)safe.PageSize));
        });
    }

    public Task<TitleResponse?> GetAsync(int id, CancellationToken ct)
    {
        if (id <= 0) return Task.FromResult<TitleResponse?>(null);
        return titleCache.GetOrCreateAsync($"detail:{id}", TimeSpan.FromSeconds(30), async () =>
            (await repository.GetAsync(id, ct)) is { } value ? Map(value) : null);
    }

    public async Task<TitleResponse> CreateAsync(CreateTitleRequest request, CancellationToken ct)
    {
        Validate(request.Title, request.InvoiceNumber, request.CodeReference, request.TitleYear, request.CreatedBy);
        var normalized = TitleRules.Normalize(request.Title);
        var existing = await repository.FindByReferenceTitleAsync(normalized, null, ct);
        var entity = NewEntity(request.CodeReference, request.InvoiceNumber, request.Title, request.TitleYear, request.CreatedBy);
        entity.Status = existing is null ? "Clean" : "Blocked";

        await repository.AddAsync(entity, ct);
        await repository.SaveChangesAsync(ct);
        titleCache.Invalidate();
        return Map(entity);
    }

    public async Task<TitleResponse?> UpdateAsync(int id, UpdateTitleRequest request, CancellationToken ct)
    {
        if (id <= 0) return null;
        Validate(request.Title, request.InvoiceNumber, request.CodeReference, request.TitleYear, request.CreatedBy);
        var entity = await repository.GetAsync(id, ct);
        if (entity is null) return null;

        var normalized = TitleRules.Normalize(request.Title);
        var existing = await repository.FindByReferenceTitleAsync(normalized, id, ct);
        entity.CodeReference = request.CodeReference.Trim();
        entity.InvoiceNumber = request.InvoiceNumber.Trim();
        entity.Title = request.Title.Trim();
        entity.TitleYear = request.TitleYear.Trim();
        entity.ReferenceTitle = normalized;
        entity.Status = existing is null ? "Clean" : "Blocked";
        if (!string.IsNullOrWhiteSpace(request.CreatedBy)) entity.CreatedBy = request.CreatedBy.Trim();

        await repository.SaveChangesAsync(ct);
        titleCache.Invalidate();
        return Map(entity);
    }

    public async Task<int> DeleteAsync(IEnumerable<int> ids, CancellationToken ct)
    {
        var distinctIds = ids.Where(x => x > 0).Distinct().ToArray();
        if (distinctIds.Length == 0) throw new ArgumentException("At least one valid title id is required.");
        if (distinctIds.Length > MaximumBulkDeleteIds) throw new ArgumentException($"A maximum of {MaximumBulkDeleteIds} titles can be deleted at once.");

        var deleted = await repository.DeleteAsync(distinctIds, ct);
        if (deleted > 0) titleCache.Invalidate();
        return deleted;
    }

    public Task<DropdownData> GetDropdownsAsync(string? query, int limit, CancellationToken ct)
    {
        var safeQuery = CleanFilter(query);
        var safeLimit = Math.Clamp(limit, 1, 10_000);
        return titleCache.GetOrCreateAsync($"dropdowns:{safeLimit}:{safeQuery?.ToLowerInvariant()}", TimeSpan.FromMinutes(5),
            () => repository.GetDropdownsAsync(safeQuery, safeLimit, ct));
    }

    public Task<DashboardResponse> GetDashboardAsync(CancellationToken ct) =>
        titleCache.GetOrCreateAsync("dashboard", TimeSpan.FromSeconds(30), async () =>
        {
            var today = DateTime.Today;
            var start = new DateOnly(today.Year, today.Month, 1);
            var counts = await repository.GetDashboardCountsAsync(start, ct);
            var recent = await repository.GetRecentAsync(5, ct);
            return new DashboardResponse(counts.Total, counts.Clean, counts.Blocked, counts.ThisMonth, recent.Select(Map).ToList());
        });

    public async Task<ImportPreview> PreviewImportAsync(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) throw new ArgumentException("The uploaded file is empty.");
        if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Only .xlsx files are supported.");

        await using var stream = file.OpenReadStream();
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(stream);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new ArgumentException("The uploaded Excel file is invalid or corrupted.", exception);
        }

        using (workbook)
        {
            var sheet = workbook.Worksheets.FirstOrDefault() ?? throw new ArgumentException("The workbook does not contain a worksheet.");
            var last = sheet.LastRowUsed()?.RowNumber() ?? 1;
            if (last - 1 > MaximumImportRows) throw new ArgumentException($"A maximum of {MaximumImportRows:N0} rows can be previewed at once.");

            var parsed = new List<ParsedImportRow>();
            var firstTitleInFile = new Dictionary<string, ParsedImportRow>(StringComparer.Ordinal);
            for (var number = 2; number <= last; number++)
            {
                ct.ThrowIfCancellationRequested();
                var invoice = sheet.Cell(number, 1).GetString();
                var code = sheet.Cell(number, 2).GetString();
                var title = sheet.Cell(number, 3).GetString();
                var year = sheet.Cell(number, 4).GetString();
                if (string.IsNullOrWhiteSpace(title)) break;

                var error = string.IsNullOrWhiteSpace(year) ? "Year Missing "
                    : string.IsNullOrWhiteSpace(invoice) ? "Invoice No is Missing "
                    : string.IsNullOrWhiteSpace(code) ? "Code Reference No is Missing "
                    : !TitleRules.IsFinancialYear(year) ? "Invalid Financial Year"
                    : null;
                var normalized = TitleRules.Normalize(title);
                ParsedImportRow? blockedBy = null;
                if (error is null && firstTitleInFile.TryGetValue(normalized, out var first))
                {
                    error = "Duplicate in Excel";
                    blockedBy = first;
                }

                var parsedRow = new ParsedImportRow(number, title, invoice, code, year, normalized, error,
                    blockedBy?.RowNumber, blockedBy?.InvoiceNumber, blockedBy?.CodeReference);
                parsed.Add(parsedRow);
                if (error is null) firstTitleInFile.Add(normalized, parsedRow);
            }

            var existingRows = await repository.GetExistingTitlesAsync(ct);
            var existing = existingRows.Where(x => !string.IsNullOrEmpty(x.ReferenceTitle))
                .GroupBy(x => x.ReferenceTitle, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            var existingCombinations = existingRows.Select(x => (x.InvoiceNumber, x.CodeReference, x.TitleYear)).ToHashSet();
            var rows = parsed.Select(row => ToImportRow(row, existing, existingCombinations)).ToList();
            var cleanRows = rows.Where(x => x.Category == "Clean").ToList();
            var token = Guid.NewGuid().ToString("N");
            cache.Set(ImportCachePrefix + token, cleanRows, TimeSpan.FromMinutes(30));

            return new(file.FileName, rows.Count, cleanRows.Count, rows.Count(x => x.Category == "Blocked"),
                rows.Count(x => x.Category == "Invalid"), rows, token);
        }
    }

    public async Task<int> CommitImportAsync(string token, CancellationToken ct)
    {
        if (!Guid.TryParseExact(token, "N", out _)) throw new ArgumentException("Import token is invalid.");
        if (!cache.TryGetValue<List<ImportRow>>(ImportCachePrefix + token, out var rows) || rows is null)
            throw new KeyNotFoundException("Import preview expired or does not exist.");
        if (rows.Count == 0)
        {
            cache.Remove(ImportCachePrefix + token);
            return 0;
        }

        var existingRows = await repository.GetExistingTitlesAsync(ct);
        var existingTitles = existingRows.Select(x => x.ReferenceTitle).ToHashSet(StringComparer.Ordinal);
        var existingCombinations = existingRows.Select(x => (x.InvoiceNumber, x.CodeReference, x.TitleYear)).ToHashSet();
        var conflicts = rows.Count(x => existingTitles.Contains(TitleRules.Normalize(x.Title)) ||
            existingCombinations.Contains((x.InvoiceNumber, x.CodeReference, x.TitleYear)));
        if (conflicts > 0) throw new TitleConflictException($"{conflicts} row(s) conflict with records added after this preview. Preview the file again before importing.");

        var entities = rows.Select(x => NewEntity(x.CodeReference, x.InvoiceNumber, x.Title, x.TitleYear, "Title import", x.RowNumber)).ToList();
        await repository.AddRangeAsync(entities, ct);
        await repository.SaveChangesAsync(ct);
        cache.Remove(ImportCachePrefix + token);
        titleCache.Invalidate();
        return entities.Count;
    }

    public Task<byte[]> CreateTemplateAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var bytes = cache.GetOrCreate("title-template", entry =>
        {
            entry.Priority = CacheItemPriority.NeverRemove;
            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add("UploadTitles");
            var headers = new[] { "Invoice No (Required)", "Code Ref (Required)", "Title (Required)", "Financial Year (Required)", "Example" };
            for (var i = 0; i < headers.Length; i++) sheet.Cell(1, i + 1).Value = headers[i];
            sheet.Range("A1:E1").Style.Font.Bold = true;
            sheet.Range("A1:E1").Style.Font.FontColor = XLColor.Red;
            sheet.Range("A1:E1").Style.Fill.BackgroundColor = XLColor.LightYellow;
            sheet.Range("A1:E1").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            sheet.Range("A1:E1").Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            sheet.Range("A1:E1").Style.Border.OutsideBorderColor = XLColor.Black;
            sheet.Range("A1:E1").Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            sheet.Range("A1:E1").Style.Border.InsideBorderColor = XLColor.Black;
            sheet.Cell(2, 1).Value = "INV123";
            sheet.Cell(2, 2).Value = "CR456";
            sheet.Cell(2, 3).Value = "Sample Title";
            sheet.Cell(2, 4).Value = "2025-26";
            sheet.Cell(2, 5).Value = "Example row. Please delete and follow this format.";
            sheet.Range("A2:E2").Style.Font.FontColor = XLColor.Gray;
            sheet.Range("A2:E2").Style.Font.Italic = true;
            sheet.Column(1).Width = 20;
            sheet.Column(2).Width = 20;
            sheet.Column(3).Width = 25;
            sheet.Column(4).Width = 23;
            sheet.Column(5).Width = 40;
            using var output = new MemoryStream();
            workbook.SaveAs(output);
            return output.ToArray();
        });
        return Task.FromResult(bytes!);
    }

    public async Task<byte[]> ExportAsync(TitleFilter filter, CancellationToken ct)
    {
        var values = await repository.GetForExportAsync(NormalizeFilter(filter), ct);
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Titles");
        var headers = new[] { "Id", "Row Number", "Code Ref", "Invoice No", "Title", "Created By", "Created On", "Year", "Status" };
        for (var i = 0; i < headers.Length; i++) sheet.Cell(1, i + 1).Value = headers[i];
        sheet.Range(1, 1, 1, headers.Length).Style.Font.Bold = true;
        sheet.SheetView.FreezeRows(1);

        var row = 2;
        foreach (var value in values)
        {
            sheet.Cell(row, 1).Value = value.Id;
            sheet.Cell(row, 2).Value = value.RowNumber;
            sheet.Cell(row, 3).Value = value.CodeReference ?? "";
            sheet.Cell(row, 4).Value = value.InvoiceNumber ?? "";
            sheet.Cell(row, 5).Value = value.Title ?? "";
            sheet.Cell(row, 6).Value = value.CreatedBy ?? "";
            sheet.Cell(row, 7).Value = value.CreatedOn?.ToString("yyyy-MM-dd") ?? "";
            sheet.Cell(row, 8).Value = value.TitleYear ?? "";
            sheet.Cell(row, 9).Value = value.Status ?? "";
            row++;
        }

        if (row > 2) sheet.Range(1, 1, row - 1, headers.Length).CreateTable();
        sheet.Columns().AdjustToContents(1, Math.Min(row - 1, 200));
        using var output = new MemoryStream();
        workbook.SaveAs(output);
        return output.ToArray();
    }

    private static TitleFilter NormalizeFilter(TitleFilter filter) => filter with
    {
        Page = Math.Max(1, filter.Page),
        PageSize = Math.Clamp(filter.PageSize, 1, 200),
        CodeReference = CleanFilter(filter.CodeReference),
        InvoiceNumber = CleanFilter(filter.InvoiceNumber),
        Title = CleanFilter(filter.Title),
        TitleYear = CleanFilter(filter.TitleYear),
        Status = CleanFilter(filter.Status)
    };

    private static string? CleanFilter(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ImportRow ToImportRow(ParsedImportRow row, IReadOnlyDictionary<string, ExistingTitle> existing,
        IReadOnlySet<(string InvoiceNumber, string CodeReference, string TitleYear)> existingCombinations)
    {
        if (row.Error == "Duplicate in Excel")
            return new(row.RowNumber, row.Title, row.InvoiceNumber, row.CodeReference, row.TitleYear, "Blocked", row.Error,
                row.BlockedByRow, row.BlockedByInvoiceNumber, row.BlockedByCodeReference);
        if (row.Error is not null) return new(row.RowNumber, row.Title, row.InvoiceNumber, row.CodeReference, row.TitleYear, "Invalid", row.Error);
        if (existingCombinations.Contains((row.InvoiceNumber, row.CodeReference, row.TitleYear)))
            return new(row.RowNumber, row.Title, row.InvoiceNumber, row.CodeReference, row.TitleYear, "Invalid", "Invoice with codeRef already exists");
        if (existing.TryGetValue(row.ReferenceTitle, out var match))
            return new(row.RowNumber, row.Title, row.InvoiceNumber, row.CodeReference, row.TitleYear, "Blocked", "Blocked",
                match.RowNumber > 0 ? match.RowNumber : null, match.InvoiceNumber, match.CodeReference);
        return new(row.RowNumber, row.Title, row.InvoiceNumber, row.CodeReference, row.TitleYear, "Clean", "Clean");
    }

    private static TitleRecord NewEntity(string code, string invoice, string title, string year, string? createdBy, int rowNumber = 0) => new()
    {
        RowNumber = rowNumber,
        CodeReference = code.Trim(),
        InvoiceNumber = invoice.Trim(),
        Title = title.Trim(),
        TitleYear = year.Trim(),
        ReferenceTitle = TitleRules.Normalize(title),
        Status = "Clean",
        CreatedBy = string.IsNullOrWhiteSpace(createdBy) ? "Title API" : createdBy.Trim(),
        CreatedOn = DateOnly.FromDateTime(DateTime.Today)
    };

    private static void Validate(string? title, string? invoice, string? code, string? year, string? createdBy)
    {
        var error = TitleRules.Validate(title, invoice, code, year);
        if (error is not null) throw new ArgumentException(error);
        if (createdBy?.Trim().Length > 240) throw new ArgumentException("Created by cannot exceed 240 characters.");
    }

    private static TitleResponse Map(TitleRecord value) => new(value.Id, value.RowNumber, value.CodeReference ?? "",
        value.InvoiceNumber ?? "", value.Title ?? "", value.TitleYear ?? "", value.Status ?? "",
        value.ReferenceTitle ?? "", value.CreatedBy ?? "", value.CreatedOn);

    private sealed record ParsedImportRow(int RowNumber, string Title, string InvoiceNumber, string CodeReference,
        string TitleYear, string ReferenceTitle, string? Error, int? BlockedByRow = null,
        string? BlockedByInvoiceNumber = null, string? BlockedByCodeReference = null);
}

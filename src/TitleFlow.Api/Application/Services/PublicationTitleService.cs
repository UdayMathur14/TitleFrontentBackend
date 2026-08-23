using ClosedXML.Excel;
using Microsoft.Extensions.Caching.Memory;
using TitleFlow.Api.Application.Abstractions;
using TitleFlow.Api.Contracts.PublicationTitles;
using TitleFlow.Api.Domain.Entities;

namespace TitleFlow.Api.Application.Services;

public sealed class PublicationTitleService(
    IPublicationTitleRepository repository,
    PublicationTitleCache publicationCache,
    IMemoryCache memoryCache) : IPublicationTitleService
{
    private const int MaximumImportRows = 50_000;
    private const int MaximumBulkDeleteIds = 1_000;
    private const string ImportCachePrefix = "publication-title-import:";
    private const string ModifiedImportCachePrefix = "publication-title-modified-import:";

    public Task<PublicationPagedResult<PublicationTitleResponse>> SearchAsync(
        PublicationTitleFilter filter, bool modifiedOnly, CancellationToken ct)
    {
        var safe = NormalizeFilter(filter);
        var key = string.Join(':', "search", modifiedOnly, safe.Page, safe.PageSize, safe.Id,
            safe.CodeReference?.ToLowerInvariant(), safe.LotNumber?.ToLowerInvariant(),
            safe.Title?.ToLowerInvariant(), safe.TitleYear?.ToLowerInvariant(),
            safe.PaperId?.ToLowerInvariant(), safe.Status?.ToLowerInvariant());

        return publicationCache.GetOrCreateAsync(key, TimeSpan.FromSeconds(20), async () =>
        {
            var (items, total) = await repository.SearchAsync(safe, modifiedOnly, ct);
            return new PublicationPagedResult<PublicationTitleResponse>(items.Select(Map).ToList(),
                safe.Page, safe.PageSize, total,
                total == 0 ? 0 : (int)Math.Ceiling(total / (double)safe.PageSize));
        });
    }

    public Task<PublicationTitleResponse?> GetAsync(int id, CancellationToken ct)
    {
        if (id <= 0) return Task.FromResult<PublicationTitleResponse?>(null);
        return publicationCache.GetOrCreateAsync($"detail:{id}", TimeSpan.FromSeconds(30), async () =>
            (await repository.GetAsync(id, false, ct)) is { } value ? Map(value) : null);
    }

    public async Task<int> DeleteAsync(IEnumerable<int> ids, CancellationToken ct)
    {
        var distinctIds = ids.Where(x => x > 0).Distinct().ToArray();
        if (distinctIds.Length == 0)
            throw new ArgumentException("At least one valid publication title id is required.");
        if (distinctIds.Length > MaximumBulkDeleteIds)
            throw new ArgumentException($"A maximum of {MaximumBulkDeleteIds} publication titles can be deleted at once.");

        var deleted = await repository.DeleteAsync(distinctIds, ct);
        if (deleted > 0) publicationCache.Invalidate();
        return deleted;
    }

    public Task<PublicationDropdownData> GetDropdownsAsync(CancellationToken ct) =>
        publicationCache.GetOrCreateAsync("dropdowns", TimeSpan.FromMinutes(5),
            () => repository.GetDropdownsAsync(ct));

    public Task<PublicationDashboardResponse> GetDashboardAsync(CancellationToken ct) =>
        publicationCache.GetOrCreateAsync("dashboard", TimeSpan.FromSeconds(30), async () =>
        {
            var today = DateTime.Today;
            var counts = await repository.GetDashboardCountsAsync(new DateOnly(today.Year, today.Month, 1), ct);
            var recent = await repository.GetRecentAsync(5, ct);
            return new PublicationDashboardResponse(counts.Total, counts.Clean, counts.Modified,
                counts.ThisMonth, recent.Select(Map).ToList());
        });

    public async Task<PublicationImportPreview> PreviewImportAsync(IFormFile file, CancellationToken ct)
    {
        ValidateWorkbook(file);
        using var workbook = OpenWorkbook(file);
        var sheet = workbook.Worksheets.FirstOrDefault()
            ?? throw new ArgumentException("The workbook does not contain a worksheet.");
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
        if (lastRow - 1 > MaximumImportRows)
            throw new ArgumentException($"A maximum of {MaximumImportRows:N0} rows can be previewed at once.");

        var existing = await GetExistingForPreviewAsync(ct);
        var titleLookup = existing
            .Select(x => new { Clean = EffectiveReferenceTitle(x), Value = x })
            .Where(x => !string.IsNullOrWhiteSpace(x.Clean))
            .GroupBy(x => x.Clean, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First().Value, StringComparer.OrdinalIgnoreCase);
        var comboLookup = existing
            .Where(x => !string.IsNullOrWhiteSpace(x.PaperId) && !string.IsNullOrWhiteSpace(x.LotNumber))
            .GroupBy(x => PublicationTitleRules.ComboKey(x.PaperId, x.LotNumber), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        var titlesInExcel = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var combosInExcel = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rows = new List<PublicationImportRow>();

        for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
        {
            ct.ThrowIfCancellationRequested();
            var lotNumber = sheet.Cell(rowNumber, 1).GetString().Trim();
            var paperId = sheet.Cell(rowNumber, 2).GetString().Trim();
            var codeReference = sheet.Cell(rowNumber, 3).GetString().Trim();
            var title = sheet.Cell(rowNumber, 4).GetString().Trim();
            var titleYear = sheet.Cell(rowNumber, 5).GetString().Trim();
            if (string.IsNullOrWhiteSpace(title)) continue;

            var cleanTitle = PublicationTitleRules.Normalize(title);
            var combo = PublicationTitleRules.ComboKey(paperId, lotNumber);
            PublicationImportRow result;

            if (string.IsNullOrWhiteSpace(titleYear))
                result = Invalid(rowNumber, lotNumber, paperId, codeReference, title, titleYear, "Year Missing");
            else if (!PublicationTitleRules.IsFinancialYear(titleYear))
                result = Invalid(rowNumber, lotNumber, paperId, codeReference, title, titleYear, "Invalid Financial Year");
            else if (string.IsNullOrWhiteSpace(paperId))
                result = Invalid(rowNumber, lotNumber, paperId, codeReference, title, titleYear, "PaperId Missing");
            else if (string.IsNullOrWhiteSpace(lotNumber))
                result = Invalid(rowNumber, lotNumber, paperId, codeReference, title, titleYear, "Invoice Number Missing");
            else if (string.IsNullOrWhiteSpace(codeReference))
                result = Invalid(rowNumber, lotNumber, paperId, codeReference, title, titleYear, "Code Reference Missing");
            else if (titlesInExcel.Contains(cleanTitle))
                result = Invalid(rowNumber, lotNumber, paperId, codeReference, title, titleYear, "Duplicate title in Excel");
            else if (combosInExcel.Contains(combo))
                result = Invalid(rowNumber, lotNumber, paperId, codeReference, title, titleYear,
                    "Duplicate PaperId & Lot Number in Excel");
            else if (comboLookup.TryGetValue(combo, out var comboMatch))
                result = Blocked(rowNumber, lotNumber, paperId, codeReference, title, titleYear,
                    "PaperId and Lot Number combination already exists in DB", comboMatch);
            else if (titleLookup.TryGetValue(cleanTitle, out var titleMatch))
                result = Blocked(rowNumber, lotNumber, paperId, codeReference, title, titleYear,
                    "Duplicate title in DB", titleMatch);
            else
            {
                result = new PublicationImportRow(rowNumber, lotNumber, paperId, codeReference, title,
                    titleYear, "Clean", "Clean");
                titlesInExcel.Add(cleanTitle);
                combosInExcel.Add(combo);
            }

            rows.Add(result);
        }

        var cleanRows = rows.Where(x => x.Category == "Clean").ToList();
        var token = Guid.NewGuid().ToString("N");
        memoryCache.Set(ImportCachePrefix + token, cleanRows, TimeSpan.FromMinutes(30));
        return new PublicationImportPreview(file.FileName, rows.Count, cleanRows.Count,
            rows.Count(x => x.Category == "Blocked"), rows.Count(x => x.Category == "Invalid"), rows, token);
    }

    public async Task<int> CommitImportAsync(CommitPublicationImportRequest request, CancellationToken ct)
    {
        ValidateToken(request.ImportToken);
        if (!memoryCache.TryGetValue<List<PublicationImportRow>>(ImportCachePrefix + request.ImportToken,
                out var rows) || rows is null)
            throw new KeyNotFoundException("Publication import preview expired or does not exist.");
        if (rows.Count == 0)
        {
            memoryCache.Remove(ImportCachePrefix + request.ImportToken);
            return 0;
        }

        var existing = await repository.GetExistingAsync(ct);
        var existingTitles = existing.Select(EffectiveReferenceTitle)
            .Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingCombos = existing
            .Where(x => !string.IsNullOrWhiteSpace(x.PaperId) && !string.IsNullOrWhiteSpace(x.LotNumber))
            .Select(x => PublicationTitleRules.ComboKey(x.PaperId, x.LotNumber))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var conflicts = rows.Count(x => existingTitles.Contains(PublicationTitleRules.Normalize(x.Title)) ||
            existingCombos.Contains(PublicationTitleRules.ComboKey(x.PaperId, x.LotNumber)));
        if (conflicts > 0)
            throw new PublicationTitleConflictException(
                $"{conflicts} row(s) conflict with publication records added after this preview. Preview the file again before importing.");

        var createdBy = CleanActor(request.CreatedBy, "Publication title import");
        var records = rows.Select(x => new PublicationTitleRecord
        {
            RowNumber = x.RowNumber,
            InvoiceNumber = x.LotNumber,
            PaperId = x.PaperId,
            CodeReference = x.CodeReference,
            Title = x.Title,
            TitleYear = x.TitleYear,
            ReferenceTitle = PublicationTitleRules.Normalize(x.Title),
            Status = "Clean",
            CreatedBy = createdBy,
            CreatedOn = DateOnly.FromDateTime(DateTime.Today)
        }).ToList();

        await repository.AddRangeAsync(records, ct);
        await repository.SaveChangesAsync(ct);
        memoryCache.Remove(ImportCachePrefix + request.ImportToken);
        publicationCache.Invalidate();
        return records.Count;
    }

    public async Task<ModifiedPublicationImportPreview> PreviewModifiedImportAsync(
        IFormFile file, CancellationToken ct)
    {
        ValidateWorkbook(file);
        using var workbook = OpenWorkbook(file);
        var sheet = workbook.Worksheets.FirstOrDefault()
            ?? throw new ArgumentException("The workbook does not contain a worksheet.");
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
        if (lastRow - 1 > MaximumImportRows)
            throw new ArgumentException($"A maximum of {MaximumImportRows:N0} rows can be previewed at once.");

        var existing = await GetExistingForPreviewAsync(ct);
        var recordLookup = existing
            .Where(x => !string.IsNullOrWhiteSpace(x.PaperId) && !string.IsNullOrWhiteSpace(x.LotNumber))
            .GroupBy(x => PublicationTitleRules.ComboKey(x.PaperId, x.LotNumber), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        var titleSet = existing.Select(EffectiveReferenceTitle)
            .Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var uploadedCombos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rows = new List<ModifiedPublicationImportRow>();

        for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
        {
            ct.ThrowIfCancellationRequested();
            var lotNumber = sheet.Cell(rowNumber, 1).GetString().Trim();
            var paperId = sheet.Cell(rowNumber, 2).GetString().Trim();
            var updatedTitle = sheet.Cell(rowNumber, 3).GetString().Trim();
            if (string.IsNullOrWhiteSpace(lotNumber) && string.IsNullOrWhiteSpace(paperId) &&
                string.IsNullOrWhiteSpace(updatedTitle)) continue;

            ModifiedPublicationImportRow result;
            if (string.IsNullOrWhiteSpace(lotNumber))
                result = ModifiedInvalid(rowNumber, lotNumber, paperId, updatedTitle, "Invoice Number Missing");
            else if (string.IsNullOrWhiteSpace(paperId))
                result = ModifiedInvalid(rowNumber, lotNumber, paperId, updatedTitle, "PaperId Missing");
            else if (string.IsNullOrWhiteSpace(updatedTitle))
                result = ModifiedInvalid(rowNumber, lotNumber, paperId, updatedTitle, "Updated Title Missing");
            else
            {
                var combo = PublicationTitleRules.ComboKey(paperId, lotNumber);
                if (!uploadedCombos.Add(combo))
                    result = ModifiedInvalid(rowNumber, lotNumber, paperId, updatedTitle,
                        "Duplicate PaperId & Invoice Number in Excel");
                else if (!recordLookup.TryGetValue(combo, out var record))
                    result = ModifiedInvalid(rowNumber, lotNumber, paperId, updatedTitle,
                        "PaperId and Invoice Number combination not found");
                else
                {
                    var oldCleanTitle = EffectiveReferenceTitle(record);
                    if (!string.IsNullOrWhiteSpace(oldCleanTitle)) titleSet.Remove(oldCleanTitle);
                    var cleanTitle = PublicationTitleRules.Normalize(updatedTitle);
                    if (titleSet.Contains(cleanTitle))
                    {
                        result = ModifiedInvalid(rowNumber, lotNumber, paperId, updatedTitle, "Duplicate Title");
                        if (!string.IsNullOrWhiteSpace(oldCleanTitle)) titleSet.Add(oldCleanTitle);
                    }
                    else
                    {
                        titleSet.Add(cleanTitle);
                        result = new ModifiedPublicationImportRow(rowNumber, lotNumber, paperId, updatedTitle,
                            "PASS", "PASS", record.Id, EffectiveDisplayTitle(record));
                    }
                }
            }

            rows.Add(result);
        }

        var passRows = rows.Where(x => x.Category == "PASS").ToList();
        var token = Guid.NewGuid().ToString("N");
        memoryCache.Set(ModifiedImportCachePrefix + token, passRows, TimeSpan.FromMinutes(30));
        return new ModifiedPublicationImportPreview(file.FileName, rows.Count, passRows.Count,
            rows.Count(x => x.Category == "Invalid"), rows, token);
    }

    public async Task<int> CommitModifiedImportAsync(
        CommitModifiedPublicationImportRequest request, CancellationToken ct)
    {
        ValidateToken(request.ImportToken);
        if (!memoryCache.TryGetValue<List<ModifiedPublicationImportRow>>(
                ModifiedImportCachePrefix + request.ImportToken, out var rows) || rows is null)
            throw new KeyNotFoundException("Modified publication import preview expired or does not exist.");
        if (rows.Count == 0)
        {
            memoryCache.Remove(ModifiedImportCachePrefix + request.ImportToken);
            return 0;
        }

        var existing = await repository.GetExistingAsync(ct);
        var byId = existing.ToDictionary(x => x.Id);
        var titleSet = existing.Select(EffectiveReferenceTitle)
            .Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var conflicts = 0;
        foreach (var row in rows)
        {
            if (!row.TargetId.HasValue || !byId.TryGetValue(row.TargetId.Value, out var record) ||
                !string.Equals(PublicationTitleRules.ComboKey(record.PaperId, record.LotNumber),
                    PublicationTitleRules.ComboKey(row.PaperId, row.LotNumber),
                    StringComparison.OrdinalIgnoreCase))
            {
                conflicts++;
                continue;
            }

            var oldTitle = EffectiveReferenceTitle(record);
            if (!string.IsNullOrWhiteSpace(oldTitle)) titleSet.Remove(oldTitle);
            var newTitle = PublicationTitleRules.Normalize(row.UpdatedTitle);
            if (titleSet.Contains(newTitle))
            {
                conflicts++;
                if (!string.IsNullOrWhiteSpace(oldTitle)) titleSet.Add(oldTitle);
            }
            else
            {
                titleSet.Add(newTitle);
            }
        }

        if (conflicts > 0)
            throw new PublicationTitleConflictException(
                $"{conflicts} row(s) conflict with publication records changed after this preview. Preview the file again before updating.");

        var ids = rows.Select(x => x.TargetId!.Value).Distinct().ToArray();
        var tracked = await repository.GetTrackedAsync(ids, ct);
        if (tracked.Count != ids.Length)
            throw new PublicationTitleConflictException(
                "One or more publication records no longer exist. Preview the file again before updating.");
        var rowById = rows.ToDictionary(x => x.TargetId!.Value);
        var updatedBy = CleanActor(request.UpdatedBy, "Publication title import");
        foreach (var record in tracked)
        {
            var row = rowById[record.Id];
            record.UpdatedTitle = row.UpdatedTitle;
            record.UpdatedReferenceTitle = PublicationTitleRules.Normalize(row.UpdatedTitle);
            record.UpdatedTitleBy = updatedBy;
        }

        await repository.SaveChangesAsync(ct);
        memoryCache.Remove(ModifiedImportCachePrefix + request.ImportToken);
        publicationCache.Invalidate();
        return tracked.Count;
    }

    public Task<byte[]> CreateTemplateAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var bytes = memoryCache.GetOrCreate("publication-title-template", entry =>
        {
            entry.Priority = CacheItemPriority.NeverRemove;
            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add("UploadTitles");
            var headers = new[] { "Lot Number (Required)", "Paper Id (Required)",
                "Code Ref (Required)", "Title (Required)", "Financial Year (Required)", "Example" };
            WriteTemplateHeader(sheet, headers);
            sheet.Cell(2, 1).Value = "INV123";
            sheet.Cell(2, 2).Value = "P1234";
            sheet.Cell(2, 3).Value = "CR456";
            sheet.Cell(2, 4).Value = "Sample Title";
            sheet.Cell(2, 5).Value = "2025-26";
            sheet.Cell(2, 6).Value = "Example row. Please delete and follow this format.";
            StyleExampleRow(sheet, headers.Length);
            SetWidths(sheet, 20, 20, 20, 25, 23, 40);
            return Save(workbook);
        });
        return Task.FromResult(bytes!);
    }

    public Task<byte[]> CreateModifiedTemplateAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var bytes = memoryCache.GetOrCreate("publication-title-modified-template", entry =>
        {
            entry.Priority = CacheItemPriority.NeverRemove;
            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add("ModifiedTitles");
            var headers = new[] { "Lot Number (Required)", "Paper Id (Required)", "Updated Title (Required)" };
            WriteTemplateHeader(sheet, headers);
            sheet.Cell(2, 1).Value = "INV123";
            sheet.Cell(2, 2).Value = "P1234";
            sheet.Cell(2, 3).Value = "Updated Sample Title";
            StyleExampleRow(sheet, headers.Length);
            SetWidths(sheet, 22, 22, 35);
            return Save(workbook);
        });
        return Task.FromResult(bytes!);
    }

    public async Task<byte[]> ExportAsync(
        PublicationTitleFilter filter, bool modifiedOnly, CancellationToken ct)
    {
        var values = await repository.GetForExportAsync(NormalizeFilter(filter), modifiedOnly, ct);
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(modifiedOnly ? "ModifiedPublicationTitles" : "PublicationTitles");
        var headers = new[] { "Id", "Code Ref", "Lot No", "Paper Id", "Title", "Created By",
            "Year", "Status" };
        for (var i = 0; i < headers.Length; i++) sheet.Cell(1, i + 1).Value = headers[i];
        sheet.Range(1, 1, 1, headers.Length).Style.Font.Bold = true;
        sheet.SheetView.FreezeRows(1);

        var row = 2;
        foreach (var value in values)
        {
            sheet.Cell(row, 1).Value = value.Id;
            sheet.Cell(row, 2).Value = value.CodeReference ?? "";
            sheet.Cell(row, 3).Value = value.InvoiceNumber ?? "";
            sheet.Cell(row, 4).Value = value.PaperId ?? "";
            sheet.Cell(row, 5).Value = value.Title ?? "";
            sheet.Cell(row, 6).Value = value.CreatedBy ?? "";
            sheet.Cell(row, 7).Value = value.TitleYear ?? "";
            sheet.Cell(row, 8).Value = value.Status ?? "";
            row++;
        }

        if (row > 2) sheet.Range(1, 1, row - 1, headers.Length).CreateTable();
        sheet.Columns().AdjustToContents(1, Math.Min(row - 1, 200));
        return Save(workbook);
    }

    private static PublicationTitleFilter NormalizeFilter(PublicationTitleFilter filter) => filter with
    {
        Page = Math.Max(1, filter.Page),
        PageSize = Math.Clamp(filter.PageSize, 1, 200),
        CodeReference = CleanFilter(filter.CodeReference),
        LotNumber = CleanFilter(filter.LotNumber),
        Title = CleanFilter(filter.Title),
        TitleYear = CleanFilter(filter.TitleYear),
        PaperId = CleanFilter(filter.PaperId),
        Status = CleanFilter(filter.Status)
    };

    private static string? CleanFilter(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private Task<IReadOnlyList<ExistingPublicationTitle>> GetExistingForPreviewAsync(CancellationToken ct) =>
        publicationCache.GetOrCreateAsync("existing-preview-snapshot", TimeSpan.FromSeconds(30),
            () => repository.GetExistingAsync(ct));

    private static string CleanActor(string? value, string fallback)
    {
        var actor = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        if (actor.Length > 240) throw new ArgumentException("User name cannot exceed 240 characters.");
        return actor;
    }

    private static string EffectiveReferenceTitle(ExistingPublicationTitle value) =>
        PublicationTitleRules.Normalize(!string.IsNullOrWhiteSpace(value.UpdatedReferenceTitle)
            ? value.UpdatedReferenceTitle
            : value.ReferenceTitle);

    private static string EffectiveDisplayTitle(ExistingPublicationTitle value) =>
        !string.IsNullOrWhiteSpace(value.UpdatedTitle) ? value.UpdatedTitle : value.Title;

    private static PublicationImportRow Invalid(int row, string lot, string paper, string code,
        string title, string year, string message) =>
        new(row, lot, paper, code, title, year, "Invalid", message);

    private static PublicationImportRow Blocked(int row, string lot, string paper, string code,
        string title, string year, string message, ExistingPublicationTitle match) =>
        new(row, lot, paper, code, title, year, "Blocked", message, match.Id,
            match.RowNumber > 0 ? match.RowNumber : null, match.PaperId, match.LotNumber,
            match.CodeReference, match.Title);

    private static ModifiedPublicationImportRow ModifiedInvalid(int row, string lot, string paper,
        string title, string message) => new(row, lot, paper, title, "Invalid", message);

    private static void ValidateWorkbook(IFormFile file)
    {
        if (file is null || file.Length == 0) throw new ArgumentException("The uploaded file is empty.");
        if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Only .xlsx files are supported.");
    }

    private static XLWorkbook OpenWorkbook(IFormFile file)
    {
        try
        {
            using var stream = file.OpenReadStream();
            return new XLWorkbook(stream);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new ArgumentException("The uploaded Excel file is invalid or corrupted.", exception);
        }
    }

    private static void ValidateToken(string token)
    {
        if (!Guid.TryParseExact(token, "N", out _)) throw new ArgumentException("Import token is invalid.");
    }

    private static void WriteTemplateHeader(IXLWorksheet sheet, IReadOnlyList<string> headers)
    {
        for (var i = 0; i < headers.Count; i++) sheet.Cell(1, i + 1).Value = headers[i];
        var range = sheet.Range(1, 1, 1, headers.Count);
        range.Style.Font.Bold = true;
        range.Style.Font.FontColor = XLColor.Red;
        range.Style.Fill.BackgroundColor = XLColor.LightYellow;
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.OutsideBorderColor = XLColor.Black;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.InsideBorderColor = XLColor.Black;
        sheet.SheetView.FreezeRows(1);
    }

    private static void StyleExampleRow(IXLWorksheet sheet, int columnCount)
    {
        var range = sheet.Range(2, 1, 2, columnCount);
        range.Style.Font.FontColor = XLColor.Gray;
        range.Style.Font.Italic = true;
    }

    private static void SetWidths(IXLWorksheet sheet, params double[] widths)
    {
        for (var i = 0; i < widths.Length; i++) sheet.Column(i + 1).Width = widths[i];
    }

    private static byte[] Save(XLWorkbook workbook)
    {
        using var output = new MemoryStream();
        workbook.SaveAs(output);
        return output.ToArray();
    }

    private static PublicationTitleResponse Map(PublicationTitleRecord value) => new(
        value.Id, value.RowNumber, value.InvoiceNumber ?? "", value.PaperId ?? "",
        value.CodeReference ?? "", value.Title ?? "", value.TitleYear ?? "", value.Status ?? "",
        value.ReferenceTitle ?? "", value.CreatedBy ?? "", value.CreatedOn, value.UpdatedTitle,
        value.UpdatedReferenceTitle, value.UpdatedTitleBy);
}

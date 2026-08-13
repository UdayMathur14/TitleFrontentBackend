using ClosedXML.Excel;
using Microsoft.Extensions.Caching.Memory;
using TitleFlow.Api.Application.Abstractions;
using TitleFlow.Api.Contracts.Titles;
using TitleFlow.Api.Domain.Entities;

namespace TitleFlow.Api.Application.Services;

public sealed class TitleService(ITitleRepository repository, IMemoryCache cache) : ITitleService
{
    private const string CachePrefix = "title-import:";
    public async Task<PagedResult<TitleResponse>> SearchAsync(TitleFilter filter, CancellationToken ct)
    {
        var safe = filter with { Page = Math.Max(1, filter.Page), PageSize = Math.Clamp(filter.PageSize, 1, 200) };
        var (items, total) = await repository.SearchAsync(safe, ct);
        return new(items.Select(Map).ToList(), safe.Page, safe.PageSize, total, (int)Math.Ceiling(total / (double)safe.PageSize));
    }
    public async Task<TitleResponse?> GetAsync(int id, CancellationToken ct) => (await repository.GetAsync(id, ct)) is { } value ? Map(value) : null;
    public async Task<TitleResponse> CreateAsync(CreateTitleRequest request, CancellationToken ct)
    {
        Validate(request.Title, request.InvoiceNumber, request.CodeReference, request.TitleYear);
        if (await repository.InvoiceCombinationExistsAsync(request.InvoiceNumber, request.CodeReference, request.TitleYear, null, ct)) throw new InvalidOperationException("Invoice, code reference and financial year combination already exists.");
        var entity = NewEntity(request.CodeReference, request.InvoiceNumber, request.Title, request.TitleYear, request.CreatedBy);
        entity.Status = await repository.FindByReferenceTitleAsync(entity.ReferenceTitle!, ct) is null ? "Clean" : "Blocked";
        await repository.AddAsync(entity, ct); await repository.SaveChangesAsync(ct); return Map(entity);
    }
    public async Task<TitleResponse?> UpdateAsync(int id, UpdateTitleRequest request, CancellationToken ct)
    {
        Validate(request.Title, request.InvoiceNumber, request.CodeReference, request.TitleYear);
        var entity = await repository.GetAsync(id, ct); if (entity is null) return null;
        if (await repository.InvoiceCombinationExistsAsync(request.InvoiceNumber, request.CodeReference, request.TitleYear, id, ct)) throw new InvalidOperationException("Invoice, code reference and financial year combination already exists.");
        entity.CodeReference=request.CodeReference.Trim(); entity.InvoiceNumber=request.InvoiceNumber.Trim(); entity.Title=request.Title.Trim(); entity.TitleYear=request.TitleYear.Trim(); entity.ReferenceTitle=TitleRules.Normalize(request.Title); entity.CreatedBy=request.CreatedBy.Trim();
        await repository.SaveChangesAsync(ct); return Map(entity);
    }
    public async Task<int> DeleteAsync(IEnumerable<int> ids, CancellationToken ct)
    {
        var found = new List<TitleRecord>(); foreach (var id in ids.Distinct()) if (await repository.GetAsync(id, ct) is { } record) found.Add(record);
        repository.RemoveRange(found); if (found.Count > 0) await repository.SaveChangesAsync(ct); return found.Count;
    }
    public Task<DropdownData> GetDropdownsAsync(CancellationToken ct) => repository.GetDropdownsAsync(ct);
    public async Task<DashboardResponse> GetDashboardAsync(CancellationToken ct)
    {
        var start = new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var total = await repository.CountAsync(null, null, ct); var clean = await repository.CountAsync("Clean", null, ct); var blocked = await repository.CountAsync("Blocked", null, ct); var month = await repository.CountAsync(null, start, ct);
        return new(total, clean, blocked, month, (await repository.GetRecentAsync(5, ct)).Select(Map).ToList());
    }
    public async Task<ImportPreview> PreviewImportAsync(IFormFile file, CancellationToken ct)
    {
        if (file.Length == 0) throw new ArgumentException("The uploaded file is empty.");
        var rows = new List<ImportRow>(); var normalizedInFile = new HashSet<string>();
        await using var stream = file.OpenReadStream(); using var workbook = new XLWorkbook(stream); var sheet = workbook.Worksheets.First(); var last = sheet.LastRowUsed()?.RowNumber() ?? 1;
        for (var number = 2; number <= last; number++)
        {
            var invoice=sheet.Cell(number,1).GetString().Trim(); var code=sheet.Cell(number,2).GetString().Trim(); var title=sheet.Cell(number,3).GetString().Trim(); var year=sheet.Cell(number,4).GetString().Trim();
            if (string.IsNullOrWhiteSpace(title)) break;
            var normalized=TitleRules.Normalize(title); string? invalid = string.IsNullOrWhiteSpace(year)?"Financial year is required":string.IsNullOrWhiteSpace(invoice)?"Invoice number is required":string.IsNullOrWhiteSpace(code)?"Code reference is required":!TitleRules.IsFinancialYear(year)?"Invalid financial year":!normalizedInFile.Add(normalized)?"Duplicate title in spreadsheet":null;
            if (invalid is not null){rows.Add(new(number,title,invoice,code,year,"Invalid",invalid));continue;}
            if (await repository.InvoiceCombinationExistsAsync(invoice,code,year,null,ct)){rows.Add(new(number,title,invoice,code,year,"Invalid","Invoice with code reference already exists"));continue;}
            var existing=await repository.FindByReferenceTitleAsync(normalized,ct);
            rows.Add(existing is null?new(number,title,invoice,code,year,"Clean","Ready to import"):new(number,title,invoice,code,year,"Blocked","Title already exists",existing.InvoiceNumber,existing.CodeReference));
        }
        var token=Guid.NewGuid().ToString("N"); cache.Set(CachePrefix+token,rows.Where(x=>x.Category=="Clean").ToList(),TimeSpan.FromMinutes(30));
        return new(file.FileName,rows.Count,rows.Count(x=>x.Category=="Clean"),rows.Count(x=>x.Category=="Blocked"),rows.Count(x=>x.Category=="Invalid"),rows,token);
    }
    public async Task<int> CommitImportAsync(string token, CancellationToken ct)
    {
        if (!cache.TryGetValue<List<ImportRow>>(CachePrefix+token,out var rows)||rows is null) throw new KeyNotFoundException("Import preview expired or does not exist.");
        var entities=rows.Select(x=>NewEntity(x.CodeReference,x.InvoiceNumber,x.Title,x.TitleYear,"Title import")).ToList(); await repository.AddRangeAsync(entities,ct); await repository.SaveChangesAsync(ct); cache.Remove(CachePrefix+token); return entities.Count;
    }
    public Task<byte[]> CreateTemplateAsync(CancellationToken ct)
    {
        using var workbook=new XLWorkbook(); var sheet=workbook.Worksheets.Add("UploadTitles"); var headers=new[]{"Invoice No (Required)","Code Ref (Required)","Title (Required)","Financial Year (Required)","Example"};
        for(var i=0;i<headers.Length;i++)sheet.Cell(1,i+1).Value=headers[i]; sheet.Range("A1:E1").Style.Font.Bold=true; sheet.Range("A1:E1").Style.Fill.BackgroundColor=XLColor.FromHtml("#DDF6EE"); sheet.Cell(2,1).Value="INV123";sheet.Cell(2,2).Value="CR456";sheet.Cell(2,3).Value="Sample Title";sheet.Cell(2,4).Value="2026-27";sheet.Cell(2,5).Value="Delete this example row";sheet.Columns().AdjustToContents();
        using var output=new MemoryStream();workbook.SaveAs(output);return Task.FromResult(output.ToArray());
    }
    public async Task<byte[]> ExportAsync(TitleFilter filter,CancellationToken ct)
    {
        var all=await repository.GetAllAsync(ct); var values=all.Where(x=>(!filter.Id.HasValue||x.Id==filter.Id)&& (string.IsNullOrWhiteSpace(filter.Title)||x.Title?.Contains(filter.Title,StringComparison.OrdinalIgnoreCase)==true)&& (string.IsNullOrWhiteSpace(filter.CodeReference)||x.CodeReference?.Contains(filter.CodeReference,StringComparison.OrdinalIgnoreCase)==true)&& (string.IsNullOrWhiteSpace(filter.InvoiceNumber)||x.InvoiceNumber?.Contains(filter.InvoiceNumber,StringComparison.OrdinalIgnoreCase)==true)&& (string.IsNullOrWhiteSpace(filter.TitleYear)||x.TitleYear==filter.TitleYear)&& (string.IsNullOrWhiteSpace(filter.Status)||x.Status==filter.Status));
        using var workbook=new XLWorkbook();var sheet=workbook.Worksheets.Add("Titles");var headers=new[]{"Id","Code Ref","Invoice No","Title","Created By","Created On","Year","Status"};for(var i=0;i<headers.Length;i++)sheet.Cell(1,i+1).Value=headers[i];var row=2;foreach(var x in values){sheet.Cell(row,1).Value=x.Id;sheet.Cell(row,2).Value=x.CodeReference;sheet.Cell(row,3).Value=x.InvoiceNumber;sheet.Cell(row,4).Value=x.Title;sheet.Cell(row,5).Value=x.CreatedBy;sheet.Cell(row,6).Value=x.CreatedOn.ToString("yyyy-MM-dd");sheet.Cell(row,7).Value=x.TitleYear;sheet.Cell(row,8).Value=x.Status;row++;}sheet.RangeUsed()?.CreateTable();sheet.Columns().AdjustToContents();using var output=new MemoryStream();workbook.SaveAs(output);return output.ToArray();
    }
    private static TitleRecord NewEntity(string code,string invoice,string title,string year,string createdBy)=>new(){CodeReference=code.Trim(),InvoiceNumber=invoice.Trim(),Title=title.Trim(),TitleYear=year.Trim(),ReferenceTitle=TitleRules.Normalize(title),Status="Clean",CreatedBy=string.IsNullOrWhiteSpace(createdBy)?"Title API":createdBy.Trim(),CreatedOn=DateOnly.FromDateTime(DateTime.UtcNow)};
    private static void Validate(string title,string invoice,string code,string year){if(string.IsNullOrWhiteSpace(title)||string.IsNullOrWhiteSpace(invoice)||string.IsNullOrWhiteSpace(code))throw new ArgumentException("Title, invoice number and code reference are required.");if(!TitleRules.IsFinancialYear(year))throw new ArgumentException("Financial year must use the format 2026-27.");}
    private static TitleResponse Map(TitleRecord x)=>new(x.Id,x.CodeReference??"",x.InvoiceNumber??"",x.Title??"",x.TitleYear??"",x.Status??"Clean",x.ReferenceTitle??"",x.CreatedBy??"",x.CreatedOn);
}

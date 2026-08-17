using System.ComponentModel.DataAnnotations;

namespace TitleFlow.Api.Contracts.Titles;

public sealed record TitleResponse(int Id, int RowNumber, string CodeReference, string InvoiceNumber, string Title, string TitleYear, string Status, string ReferenceTitle, string CreatedBy, DateOnly? CreatedOn);
public sealed record CreateTitleRequest(
    [Required, MaxLength(220)] string CodeReference,
    [Required, MaxLength(250)] string InvoiceNumber,
    [Required, MaxLength(1200)] string Title,
    [Required, MaxLength(7)] string TitleYear,
    [MaxLength(240)] string? CreatedBy = null);
public sealed record UpdateTitleRequest(
    [Required, MaxLength(220)] string CodeReference,
    [Required, MaxLength(250)] string InvoiceNumber,
    [Required, MaxLength(1200)] string Title,
    [Required, MaxLength(7)] string TitleYear,
    [MaxLength(240)] string? CreatedBy = null);
public sealed record DeleteTitlesRequest([Required, MinLength(1)] IReadOnlyCollection<int> Ids);
public sealed record CommitImportRequest([Required, StringLength(32, MinimumLength = 32)] string ImportToken);
public sealed record TitleFilter(int Page = 1, int PageSize = 20, int? Id = null, string? CodeReference = null, string? InvoiceNumber = null, string? Title = null, string? TitleYear = null, string? Status = null);
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount, int TotalPages);
public sealed record DropdownData(IReadOnlyList<string> CodeReferences, IReadOnlyList<string> InvoiceNumbers, IReadOnlyList<string> Titles, IReadOnlyList<string> Years);
public sealed record DashboardResponse(int TotalTitles, int CleanTitles, int BlockedTitles, int UploadedThisMonth, IReadOnlyList<TitleResponse> RecentTitles);
public sealed record ImportRow(int RowNumber, string Title, string InvoiceNumber, string CodeReference, string TitleYear, string Category, string Message, int? BlockedByRow = null, string? BlockedByInvoiceNumber = null, string? BlockedByCodeReference = null);
public sealed record ImportPreview(string FileName, int TotalRows, int CleanCount, int BlockedCount, int InvalidCount, IReadOnlyList<ImportRow> Rows, string ImportToken);
public sealed record ExistingTitle(int Id, int RowNumber, string ReferenceTitle, string InvoiceNumber, string CodeReference, string TitleYear);
public sealed record DashboardCounts(int Total, int Clean, int Blocked, int ThisMonth);

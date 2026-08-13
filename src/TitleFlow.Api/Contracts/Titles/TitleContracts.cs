namespace TitleFlow.Api.Contracts.Titles;

public sealed record TitleResponse(int Id, string CodeReference, string InvoiceNumber, string Title, string TitleYear, string Status, string ReferenceTitle, string CreatedBy, DateOnly CreatedOn);
public sealed record CreateTitleRequest(string CodeReference, string InvoiceNumber, string Title, string TitleYear, string CreatedBy);
public sealed record UpdateTitleRequest(string CodeReference, string InvoiceNumber, string Title, string TitleYear, string CreatedBy);
public sealed record DeleteTitlesRequest(IReadOnlyCollection<int> Ids);
public sealed record CommitImportRequest(string ImportToken);
public sealed record TitleFilter(int Page = 1, int PageSize = 20, int? Id = null, string? CodeReference = null, string? InvoiceNumber = null, string? Title = null, string? TitleYear = null, string? Status = null);
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount, int TotalPages);
public sealed record DropdownData(IReadOnlyList<string> CodeReferences, IReadOnlyList<string> InvoiceNumbers, IReadOnlyList<string> Titles, IReadOnlyList<string> Years);
public sealed record DashboardResponse(int TotalTitles, int CleanTitles, int BlockedTitles, int UploadedThisMonth, IReadOnlyList<TitleResponse> RecentTitles);
public sealed record ImportRow(int RowNumber, string Title, string InvoiceNumber, string CodeReference, string TitleYear, string Category, string Message, string? BlockedByInvoiceNumber = null, string? BlockedByCodeReference = null);
public sealed record ImportPreview(string FileName, int TotalRows, int CleanCount, int BlockedCount, int InvalidCount, IReadOnlyList<ImportRow> Rows, string ImportToken);

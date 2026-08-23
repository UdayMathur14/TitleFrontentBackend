using System.ComponentModel.DataAnnotations;

namespace TitleFlow.Api.Contracts.PublicationTitles;

public sealed record PublicationTitleResponse(int Id, int RowNumber, string LotNumber, string PaperId,
    string CodeReference, string Title, string TitleYear, string Status, string ReferenceTitle,
    string CreatedBy, DateOnly? CreatedOn, string? UpdatedTitle, string? UpdatedReferenceTitle,
    string? UpdatedTitleBy);

public sealed record PublicationTitleFilter(int Page = 1, int PageSize = 100, int? Id = null,
    string? CodeReference = null, string? LotNumber = null, string? Title = null,
    string? TitleYear = null, string? PaperId = null, string? Status = null);

public sealed record PublicationPagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize,
    int TotalCount, int TotalPages);

public sealed record DeletePublicationTitlesRequest([Required, MinLength(1)] IReadOnlyCollection<int> Ids);

public sealed record PublicationDropdownData(IReadOnlyList<string> CodeReferences,
    IReadOnlyList<string> LotNumbers, IReadOnlyList<string> Titles, IReadOnlyList<string> PaperIds,
    IReadOnlyList<string> Years);

public sealed record ExistingPublicationTitle(int Id, int RowNumber, string LotNumber, string PaperId,
    string CodeReference, string Title, string ReferenceTitle, string TitleYear,
    string? UpdatedTitle, string? UpdatedReferenceTitle);

public sealed record PublicationImportRow(int RowNumber, string LotNumber, string PaperId,
    string CodeReference, string Title, string TitleYear, string Category, string Message,
    int? BlockedById = null, int? BlockedByRow = null, string? BlockedByPaperId = null,
    string? BlockedByLotNumber = null, string? BlockedByCodeReference = null,
    string? BlockedByTitle = null);

public sealed record PublicationImportPreview(string FileName, int TotalRows, int CleanCount,
    int BlockedCount, int InvalidCount, IReadOnlyList<PublicationImportRow> Rows, string ImportToken);

public sealed record CommitPublicationImportRequest(
    [Required, StringLength(32, MinimumLength = 32)] string ImportToken,
    [MaxLength(240)] string? CreatedBy = null);

public sealed record ModifiedPublicationImportRow(int RowNumber, string LotNumber, string PaperId,
    string UpdatedTitle, string Category, string Message, int? TargetId = null,
    string? CurrentTitle = null);

public sealed record ModifiedPublicationImportPreview(string FileName, int TotalRows, int PassCount,
    int InvalidCount, IReadOnlyList<ModifiedPublicationImportRow> Rows, string ImportToken);

public sealed record CommitModifiedPublicationImportRequest(
    [Required, StringLength(32, MinimumLength = 32)] string ImportToken,
    [MaxLength(240)] string? UpdatedBy = null);

public sealed record PublicationDashboardResponse(int TotalTitles, int CleanTitles,
    int ModifiedTitles, int UploadedThisMonth, IReadOnlyList<PublicationTitleResponse> RecentTitles);

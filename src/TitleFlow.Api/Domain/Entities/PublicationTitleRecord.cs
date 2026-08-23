using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TitleFlow.Api.Domain.Entities;

[Table("TBL_TITLE_PUBLICATIONS")]
public sealed class PublicationTitleRecord
{
    [Key] public int Id { get; set; }
    public int RowNumber { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? PaperId { get; set; }
    public string? CodeReference { get; set; }
    public string? Title { get; set; }
    [Column("CREATED_BY")] public string? CreatedBy { get; set; }
    [Column("CREATED_ON")] public DateOnly? CreatedOn { get; set; }
    public string? Status { get; set; }
    public string? ReferenceTitle { get; set; }
    public string? TitleYear { get; set; }
    public string? UpdatedTitle { get; set; }
    public string? UpdatedReferenceTitle { get; set; }
    public string? UpdatedTitleBy { get; set; }
}

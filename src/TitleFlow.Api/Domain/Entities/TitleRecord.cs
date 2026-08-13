using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TitleFlow.Api.Domain.Entities;

[Table("TBL_TITLES")]
public sealed class TitleRecord
{
    [Key] public int Id { get; set; }
    public string? CodeReference { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? Title { get; set; }
    [Column("CREATED_BY")] public string? CreatedBy { get; set; }
    [Column("CREATED_ON")] public DateOnly CreatedOn { get; set; }
    public string? Status { get; set; }
    public string? ReferenceTitle { get; set; }
    public string? TitleYear { get; set; }
}

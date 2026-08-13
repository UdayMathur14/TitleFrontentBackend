using Microsoft.EntityFrameworkCore;
using TitleFlow.Api.Domain.Entities;

namespace TitleFlow.Api.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<TitleRecord> Titles => Set<TitleRecord>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var title = modelBuilder.Entity<TitleRecord>();
        title.HasKey(x => x.Id);
        title.Property(x => x.CreatedOn).HasConversion(v => v.ToDateTime(TimeOnly.MinValue), v => DateOnly.FromDateTime(v));
        title.HasIndex(x => x.ReferenceTitle);
        title.HasIndex(x => new { x.InvoiceNumber, x.CodeReference, x.TitleYear });
    }
}

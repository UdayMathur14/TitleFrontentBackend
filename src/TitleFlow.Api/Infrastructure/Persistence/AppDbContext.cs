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
        title.Property(x => x.Id).ValueGeneratedOnAdd();
        title.Property(x => x.InvoiceNumber).HasMaxLength(250).IsUnicode(false);
        title.Property(x => x.CodeReference).HasMaxLength(220).IsUnicode(false);
        title.Property(x => x.Title).HasMaxLength(1200);
        title.Property(x => x.CreatedBy).HasMaxLength(240);
        title.Property(x => x.Status).HasMaxLength(300);
        title.Property(x => x.ReferenceTitle).HasMaxLength(700).IsUnicode(false);
        title.Property(x => x.TitleYear).HasMaxLength(204).IsUnicode(false);
    }
}

using Microsoft.EntityFrameworkCore;
using TitleFlow.Api.Domain.Entities;

namespace TitleFlow.Api.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<TitleRecord> Titles => Set<TitleRecord>();
    public DbSet<PublicationTitleRecord> PublicationTitles => Set<PublicationTitleRecord>();

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

        var publication = modelBuilder.Entity<PublicationTitleRecord>();
        publication.HasKey(x => x.Id);
        publication.Property(x => x.Id).ValueGeneratedOnAdd();
        publication.Property(x => x.InvoiceNumber).HasMaxLength(250).IsUnicode(false);
        publication.Property(x => x.PaperId).HasMaxLength(250).IsUnicode(false);
        publication.Property(x => x.CodeReference).HasMaxLength(220).IsUnicode(false);
        publication.Property(x => x.Title).HasMaxLength(1200);
        publication.Property(x => x.CreatedBy).HasMaxLength(240);
        publication.Property(x => x.Status).HasMaxLength(300);
        publication.Property(x => x.ReferenceTitle).HasMaxLength(700).IsUnicode(false);
        publication.Property(x => x.TitleYear).HasMaxLength(204).IsUnicode(false);
        publication.Property(x => x.UpdatedTitle).HasMaxLength(1200);
        publication.Property(x => x.UpdatedReferenceTitle).HasMaxLength(700).IsUnicode(false);
        publication.Property(x => x.UpdatedTitleBy).HasMaxLength(240);
    }
}

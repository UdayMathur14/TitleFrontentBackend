using Microsoft.EntityFrameworkCore;
using TitleFlow.Api.Application.Services;
using TitleFlow.Api.Domain.Entities;

namespace TitleFlow.Api.Infrastructure.Persistence;

public static class DemoDataSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        if (await db.Titles.AnyAsync()) return;
        var values = new[]
        {
            ("CR-4582","INV-2026-118","Global Intellectual Property Review","2026-27","Clean","Uday Mathur"),
            ("CR-4579","INV-2026-116","Asia Pacific Legal Directory","2026-27","Clean","Anjali Singh"),
            ("CR-4571","INV-2026-109","European Patent Leaders","2025-26","Blocked","Uday Mathur"),
            ("CR-4564","INV-2026-103","India Corporate Counsel Handbook","2025-26","Clean","Rhea Kapoor"),
            ("CR-4558","INV-2026-098","Trademark Strategy Annual","2025-26","Clean","Anjali Singh")
        };
        var index = 0;
        db.Titles.AddRange(values.Select(x => new TitleRecord { CodeReference=x.Item1, InvoiceNumber=x.Item2, Title=x.Item3, TitleYear=x.Item4, Status=x.Item5, CreatedBy=x.Item6, CreatedOn=DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-index++)), ReferenceTitle=TitleRules.Normalize(x.Item3) }));
        await db.SaveChangesAsync();
    }
}

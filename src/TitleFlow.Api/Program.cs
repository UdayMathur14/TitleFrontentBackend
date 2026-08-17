using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TitleFlow.Api.Application.Abstractions;
using TitleFlow.Api.Application.Services;
using TitleFlow.Api.Contracts.Titles;
using TitleFlow.Api.Infrastructure.Persistence;
using TitleFlow.Api.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMemoryCache();
builder.Services.AddResponseCompression(options => options.EnableForHttps = true);
builder.Services.AddSingleton<TitleCache>();
builder.Services.AddScoped<ITitleRepository, TitleRepository>();
builder.Services.AddScoped<ITitleService, TitleService>();
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = 50 * 1024 * 1024);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString,
    sql => sql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(2), null)));

builder.Services.AddCors(options => options.AddPolicy("Angular", policy => policy.WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:4200"]).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseExceptionHandler(handler => handler.Run(async context =>
{
    var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    var status = exception switch
    {
        ArgumentException => StatusCodes.Status400BadRequest,
        KeyNotFoundException => StatusCodes.Status404NotFound,
        TitleConflictException => StatusCodes.Status409Conflict,
        DbUpdateConcurrencyException => StatusCodes.Status409Conflict,
        DbUpdateException => StatusCodes.Status409Conflict,
        _ when HasSqlException(exception) => StatusCodes.Status503ServiceUnavailable,
        _ => StatusCodes.Status500InternalServerError
    };
    context.Response.StatusCode = status;
    context.Response.ContentType = "application/problem+json";
    var title = status switch
    {
        StatusCodes.Status500InternalServerError => "Unexpected server error.",
        StatusCodes.Status503ServiceUnavailable => "Database is temporarily unavailable.",
        _ => exception?.Message ?? "The request could not be completed."
    };
    await context.Response.WriteAsJsonAsync(new ProblemDetails
    {
        Status = status,
        Title = title,
        Detail = app.Environment.IsDevelopment() && status >= 500 ? exception?.Message : null,
        Instance = context.Request.Path
    });
}));
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
if (!app.Environment.IsDevelopment()) app.UseHttpsRedirection();
app.UseResponseCompression();
app.UseCors("Angular");
app.MapControllers();

await using (var scope = app.Services.CreateAsyncScope())
{
    try
    {
        var titleService = scope.ServiceProvider.GetRequiredService<ITitleService>();
        await titleService.SearchAsync(new TitleFilter(), CancellationToken.None);
        await titleService.GetDashboardAsync(CancellationToken.None);
        await titleService.GetDropdownsAsync(null, 10_000, CancellationToken.None);
        await titleService.CreateTemplateAsync(CancellationToken.None);
    }
    catch (Exception exception)
    {
        app.Logger.LogWarning(exception, "Title API warm-up failed; the application will continue starting.");
    }
}

app.Run();

static bool HasSqlException(Exception? exception)
{
    while (exception is not null)
    {
        if (exception is SqlException) return true;
        exception = exception.InnerException;
    }
    return false;
}

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TitleFlow.Api.Application.Abstractions;
using TitleFlow.Api.Application.Services;
using TitleFlow.Api.Infrastructure.Persistence;
using TitleFlow.Api.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<ITitleRepository, TitleRepository>();
builder.Services.AddScoped<ITitleService, TitleService>();

if (builder.Configuration.GetValue("Database:UseDemoData", true))
    builder.Services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase("TitleFlowDemo"));
else
    builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddCors(options => options.AddPolicy("Angular", policy => policy.WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:4200"]).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseExceptionHandler(handler => handler.Run(async context =>
{
    var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    var status = exception switch { ArgumentException => 400, InvalidOperationException => 409, KeyNotFoundException => 404, _ => 500 };
    context.Response.StatusCode = status;
    await context.Response.WriteAsJsonAsync(new ProblemDetails { Status=status, Title=status==500?"Unexpected server error":exception?.Message, Detail=app.Environment.IsDevelopment()?exception?.StackTrace:null });
}));
if (app.Environment.IsDevelopment()) { app.MapOpenApi(); app.UseSwagger(); app.UseSwaggerUI(); }
app.UseHttpsRedirection(); app.UseCors("Angular"); app.MapControllers();
using (var scope = app.Services.CreateScope()) if (builder.Configuration.GetValue("Database:UseDemoData", true)) await DemoDataSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>());
app.Run();

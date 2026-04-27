using InsightCore.Api.Middleware;
using InsightCore.Application;
using InsightCore.Infrastructure;
using InsightCore.Api.Middleware;
using InsightCore.Application.Interfaces;
using InsightCore.Infrastructure.Implementations;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.File(
        path: "Logs/insightcore-api-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        outputTemplate:
        "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
    )
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddScoped<IEntitySchemaGenerator, EntitySchemaGenerator>();
builder.Services.AddScoped<IDBExecution, DBExecution>();

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<IClientService, ClientService>();
builder.Services.AddScoped<IBlobStorageService, BlobStorageService>();

var app = builder.Build();

app.UseMiddleware<ExceptionMiddleware>();

Console.WriteLine(app.Environment.EnvironmentName);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllers();

app.Run();

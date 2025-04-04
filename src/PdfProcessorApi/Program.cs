using Elastic.Clients.Elasticsearch;
using Microsoft.EntityFrameworkCore;
using PdfProcessorApi.Data;
using PdfProcessorApi.Services;

var builder = WebApplication.CreateBuilder(args);

var loggerFactory = LoggerFactory.Create(loggingBuilder => loggingBuilder
    .AddConfiguration(builder.Configuration.GetSection("Logging"))
    .AddConsole());
var startupLogger = loggerFactory.CreateLogger<Program>();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
);

startupLogger.LogInformation("Elasticsearch client configuring");

var elasticsearchUrl = builder.Configuration["ElasticsearchSettings:Url"] ?? "http://localhost:9200";
var defaultIndex = builder.Configuration["ElasticsearchSettings:DefaultIndex"] ?? "pdf-documents";

startupLogger.LogInformation("Elasticsearch URL: {ElasticUrl}, Default Index: {DefaultIndex}", elasticsearchUrl, defaultIndex);

builder.Services.AddSingleton<ElasticsearchClient>(sp =>
{
    startupLogger.LogInformation("Creating Elasticsearch client");

    var settings = new ElasticsearchClientSettings(new Uri(elasticsearchUrl))
        .DefaultIndex(defaultIndex)
        .DisableDirectStreaming(true)
        .EnableDebugMode();

    startupLogger.LogInformation("Elasticsearch client created");
    return new ElasticsearchClient(settings);
});

builder.Services.AddScoped<IPdfTextExtractorService, PdfTextExtractorService>();
builder.Services.AddScoped<IElasticsearchIndexingService, ElasticsearchIndexingService>();


builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

using LibraryRecommendation.Api.Data;
using LibraryRecommendation.Api.Repositories;
using LibraryRecommendation.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddDbContext<LibraryDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IBookRepository, BookRepository>();
builder.Services.AddScoped<DatasetImporter>();
builder.Services.AddScoped<GoogleBooksImporter>();

// The recommendation service needs one fitted vectorizer per category, so vectorizers are
// transient and handed to it as a factory rather than as a single shared instance.
builder.Services.AddTransient<ITextVectorizer, TfIdfVectorizer>();
builder.Services.AddSingleton<Func<ITextVectorizer>>(sp => sp.GetRequiredService<ITextVectorizer>);
builder.Services.AddSingleton<ISimilarityCalculator, CosineSimilarity>();
builder.Services.AddSingleton<IRecommendationService, ContentBasedRecommendationService>();

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Dataset import is an explicit command-line operation, so ordinary runs never re-import.
//   dotnet run --project LibraryRecommendation.Api -- --import-dataset <path>
if (args is ["--import-dataset", var datasetPath, ..])
{
    if (!File.Exists(datasetPath))
    {
        Console.Error.WriteLine($"Dataset file not found: {datasetPath}");
        return 1;
    }

    using var importScope = app.Services.CreateScope();
    var importer = importScope.ServiceProvider.GetRequiredService<DatasetImporter>();
    var report = await importer.ImportAsync(datasetPath);
    Console.WriteLine(report.Format());
    return 0;
}

if (args.Contains("--import-dataset"))
{
    Console.Error.WriteLine("Usage: --import-dataset <path-to-booksummaries.txt>");
    return 1;
}

// Educational import is additive: it replaces only the Educational rows.
//   dotnet run --project LibraryRecommendation.Api -- --import-educational <path>
if (args is ["--import-educational", var educationalPath, ..])
{
    if (!File.Exists(educationalPath))
    {
        Console.Error.WriteLine($"Dataset file not found: {educationalPath}");
        return 1;
    }

    using var educationalScope = app.Services.CreateScope();
    var educationalImporter = educationalScope.ServiceProvider.GetRequiredService<GoogleBooksImporter>();
    var educationalReport = await educationalImporter.ImportAsync(educationalPath);
    Console.WriteLine(educationalReport.Format());
    return 0;
}

if (args.Contains("--import-educational"))
{
    Console.Error.WriteLine("Usage: --import-educational <path-to-google_books_dataset.csv>");
    return 1;
}

// Books are loaded by the importer, never seeded, so an empty table means the operator has a
// step left to run. Warn rather than throw: the API is still usable and every controller
// already degrades gracefully when a query fails.
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
    try
    {
        if (!await context.Books.AsNoTracking().AnyAsync())
        {
            app.Logger.LogWarning(
                "The Books table is empty. Recommendations and book listings will return nothing " +
                "until the dataset is imported. Run: dotnet run --project LibraryRecommendation.Api " +
                "-- --import-dataset <path-to-booksummaries.txt>");
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Could not check whether the Books table is populated.");
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

return 0;

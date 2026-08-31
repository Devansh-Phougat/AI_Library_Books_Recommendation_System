using System.Diagnostics;
using LibraryRecommendation.Mvc.Models;
using LibraryRecommendation.Mvc.Services;
using Microsoft.AspNetCore.Mvc;

namespace LibraryRecommendation.Mvc.Controllers;

public class HomeController : Controller
{
    private const string ApiUnreachableMessage =
        "The API is not reachable. Start LibraryRecommendation.Api and try again.";

    private const int FeaturedBookCount = 5;

    private readonly IBookApiService _api;
    private readonly ILogger<HomeController> _logger;

    public HomeController(IBookApiService api, ILogger<HomeController> logger)
    {
        _api = api;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            var books = await _api.GetAllBooksAsync();

            return View(new HomeViewModel
            {
                // Both categories are always listed, so an empty one reads as "nothing here yet"
                // rather than silently disappearing.
                Categories = BookCategories.All.Select(name => Summarize(name, books)).ToList(),
                // The imported dataset carries no ratings, so recency is the only meaningful order.
                FeaturedBooks = books
                    .OrderByDescending(book => book.PublishedYear)
                    .ThenBy(book => book.Title)
                    .Take(FeaturedBookCount)
                    .ToList()
            });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to load the home page because the API call failed.");
            TempData["Error"] = ApiUnreachableMessage;
            return View(new HomeViewModel());
        }
    }

    private static CategorySummary Summarize(string category, IEnumerable<BookViewModel> books)
    {
        var inCategory = books
            .Where(book => string.Equals(book.Category, category, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return new CategorySummary
        {
            Name = category,
            BookCount = inCategory.Count,
            Genres = inCategory
                .Where(book => !string.IsNullOrWhiteSpace(book.Genre))
                .GroupBy(book => book.Genre)
                .Select(group => new GenreSummary { Name = group.Key, BookCount = group.Count() })
                .OrderByDescending(genre => genre.BookCount)
                .ThenBy(genre => genre.Name)
                .ToList()
        };
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}

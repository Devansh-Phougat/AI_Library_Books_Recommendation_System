using LibraryRecommendation.Mvc.Models;
using LibraryRecommendation.Mvc.Services;
using Microsoft.AspNetCore.Mvc;

namespace LibraryRecommendation.Mvc.Controllers;

public class BooksController : Controller
{
    private const string ApiUnreachableMessage =
        "The API is not reachable. Start LibraryRecommendation.Api and try again.";

    private const int RecommendationCount = 5;

    private readonly IBookApiService _api;
    private readonly ILogger<BooksController> _logger;

    public BooksController(IBookApiService api, ILogger<BooksController> logger)
    {
        _api = api;
        _logger = logger;
    }

    public async Task<IActionResult> Index(string? category)
    {
        try
        {
            var books = InCategory(await _api.GetAllBooksAsync(), category);
            PopulateFilters(books, selectedGenre: string.Empty, selectedCategory: category);
            return View(books);
        }
        catch (HttpRequestException ex)
        {
            return ApiUnreachable(ex, "load the book list");
        }
    }

    public async Task<IActionResult> FilterByGenre(string genre, string? category)
    {
        if (string.IsNullOrWhiteSpace(genre))
        {
            return RedirectToAction(nameof(Index), new { category });
        }

        try
        {
            var allBooks = InCategory(await _api.GetAllBooksAsync(), category);
            var books = InCategory(await _api.GetBooksByGenreAsync(genre), category);

            PopulateFilters(allBooks, selectedGenre: genre, selectedCategory: category);
            return View(nameof(Index), books);
        }
        catch (HttpRequestException ex)
        {
            return ApiUnreachable(ex, $"filter books by genre '{genre}'");
        }
    }

    public async Task<IActionResult> Details(int id)
    {
        try
        {
            var book = await _api.GetBookByIdAsync(id);
            if (book is null)
            {
                return NotFound();
            }

            ViewBag.Recommendations = await _api.GetSimilarBooksAsync(id, RecommendationCount);
            return View(book);
        }
        catch (HttpRequestException ex)
        {
            return ApiUnreachable(ex, $"load book {id}");
        }
    }

    public IActionResult Create() => View(new BookViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BookViewModel book)
    {
        if (!ModelState.IsValid)
        {
            return View(book);
        }

        try
        {
            var created = await _api.CreateBookAsync(book);
            if (created is null)
            {
                ModelState.AddModelError(string.Empty, "The API rejected the book. Check the values and try again.");
                return View(book);
            }

            TempData["Success"] = $"\"{created.Title}\" was added.";
            return RedirectToAction(nameof(Index));
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to create a book through the API.");
            TempData["Error"] = ApiUnreachableMessage;
            return View(book);
        }
    }

    public async Task<IActionResult> Edit(int id)
    {
        try
        {
            var book = await _api.GetBookByIdAsync(id);
            if (book is null)
            {
                return NotFound();
            }

            return View(book);
        }
        catch (HttpRequestException ex)
        {
            return ApiUnreachable(ex, $"load book {id} for editing");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, BookViewModel book)
    {
        if (id != book.Id)
        {
            ModelState.AddModelError(string.Empty, "The book id in the address does not match the submitted book.");
        }

        if (!ModelState.IsValid)
        {
            return View(book);
        }

        try
        {
            if (!await _api.UpdateBookAsync(book))
            {
                ModelState.AddModelError(string.Empty, "The API rejected the update, or the book no longer exists.");
                return View(book);
            }

            TempData["Success"] = $"\"{book.Title}\" was updated.";
            return RedirectToAction(nameof(Details), new { id = book.Id });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to update book {BookId} through the API.", id);
            TempData["Error"] = ApiUnreachableMessage;
            return View(book);
        }
    }

    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var book = await _api.GetBookByIdAsync(id);
            if (book is null)
            {
                return NotFound();
            }

            return View(book);
        }
        catch (HttpRequestException ex)
        {
            return ApiUnreachable(ex, $"load book {id} for deletion");
        }
    }

    [HttpPost]
    [ActionName(nameof(Delete))]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        try
        {
            if (!await _api.DeleteBookAsync(id))
            {
                TempData["Error"] = "That book no longer exists.";
                return RedirectToAction(nameof(Index));
            }

            TempData["Success"] = "The book was deleted.";
            return RedirectToAction(nameof(Index));
        }
        catch (HttpRequestException ex)
        {
            return ApiUnreachable(ex, $"delete book {id}");
        }
    }

    private IActionResult ApiUnreachable(HttpRequestException ex, string attemptedAction)
    {
        _logger.LogError(ex, "Failed to {AttemptedAction} because the API call failed.", attemptedAction);
        TempData["Error"] = ApiUnreachableMessage;

        PopulateFilters(new List<BookViewModel>(), selectedGenre: string.Empty, selectedCategory: null);
        return View(nameof(Index), new List<BookViewModel>());
    }

    /// <summary>An unset or unrecognised category means "all categories".</summary>
    private static List<BookViewModel> InCategory(List<BookViewModel> books, string? category)
        => string.IsNullOrWhiteSpace(category)
            ? books
            : books.Where(book => string.Equals(book.Category, category, StringComparison.OrdinalIgnoreCase)).ToList();

    private void PopulateFilters(IEnumerable<BookViewModel> books, string selectedGenre, string? selectedCategory)
    {
        // Genres are drawn from the selected category, so the two filters stay consistent.
        ViewBag.Genres = ExtractGenres(books);
        ViewBag.SelectedGenre = selectedGenre;
        ViewBag.Categories = BookCategories.All;
        ViewBag.SelectedCategory = selectedCategory ?? string.Empty;
    }

    private static List<string> ExtractGenres(IEnumerable<BookViewModel> books)
        => books.Select(book => book.Genre)
            .Where(genre => !string.IsNullOrWhiteSpace(genre))
            .Distinct()
            .OrderBy(genre => genre)
            .ToList();
}

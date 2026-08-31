namespace LibraryRecommendation.Mvc.Models;

public class HomeViewModel
{
    public List<CategorySummary> Categories { get; set; } = new();

    public List<BookViewModel> FeaturedBooks { get; set; } = new();

    public int TotalBooks => Categories.Sum(category => category.BookCount);

    public int GenreCount => Categories
        .SelectMany(category => category.Genres)
        .Select(genre => genre.Name)
        .Distinct()
        .Count();
}

public class CategorySummary
{
    public string Name { get; set; } = string.Empty;

    public int BookCount { get; set; }

    public List<GenreSummary> Genres { get; set; } = new();
}

public class GenreSummary
{
    public string Name { get; set; } = string.Empty;

    public int BookCount { get; set; }
}

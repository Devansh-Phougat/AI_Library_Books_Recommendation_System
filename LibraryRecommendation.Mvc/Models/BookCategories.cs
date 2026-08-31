namespace LibraryRecommendation.Mvc.Models;

/// <summary>
/// Mirrors the categories the API accepts. This is a wire contract, not shared code — the MVC
/// project deliberately has no reference to the API assembly.
/// </summary>
public static class BookCategories
{
    public const string Fiction = "Fiction";
    public const string Educational = "Educational";

    public const string AllowedPattern = "^(Fiction|Educational)$";

    public const string ValidationMessage = "Category must be either 'Fiction' or 'Educational'.";

    public const int MaxLength = 20;

    public static readonly string[] All = { Fiction, Educational };
}

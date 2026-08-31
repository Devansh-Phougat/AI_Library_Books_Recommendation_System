namespace LibraryRecommendation.Api.Models;

public static class BookCategories
{
    public const string Fiction = "Fiction";
    public const string Educational = "Educational";

    /// <summary>Usable as a [RegularExpression] argument, which requires a compile-time constant.</summary>
    public const string AllowedPattern = "^(Fiction|Educational)$";

    public const string ValidationMessage = "Category must be either 'Fiction' or 'Educational'.";

    public const int MaxLength = 20;

    public static readonly string[] All = { Fiction, Educational };
}

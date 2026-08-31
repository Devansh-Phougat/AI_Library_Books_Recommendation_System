using LibraryRecommendation.Api.Models;

namespace LibraryRecommendation.Api.Services;

public interface IRecommendationService
{
    Task<List<BookRecommendation>> GetSimilarBooksAsync(int bookId, int count);
}

public class BookRecommendation
{
    public required Book Book { get; init; }

    public double SimilarityScore { get; init; }

    public List<string> MatchingTerms { get; init; } = new();
}

using LibraryRecommendation.Api.Models;
using LibraryRecommendation.Api.Repositories;

namespace LibraryRecommendation.Api.Services;

public class ContentBasedRecommendationService : IRecommendationService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Func<ITextVectorizer> _vectorizerFactory;
    private readonly ISimilarityCalculator _similarityCalculator;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private volatile VectorCache? _cache;

    public ContentBasedRecommendationService(
        IServiceScopeFactory scopeFactory,
        Func<ITextVectorizer> vectorizerFactory,
        ISimilarityCalculator similarityCalculator)
    {
        _scopeFactory = scopeFactory;
        _vectorizerFactory = vectorizerFactory;
        _similarityCalculator = similarityCalculator;
    }

    public async Task<List<BookRecommendation>> GetSimilarBooksAsync(int bookId, int count)
    {
        var cache = await GetCacheAsync();
        if (count <= 0 || !cache.CategoryOfBook.TryGetValue(bookId, out var category))
        {
            return new List<BookRecommendation>();
        }

        // Candidates are drawn only from the source book's own category.
        var candidates = cache.ByCategory[category];
        if (!candidates.TryGetValue(bookId, out var source))
        {
            return new List<BookRecommendation>();
        }

        return candidates.Values
            .Where(candidate => candidate.Book.Id != bookId)
            .Select(candidate => new
            {
                candidate.Book,
                Score = _similarityCalculator.Compute(source.Vector, candidate.Vector),
                candidate.Vector
            })
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Book.Id)
            .Take(count)
            .Select(candidate => new BookRecommendation
            {
                Book = candidate.Book,
                SimilarityScore = candidate.Score,
                MatchingTerms = GetTopMatchingTerms(source.Vector, candidate.Vector)
            })
            .ToList();
    }

    private async Task<VectorCache> GetCacheAsync()
    {
        var cached = _cache;
        if (cached is not null)
        {
            return cached;
        }

        await _initLock.WaitAsync();
        try
        {
            if (_cache is not null)
            {
                return _cache;
            }

            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IBookRepository>();
            var books = await repository.GetAllBooksAsync();

            var byCategory = new Dictionary<string, Dictionary<int, CachedBook>>(StringComparer.OrdinalIgnoreCase);
            var categoryOfBook = new Dictionary<int, string>(books.Count);

            // One vectorizer per category, each fitted over only that category's documents, so
            // document frequencies and the corpus size N are scoped to the category. A book is
            // never compared against, and never influences the weighting of, another category.
            foreach (var group in books.GroupBy(book => book.Category, StringComparer.OrdinalIgnoreCase))
            {
                var categoryBooks = group.ToList();
                var vectorizer = _vectorizerFactory();
                vectorizer.Fit(categoryBooks.Select(BuildDocument));

                var vectors = new Dictionary<int, CachedBook>(categoryBooks.Count);
                foreach (var book in categoryBooks)
                {
                    vectors[book.Id] = new CachedBook(book, vectorizer.Transform(BuildDocument(book)));
                    categoryOfBook[book.Id] = group.Key;
                }

                byCategory[group.Key] = vectors;
            }

            _cache = new VectorCache(byCategory, categoryOfBook);
            return _cache;
        }
        finally
        {
            _initLock.Release();
        }
    }

    // Genre appears once. It was weighted 2x when descriptions averaged ~500 characters and the
    // corpus held 70 books; against the 5000-book corpus the extra copy changes 18% of result
    // sets but doubles how often a genre token displaces a real content word in MatchingTerms.
    // See "Genre weighting" in README.md for the measurements.
    private static string BuildDocument(Book book)
        => $"{book.Title} {book.Author} {book.Genre} {book.Description}";

    private static List<string> GetTopMatchingTerms(Dictionary<string, double> a, Dictionary<string, double> b)
    {
        var (smaller, larger) = a.Count <= b.Count ? (a, b) : (b, a);

        return smaller
            .Where(entry => larger.ContainsKey(entry.Key))
            .Select(entry => new { Term = entry.Key, CombinedWeight = entry.Value + larger[entry.Key] })
            .OrderByDescending(entry => entry.CombinedWeight)
            .Take(3)
            .Select(entry => entry.Term)
            .ToList();
    }

    private sealed record CachedBook(Book Book, Dictionary<string, double> Vector);

    private sealed record VectorCache(
        Dictionary<string, Dictionary<int, CachedBook>> ByCategory,
        Dictionary<int, string> CategoryOfBook);
}

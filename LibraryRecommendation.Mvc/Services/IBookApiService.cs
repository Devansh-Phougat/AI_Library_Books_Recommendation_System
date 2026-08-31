using LibraryRecommendation.Mvc.Models;

namespace LibraryRecommendation.Mvc.Services;

public interface IBookApiService
{
    Task<List<BookViewModel>> GetAllBooksAsync();
    Task<BookViewModel?> GetBookByIdAsync(int id);
    Task<BookViewModel?> CreateBookAsync(BookViewModel book);
    Task<bool> UpdateBookAsync(BookViewModel book);
    Task<bool> DeleteBookAsync(int id);
    Task<List<BookViewModel>> GetBooksByGenreAsync(string genre);
    Task<List<RecommendationViewModel>> GetSimilarBooksAsync(int bookId, int count);
}

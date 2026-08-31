using LibraryRecommendation.Api.Models;

namespace LibraryRecommendation.Api.Repositories;

public interface IBookRepository
{
    Task<List<Book>> GetAllBooksAsync();
    Task<Book?> GetBookByIdAsync(int id);
    Task<int> CreateBookAsync(Book book);
    Task UpdateBookAsync(Book book);
    Task DeleteBookAsync(int id);
    Task<List<Book>> GetBooksByGenreAsync(string genre);
}

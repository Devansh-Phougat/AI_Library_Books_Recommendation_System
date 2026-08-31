using LibraryRecommendation.Api.Data;
using LibraryRecommendation.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace LibraryRecommendation.Api.Repositories;

public class BookRepository : IBookRepository
{
    private readonly LibraryDbContext _context;

    public BookRepository(LibraryDbContext context)
    {
        _context = context;
    }

    public async Task<List<Book>> GetAllBooksAsync()
    {
        return await _context.Books
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Book?> GetBookByIdAsync(int id)
    {
        return await _context.Books
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == id);
    }

    public async Task<int> CreateBookAsync(Book book)
    {
        _context.Books.Add(book);
        await _context.SaveChangesAsync();
        return book.Id;
    }

    public async Task UpdateBookAsync(Book book)
    {
        _context.Books.Update(book);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteBookAsync(int id)
    {
        await _context.Books
            .Where(b => b.Id == id)
            .ExecuteDeleteAsync();
    }

    public async Task<List<Book>> GetBooksByGenreAsync(string genre)
    {
        return await _context.Books
            .AsNoTracking()
            .Where(b => b.Genre == genre)
            .ToListAsync();
    }
}

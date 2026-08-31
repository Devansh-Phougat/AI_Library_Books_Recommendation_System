using System.Net;
using System.Net.Http.Json;
using LibraryRecommendation.Mvc.Models;

namespace LibraryRecommendation.Mvc.Services;

public class BookApiService : IBookApiService
{
    private readonly HttpClient _httpClient;

    public BookApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<BookViewModel>> GetAllBooksAsync()
    {
        var books = await _httpClient.GetFromJsonAsync<List<BookViewModel>>("api/Books");
        return books ?? new List<BookViewModel>();
    }

    public async Task<BookViewModel?> GetBookByIdAsync(int id)
    {
        var response = await _httpClient.GetAsync($"api/Books/{id}");
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<BookViewModel>();
    }

    public async Task<BookViewModel?> CreateBookAsync(BookViewModel book)
    {
        var response = await _httpClient.PostAsJsonAsync("api/Books", book);
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<BookViewModel>();
    }

    public async Task<bool> UpdateBookAsync(BookViewModel book)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/Books/{book.Id}", book);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }

    public async Task<bool> DeleteBookAsync(int id)
    {
        var response = await _httpClient.DeleteAsync($"api/Books/{id}");
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }

    public async Task<List<BookViewModel>> GetBooksByGenreAsync(string genre)
    {
        var books = await _httpClient.GetFromJsonAsync<List<BookViewModel>>(
            $"api/Books/Genre/{Uri.EscapeDataString(genre)}");
        return books ?? new List<BookViewModel>();
    }

    public async Task<List<RecommendationViewModel>> GetSimilarBooksAsync(int bookId, int count)
    {
        var response = await _httpClient.GetAsync($"api/Recommendations/similar/{bookId}?count={count}");
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
        {
            return new List<RecommendationViewModel>();
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<RecommendationViewModel>>()
            ?? new List<RecommendationViewModel>();
    }
}

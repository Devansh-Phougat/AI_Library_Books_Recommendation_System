using LibraryRecommendation.Api.Dtos;
using LibraryRecommendation.Api.Models;
using LibraryRecommendation.Api.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace LibraryRecommendation.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BooksController : ControllerBase
{
    private readonly IBookRepository _repository;
    private readonly ILogger<BooksController> _logger;

    public BooksController(IBookRepository repository, ILogger<BooksController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<BookResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<BookResponseDto>>> GetAllBooks()
    {
        try
        {
            var books = await _repository.GetAllBooksAsync();
            return Ok(books.Select(ToResponseDto).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve books.");
            return UnexpectedError();
        }
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(BookResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<BookResponseDto>> GetBookById(int id)
    {
        try
        {
            var book = await _repository.GetBookByIdAsync(id);
            if (book is null)
            {
                return NotFound();
            }

            return Ok(ToResponseDto(book));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve book {BookId}.", id);
            return UnexpectedError();
        }
    }

    [HttpPost]
    [ProducesResponseType(typeof(BookResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<BookResponseDto>> CreateBook([FromBody] BookCreateDto dto)
    {
        try
        {
            var book = ToEntity(dto);
            var id = await _repository.CreateBookAsync(book);
            return CreatedAtAction(nameof(GetBookById), new { id }, ToResponseDto(book));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create book.");
            return UnexpectedError();
        }
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateBook(int id, [FromBody] BookUpdateDto dto)
    {
        try
        {
            if (id != dto.Id)
            {
                ModelState.AddModelError(nameof(BookUpdateDto.Id), "The route id and the body id do not match.");
                return ValidationProblem();
            }

            var existing = await _repository.GetBookByIdAsync(id);
            if (existing is null)
            {
                return NotFound();
            }

            await _repository.UpdateBookAsync(ToEntity(dto));
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update book {BookId}.", id);
            return UnexpectedError();
        }
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteBook(int id)
    {
        try
        {
            var existing = await _repository.GetBookByIdAsync(id);
            if (existing is null)
            {
                return NotFound();
            }

            await _repository.DeleteBookAsync(id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete book {BookId}.", id);
            return UnexpectedError();
        }
    }

    [HttpGet("Genre/{genre}")]
    [ProducesResponseType(typeof(List<BookResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<BookResponseDto>>> GetBooksByGenre(string genre)
    {
        try
        {
            var books = await _repository.GetBooksByGenreAsync(genre);
            return Ok(books.Select(ToResponseDto).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve books for genre {Genre}.", genre);
            return UnexpectedError();
        }
    }

    private ObjectResult UnexpectedError()
        => Problem(
            detail: "An unexpected error occurred while processing the request.",
            statusCode: StatusCodes.Status500InternalServerError);

    private static BookResponseDto ToResponseDto(Book book) => new()
    {
        Id = book.Id,
        Category = book.Category,
        Title = book.Title,
        Author = book.Author,
        Genre = book.Genre,
        Description = book.Description,
        Rating = book.Rating,
        PublishedYear = book.PublishedYear
    };

    private static Book ToEntity(BookCreateDto dto) => new()
    {
        Category = dto.Category,
        Title = dto.Title,
        Author = dto.Author,
        Genre = dto.Genre,
        Description = dto.Description,
        Rating = dto.Rating,
        PublishedYear = dto.PublishedYear
    };

    private static Book ToEntity(BookUpdateDto dto) => new()
    {
        Id = dto.Id,
        Category = dto.Category,
        Title = dto.Title,
        Author = dto.Author,
        Genre = dto.Genre,
        Description = dto.Description,
        Rating = dto.Rating,
        PublishedYear = dto.PublishedYear
    };
}

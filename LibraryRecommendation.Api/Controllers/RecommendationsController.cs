using LibraryRecommendation.Api.Dtos;
using LibraryRecommendation.Api.Repositories;
using LibraryRecommendation.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace LibraryRecommendation.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RecommendationsController : ControllerBase
{
    private const int MinCount = 1;
    private const int MaxCount = 20;

    private readonly IRecommendationService _recommendationService;
    private readonly IBookRepository _repository;
    private readonly ILogger<RecommendationsController> _logger;

    public RecommendationsController(
        IRecommendationService recommendationService,
        IBookRepository repository,
        ILogger<RecommendationsController> logger)
    {
        _recommendationService = recommendationService;
        _repository = repository;
        _logger = logger;
    }

    [HttpGet("similar/{bookId:int}")]
    [ProducesResponseType(typeof(List<RecommendationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<RecommendationDto>>> GetSimilarBooks(int bookId, [FromQuery] int count = 5)
    {
        try
        {
            if (count < MinCount || count > MaxCount)
            {
                ModelState.AddModelError(nameof(count), $"The count must be between {MinCount} and {MaxCount}.");
                return ValidationProblem();
            }

            var book = await _repository.GetBookByIdAsync(bookId);
            if (book is null)
            {
                return NotFound();
            }

            var recommendations = await _recommendationService.GetSimilarBooksAsync(bookId, count);
            return Ok(recommendations.Select(ToDto).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compute recommendations for book {BookId}.", bookId);
            return Problem(
                detail: "An unexpected error occurred while processing the request.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static RecommendationDto ToDto(BookRecommendation recommendation) => new()
    {
        Id = recommendation.Book.Id,
        Title = recommendation.Book.Title,
        Author = recommendation.Book.Author,
        Genre = recommendation.Book.Genre,
        Description = recommendation.Book.Description,
        Rating = recommendation.Book.Rating,
        PublishedYear = recommendation.Book.PublishedYear,
        SimilarityScore = recommendation.SimilarityScore,
        MatchingTerms = recommendation.MatchingTerms.ToArray()
    };
}

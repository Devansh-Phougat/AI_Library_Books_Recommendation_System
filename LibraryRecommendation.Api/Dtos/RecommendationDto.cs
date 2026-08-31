namespace LibraryRecommendation.Api.Dtos;

public class RecommendationDto
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Author { get; set; } = string.Empty;

    public string Genre { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public double Rating { get; set; }

    public int PublishedYear { get; set; }

    public double SimilarityScore { get; set; }

    public string[] MatchingTerms { get; set; } = Array.Empty<string>();
}

using System.ComponentModel.DataAnnotations;

namespace LibraryRecommendation.Api.Models;

public class Book
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(150)]
    public string Author { get; set; } = string.Empty;

    [Required]
    [StringLength(BookCategories.MaxLength)]
    [RegularExpression(BookCategories.AllowedPattern, ErrorMessage = BookCategories.ValidationMessage)]
    public string Category { get; set; } = BookCategories.Fiction;

    [Required]
    [StringLength(100)]
    public string Genre { get; set; } = string.Empty;

    [Required]
    [StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Range(0.0, 5.0)]
    public double Rating { get; set; }

    [Range(1000, 2100)]
    public int PublishedYear { get; set; }
}

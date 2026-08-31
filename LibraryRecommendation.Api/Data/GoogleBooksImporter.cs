using System.Text;
using System.Text.RegularExpressions;
using LibraryRecommendation.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace LibraryRecommendation.Api.Data;

/// <summary>
/// Imports educational titles from the Google Books CSV export. Unlike the fiction importer this
/// one is additive: it removes only the existing Educational rows and leaves Fiction untouched.
/// Invoked from the command line with --import-educational &lt;path&gt;.
/// </summary>
public class GoogleBooksImporter
{
    private const int MinDescriptionLength = 300;
    private const int MaxDescriptionLength = 2000;
    private const int BatchSize = 500;
    private const int MinYear = 1000;
    private const int MaxYear = 2100;

    private const int MaxTitleLength = 200;
    private const int MaxAuthorLength = 150;

    /// <summary>
    /// search_category values worth importing, folded into a smaller set of genres. Every value
    /// not listed here is ignored, including the author_* and year_* buckets, which re-scrape the
    /// same books along a different axis.
    /// </summary>
    private static readonly Dictionary<string, string> GenreBySearchCategory = new(StringComparer.OrdinalIgnoreCase)
    {
        ["c++ programming"] = "Computer Science",
        ["java programming"] = "Computer Science",
        ["javascript"] = "Computer Science",
        ["web development"] = "Computer Science",
        ["mobile development"] = "Computer Science",
        ["technology programming"] = "Computer Science",
        ["cloud computing"] = "Computer Science",
        ["devops"] = "Computer Science",
        ["cybersecurity hacking"] = "Computer Science",

        ["machine learning"] = "Data Science",
        ["machine learning AI"] = "Data Science",
        ["data science"] = "Data Science",
        ["data science analytics"] = "Data Science",
        ["deep learning"] = "Data Science",

        ["mathematics calculus"] = "Mathematics",
        ["statistics probability"] = "Mathematics",

        ["biology genetics"] = "Natural Science",
        ["chemistry organic"] = "Natural Science",
        ["astronomy space"] = "Natural Science",
        ["science"] = "Natural Science",
        ["climate change"] = "Natural Science",

        ["economics policy"] = "Social Science",
        ["political science"] = "Social Science",
        ["sociology"] = "Social Science",
        ["psychology"] = "Social Science",
        ["psychology behavior"] = "Social Science",

        ["philosophy"] = "Philosophy",
        ["philosophy ethics"] = "Philosophy",

        ["american history"] = "History",
        ["ancient history"] = "History",
        ["history"] = "History",
        ["war history"] = "History",
        ["world war 2"] = "History",

        ["medical textbook"] = "Medicine",
        ["nutrition diet"] = "Medicine"
    };

    /// <summary>Phrases that mark wholesale or marketing copy rather than a description of the book.</summary>
    private static readonly string[] SpamMarkers =
    {
        "% discount",
        "your customers",
        "Buy it Now",
        "Purchase ",
        "BUY NOW",
        "stop wasting your money",
        "bookstores !"
    };

    /// <summary>A currency symbol immediately preceding a figure, e.g. "$9.99" or "£ 20".</summary>
    private static readonly Regex CurrencyAmount = new(@"[$£€¥₹]\s*\d", RegexOptions.Compiled);

    private readonly LibraryDbContext _context;
    private readonly ILogger<GoogleBooksImporter> _logger;

    public GoogleBooksImporter(LibraryDbContext context, ILogger<GoogleBooksImporter> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<EducationalImportReport> ImportAsync(string path)
    {
        var report = new EducationalImportReport();

        // Clear Educational first so a re-run does not deduplicate against its own last import,
        // then take the surviving Fiction keys to deduplicate against.
        report.ExistingEducationalRemoved = await _context.Books
            .Where(book => book.Category == BookCategories.Educational)
            .ExecuteDeleteAsync();

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var existing = await _context.Books
            .AsNoTracking()
            .Select(book => new { book.Title, book.Author })
            .ToListAsync();

        foreach (var book in existing)
        {
            seen.Add(DeduplicationKey(book.Title, book.Author));
        }

        report.ExistingTitlesGuardedAgainst = seen.Count;

        var toInsert = ReadBooks(path, seen, report);

        foreach (var batch in toInsert.Chunk(BatchSize))
        {
            _context.Books.AddRange(batch);
            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            report.RowsInserted += batch.Length;
            _logger.LogInformation("Inserted {Inserted} of {Total} educational books.", report.RowsInserted, toInsert.Count);
        }

        report.GenreBreakdown = toInsert
            .GroupBy(book => book.Genre)
            .Select(group => new EducationalGenreCount(group.Key, group.Count()))
            .OrderByDescending(genre => genre.Imported)
            .ThenBy(genre => genre.Name, StringComparer.Ordinal)
            .ToList();

        report.SetDescriptionStats(toInsert.Select(book => book.Description.Length).ToList());
        return report;
    }

    private static List<Book> ReadBooks(string path, HashSet<string> seen, EducationalImportReport report)
    {
        var books = new List<Book>();

        using var reader = new StreamReader(path, Encoding.UTF8);
        using var records = ReadCsvRecords(reader).GetEnumerator();

        if (!records.MoveNext())
        {
            return books;
        }

        var columns = BuildColumnIndex(records.Current);

        while (records.MoveNext())
        {
            var record = records.Current;
            if (record.Length <= 1)
            {
                continue;
            }

            report.RowsRead++;

            var searchCategory = Field(record, columns, "search_category");
            if (!GenreBySearchCategory.TryGetValue(searchCategory, out var genre))
            {
                report.SkippedUnmappedCategory++;
                continue;
            }

            report.RowsConsidered++;

            var title = Field(record, columns, "title");
            if (title.Length == 0)
            {
                report.SkippedMissingTitle++;
                continue;
            }

            // The authors column is a comma-separated list; the first entry is the primary author.
            var author = Field(record, columns, "authors").Split(',')[0].Trim();
            if (author.Length == 0)
            {
                report.SkippedMissingAuthor++;
                continue;
            }

            if (!TryExtractYear(Field(record, columns, "published_date"), out var year))
            {
                report.SkippedUnusableYear++;
                continue;
            }

            var description = Field(record, columns, "description");
            if (description.Length < MinDescriptionLength)
            {
                report.SkippedDescriptionTooShort++;
                continue;
            }

            if (IsSpam(description))
            {
                report.SkippedSpam++;
                continue;
            }

            if (!seen.Add(DeduplicationKey(title, author)))
            {
                report.SkippedDuplicate++;
                continue;
            }

            if (description.Length > MaxDescriptionLength)
            {
                description = TruncateAtWordBoundary(description, MaxDescriptionLength);
                report.DescriptionsTruncated++;
            }

            books.Add(new Book
            {
                Category = BookCategories.Educational,
                Genre = genre,
                Title = Clip(title, MaxTitleLength),
                Author = Clip(author, MaxAuthorLength),
                Description = description,
                Rating = 0,
                PublishedYear = year
            });
        }

        return books;
    }

    private static bool IsSpam(string description)
    {
        if (CurrencyAmount.IsMatch(description))
        {
            return true;
        }

        foreach (var marker in SpamMarkers)
        {
            if (description.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string DeduplicationKey(string title, string author) => $"{title.Trim()}|{author.Trim()}";

    private static bool TryExtractYear(string publishedDate, out int year)
    {
        year = 0;
        var match = Regex.Match(publishedDate, @"\d{4}");
        if (!match.Success || !int.TryParse(match.Value, out year))
        {
            return false;
        }

        return year is >= MinYear and <= MaxYear;
    }

    private static string TruncateAtWordBoundary(string text, int maxLength)
    {
        var slice = text[..maxLength];
        var lastSpace = slice.LastIndexOf(' ');
        return (lastSpace > 0 ? slice[..lastSpace] : slice).TrimEnd();
    }

    private static string Clip(string text, int maxLength)
        => text.Length <= maxLength ? text : text[..maxLength].TrimEnd();

    private static Dictionary<string, int> BuildColumnIndex(string[] header)
    {
        var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < header.Length; i++)
        {
            columns[header[i].Trim()] = i;
        }

        return columns;
    }

    private static string Field(string[] record, Dictionary<string, int> columns, string name)
        => columns.TryGetValue(name, out var index) && index < record.Length
            ? record[index].Trim()
            : string.Empty;

    /// <summary>
    /// Minimal RFC 4180 reader: handles quoted fields, embedded commas and newlines, and ""
    /// escapes. Hand-written because the project takes no third-party dependencies.
    /// </summary>
    private static IEnumerable<string[]> ReadCsvRecords(TextReader reader)
    {
        var field = new StringBuilder();
        var record = new List<string>();
        var inQuotes = false;

        int read;
        while ((read = reader.Read()) != -1)
        {
            var character = (char)read;

            if (inQuotes)
            {
                if (character != '"')
                {
                    field.Append(character);
                }
                else if (reader.Peek() == '"')
                {
                    reader.Read();
                    field.Append('"');
                }
                else
                {
                    inQuotes = false;
                }

                continue;
            }

            switch (character)
            {
                case '"':
                    inQuotes = true;
                    break;
                case ',':
                    record.Add(field.ToString());
                    field.Clear();
                    break;
                case '\r':
                    break;
                case '\n':
                    record.Add(field.ToString());
                    field.Clear();
                    yield return record.ToArray();
                    record.Clear();
                    break;
                default:
                    field.Append(character);
                    break;
            }
        }

        if (field.Length > 0 || record.Count > 0)
        {
            record.Add(field.ToString());
            yield return record.ToArray();
        }
    }
}

public record EducationalGenreCount(string Name, int Imported);

public class EducationalImportReport
{
    public int RowsRead { get; set; }
    public int RowsConsidered { get; set; }
    public int RowsInserted { get; set; }

    public int ExistingEducationalRemoved { get; set; }
    public int ExistingTitlesGuardedAgainst { get; set; }

    public int SkippedUnmappedCategory { get; set; }
    public int SkippedMissingTitle { get; set; }
    public int SkippedMissingAuthor { get; set; }
    public int SkippedUnusableYear { get; set; }
    public int SkippedDescriptionTooShort { get; set; }
    public int SkippedSpam { get; set; }
    public int SkippedDuplicate { get; set; }

    public int DescriptionsTruncated { get; set; }

    public int MinDescriptionLength { get; private set; }
    public int MedianDescriptionLength { get; private set; }
    public int MaxDescriptionLength { get; private set; }

    public List<EducationalGenreCount> GenreBreakdown { get; set; } = new();

    public int SkippedFromConsidered =>
        SkippedMissingTitle + SkippedMissingAuthor + SkippedUnusableYear +
        SkippedDescriptionTooShort + SkippedSpam + SkippedDuplicate;

    public void SetDescriptionStats(List<int> lengths)
    {
        if (lengths.Count == 0)
        {
            return;
        }

        lengths.Sort();
        MinDescriptionLength = lengths[0];
        MaxDescriptionLength = lengths[^1];
        MedianDescriptionLength = lengths.Count % 2 == 1
            ? lengths[lengths.Count / 2]
            : (lengths[lengths.Count / 2 - 1] + lengths[lengths.Count / 2]) / 2;
    }

    public string Format()
    {
        var builder = new StringBuilder();
        builder.AppendLine();
        builder.AppendLine("======== EDUCATIONAL IMPORT REPORT ========");
        builder.AppendLine($"Rows read                    : {RowsRead,6:N0}");
        builder.AppendLine($"Rows in a mapped category    : {RowsConsidered,6:N0}");
        builder.AppendLine($"Rows inserted                : {RowsInserted,6:N0}");
        builder.AppendLine($"Skipped from considered rows : {SkippedFromConsidered,6:N0}");
        builder.AppendLine();
        builder.AppendLine($"Ignored, category not mapped : {SkippedUnmappedCategory,6:N0}");
        builder.AppendLine();
        builder.AppendLine("Skipped by reason (of the considered rows):");
        builder.AppendLine($"  missing title               : {SkippedMissingTitle,6:N0}");
        builder.AppendLine($"  missing author              : {SkippedMissingAuthor,6:N0}");
        builder.AppendLine($"  year missing or out of range: {SkippedUnusableYear,6:N0}");
        builder.AppendLine($"  description under 300 chars : {SkippedDescriptionTooShort,6:N0}");
        builder.AppendLine($"  SPAM / marketing copy       : {SkippedSpam,6:N0}");
        builder.AppendLine($"  duplicate title + author    : {SkippedDuplicate,6:N0}");
        builder.AppendLine();
        builder.AppendLine($"Existing Educational rows removed first : {ExistingEducationalRemoved,6:N0}");
        builder.AppendLine($"Existing titles deduplicated against    : {ExistingTitlesGuardedAgainst,6:N0}");
        builder.AppendLine($"Descriptions truncated to 2000 chars    : {DescriptionsTruncated,6:N0}");
        builder.AppendLine();
        builder.AppendLine("Imported per genre:");
        foreach (var genre in GenreBreakdown)
        {
            builder.AppendLine($"  {genre.Name,-20} {genre.Imported,5:N0}");
        }

        builder.AppendLine();
        builder.AppendLine("Description length (imported books):");
        builder.AppendLine($"  min    : {MinDescriptionLength,6:N0}");
        builder.AppendLine($"  median : {MedianDescriptionLength,6:N0}");
        builder.AppendLine($"  max    : {MaxDescriptionLength,6:N0}");
        builder.AppendLine("===========================================");
        return builder.ToString();
    }
}

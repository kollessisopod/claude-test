namespace OstQuiz.Api.Domain;

/// <summary>
/// A game in the catalog. The pool of games is imported from RAWG and used
/// for the answer autocomplete. Detail fields (publisher/developer) are
/// enriched lazily because the RAWG list endpoint does not include them.
/// </summary>
public class Game
{
    public int Id { get; set; }

    /// <summary>RAWG external id, when the row originated from an import.</summary>
    public int? RawgId { get; set; }

    public required string Name { get; set; }

    public string? Slug { get; set; }

    public List<string> Genres { get; set; } = new();

    public DateOnly? ReleaseDate { get; set; }

    public int? MetacriticScore { get; set; }

    public string? Publisher { get; set; }

    public string? Developer { get; set; }

    /// <summary>
    /// Franchise/series grouping (e.g. "The Legend of Zelda"). Two games are in the same
    /// franchise when both have the same non-null value. Used to flag "close" wrong guesses.
    /// </summary>
    public string? Franchise { get; set; }

    /// <summary>Object key of the cover/album image in the covers bucket (optional).</summary>
    public string? CoverImageKey { get; set; }

    /// <summary>When publisher/developer were last enriched from the RAWG detail endpoint.</summary>
    public DateTimeOffset? EnrichedAt { get; set; }

    public ICollection<Puzzle> Puzzles { get; set; } = new List<Puzzle>();
}

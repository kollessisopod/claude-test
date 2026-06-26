namespace OstQuiz.Api.Domain;

/// <summary>
/// A single daily puzzle: one audio clip whose source game the player must guess.
/// Exactly one puzzle is active per calendar day (<see cref="PuzzleDate"/> is unique).
/// </summary>
public class Puzzle
{
    public int Id { get; set; }

    /// <summary>The day this puzzle is the active "today" puzzle. Unique.</summary>
    public DateOnly PuzzleDate { get; set; }

    public int GameId { get; set; }
    public Game Game { get; set; } = null!;

    /// <summary>Object key of the full-length audio in the audio bucket (mandatory).</summary>
    public required string AudioKey { get; set; }

    /// <summary>Object key of the album/OST cover in the covers bucket (optional).</summary>
    public string? AlbumCoverKey { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Pre-generated trimmed audio clips, one per guess step where available.</summary>
    public ICollection<PuzzleClip> Clips { get; set; } = new List<PuzzleClip>();
}

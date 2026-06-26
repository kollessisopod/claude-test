namespace OstQuiz.Api.Domain;

/// <summary>
/// The categories of information revealed to the player, in reveal order.
/// A new hint is unlocked on each failed/skipped guess after the first
/// (i.e. step 1 reveals Genre, step 2 ReleaseDate, ... step 5 Developer).
/// </summary>
public enum HintKind
{
    Genre = 0,
    ReleaseDate = 1,
    Metacritic = 2,
    Publisher = 3,
    Developer = 4,
}

public static class GameRules
{
    /// <summary>Total number of guesses a player is allowed.</summary>
    public const int TotalGuesses = 6;

    /// <summary>
    /// Allowed playback length (seconds) for each step 0..5.
    /// A null value means "the full audio".
    /// </summary>
    public static readonly int?[] DurationSteps = { 1, 2, 3, 5, 10, null };

    /// <summary>
    /// Order in which hints are revealed. Index N corresponds to the hint
    /// unlocked at guess step N (step 0 reveals nothing beyond the audio).
    /// </summary>
    public static readonly HintKind[] HintOrder =
    {
        HintKind.Genre,
        HintKind.ReleaseDate,
        HintKind.Metacritic,
        HintKind.Publisher,
        HintKind.Developer,
    };

    public static readonly string[] HintOrderNames =
    {
        "genre", "releaseDate", "metacritic", "publisher", "developer",
    };
}

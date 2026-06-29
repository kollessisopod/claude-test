using Microsoft.EntityFrameworkCore;
using OstQuiz.Api.Contracts;
using OstQuiz.Api.Data;
using OstQuiz.Api.Domain;
using OstQuiz.Api.Services.Rawg;
using OstQuiz.Api.Services.Security;

namespace OstQuiz.Api.Features.Puzzles;

/// <summary>
/// Business logic for the daily puzzle: resolving today's puzzle, building the
/// step-gated hints, validating guesses, and picking the right audio clip.
/// Player state is intentionally NOT stored server-side (anonymous/Wordle-style);
/// the current step travels in a signed progression token instead of being trusted
/// from the client.
/// </summary>
public class PuzzleService(AppDbContext db, RawgImportService enricher, StepTokenService tokens)
{
    public static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>
    /// The puzzle for the given day (defaults to UTC today), or null if none scheduled.
    /// Future-dated puzzles are not returned — they only become playable on their day.
    /// </summary>
    public async Task<Puzzle?> GetForDateAsync(DateOnly? date, CancellationToken ct = default)
    {
        var day = date ?? Today;
        if (day > Today) return null;
        return await db.Puzzles
            .Include(p => p.Game)
            .Include(p => p.Clips)
            .FirstOrDefaultAsync(p => p.PuzzleDate == day, ct);
    }

    /// <summary>
    /// A page of playable dates (today or earlier), newest first. Paginated so the archive
    /// scales to thousands of days without pulling them all at once.
    /// </summary>
    public async Task<ArchivePage> GetArchiveAsync(int skip, int take, CancellationToken ct = default)
    {
        var today = Today;
        skip = Math.Max(0, skip);
        take = Math.Clamp(take, 1, 100);

        var baseQuery = db.Puzzles.Where(p => p.PuzzleDate <= today);
        var total = await baseQuery.CountAsync(ct);
        var dates = await baseQuery
            .OrderByDescending(p => p.PuzzleDate)
            .Skip(skip)
            .Take(take)
            .Select(p => p.PuzzleDate)
            .ToListAsync(ct);

        var items = dates.Select(d => new PuzzleDateInfo(d, d == today)).ToList();
        return new ArchivePage(items, total);
    }

    /// <summary>Issues a fresh progression token for the given step of this puzzle.</summary>
    public string IssueToken(Puzzle p, int step) => tokens.Issue(p.PuzzleDate, step);

    /// <summary>
    /// Validates a token and returns the unlocked step, ensuring it belongs to this puzzle's day.
    /// Returns -1 when the token is missing, forged, expired (wrong day), or out of range.
    /// </summary>
    public int UnlockedStep(Puzzle p, string? token)
    {
        if (!tokens.TryValidate(token, out var date, out var step)) return -1;
        if (date != p.PuzzleDate) return -1;
        if (step < 0 || step >= GameRules.TotalGuesses) return -1;
        return step;
    }

    public TodayPuzzleResponse ToPuzzleResponse(Puzzle p) => new(
        p.Id,
        p.PuzzleDate,
        GameRules.TotalGuesses,
        GameRules.DurationSteps,
        GameRules.HintOrderNames,
        IssueToken(p, 0));

    /// <summary>Resolves the object key of the audio clip to serve for a step, falling back to full audio.</summary>
    public static string ClipKeyForStep(Puzzle p, int step)
    {
        var clip = p.Clips.FirstOrDefault(c => c.Step == step);
        return clip?.ObjectKey ?? p.AudioKey;
    }

    /// <summary>Builds the hint unlocked at <paramref name="step"/> (1..5). Enriches detail lazily.</summary>
    public async Task<HintResponse?> GetHintAsync(Puzzle p, int step, CancellationToken ct = default)
    {
        if (step < 1 || step > GameRules.HintOrder.Length) return null;
        var kind = GameRules.HintOrder[step - 1];

        // Publisher/Developer may need a RAWG detail lookup the first time.
        if (kind is HintKind.Publisher or HintKind.Developer)
            await enricher.EnrichDetailAsync(p.Game, ct);

        var g = p.Game;
        var value = kind switch
        {
            HintKind.Genre => g.Genres.Count > 0 ? string.Join(", ", g.Genres) : "Unknown",
            HintKind.ReleaseDate => g.ReleaseDate?.ToString("yyyy-MM-dd") ?? "Unknown",
            HintKind.Metacritic => g.MetacriticScore?.ToString() ?? "N/A",
            HintKind.Publisher => string.IsNullOrWhiteSpace(g.Publisher) ? "Unknown" : g.Publisher!,
            HintKind.Developer => string.IsNullOrWhiteSpace(g.Developer) ? "Unknown" : g.Developer!,
            _ => "Unknown",
        };

        return new HintResponse(step, GameRules.HintOrderNames[step - 1], value);
    }

    public static AnswerDto ToAnswer(Puzzle p)
    {
        var g = p.Game;
        var gameCover = g.CoverImageKey is null ? null : $"/api/games/{g.Id}/cover";
        var albumCover = p.AlbumCoverKey is null ? null : $"/api/puzzles/{p.PuzzleDate:yyyy-MM-dd}/album-cover";
        return new AnswerDto(
            g.Id, g.Name, g.Genres.ToArray(), g.ReleaseDate, g.MetacriticScore,
            g.Publisher, g.Developer, gameCover, albumCover);
    }

    /// <summary>
    /// Validates a guess made at the token-derived <paramref name="step"/> and returns the
    /// player-facing result. On an incorrect non-final guess, mints the next-step token and
    /// reveals the newly unlocked hint.
    /// </summary>
    public async Task<GuessResponse> EvaluateGuessAsync(Puzzle p, int step, int guessGameId, CancellationToken ct = default)
    {
        var correct = guessGameId == p.GameId;
        var lastStep = GameRules.TotalGuesses - 1; // steps are 0-based: 0..5
        var gameOver = correct || step >= lastStep;

        // Resolve the guessed game (null for a skip) for the name + franchise indicator.
        var guessed = guessGameId <= 0
            ? null
            : await db.Games.AsNoTracking().FirstOrDefaultAsync(g => g.Id == guessGameId, ct);
        var franchiseMatch = !correct
            && guessed?.Franchise is { } gf && !string.IsNullOrWhiteSpace(gf)
            && string.Equals(gf, p.Game.Franchise, StringComparison.OrdinalIgnoreCase);

        HintResponse? revealed = null;
        string? nextToken = null;
        if (!correct && step < lastStep)
        {
            var nextStep = step + 1;
            revealed = await GetHintAsync(p, nextStep, ct);
            nextToken = IssueToken(p, nextStep);
        }

        var answer = gameOver ? ToAnswer(p) : null;
        // On game over, hand out a token unlocking the full audio so the player hears the whole track.
        var fullAudioToken = gameOver ? IssueToken(p, lastStep) : null;
        return new GuessResponse(correct, gameOver, revealed, answer, nextToken, guessed?.Name, franchiseMatch, fullAudioToken);
    }
}

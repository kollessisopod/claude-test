using OstQuiz.Api.Data;
using OstQuiz.Api.Domain;
using OstQuiz.Api.Services.Storage;

namespace OstQuiz.Api.Services.Audio;

/// <summary>
/// Generates the per-step trimmed clips for a puzzle, stores them in object storage,
/// and records <see cref="PuzzleClip"/> rows. The puzzle must already be persisted
/// (its <c>Id</c> is required). Replaces any existing clips for the puzzle.
/// </summary>
public class PuzzleClipService(AppDbContext db, IObjectStorage storage, IAudioClipper clipper, ILogger<PuzzleClipService> log)
{
    public async Task<int> GenerateForPuzzleAsync(
        Puzzle puzzle, byte[] fullAudio, string extension, string contentType, CancellationToken ct = default)
    {
        var clips = await clipper.GenerateAsync(fullAudio, extension, contentType, ct);
        if (clips.Count == 0) return 0;

        // Drop any previously generated clips for this puzzle before re-adding.
        var existing = db.PuzzleClips.Where(c => c.PuzzleId == puzzle.Id);
        db.PuzzleClips.RemoveRange(existing);

        foreach (var clip in clips)
        {
            var key = $"puzzles/{puzzle.PuzzleDate:yyyy-MM-dd}/clip{clip.Step}-{clip.DurationSeconds}s{extension}";
            using var ms = new MemoryStream(clip.Data);
            await storage.PutAudioAsync(key, ms, clip.ContentType, ct);
            db.PuzzleClips.Add(new PuzzleClip
            {
                PuzzleId = puzzle.Id,
                Step = clip.Step,
                DurationSeconds = clip.DurationSeconds,
                ObjectKey = key,
            });
        }

        await db.SaveChangesAsync(ct);
        log.LogInformation("Generated {Count} clips for puzzle {Date}", clips.Count, puzzle.PuzzleDate);
        return clips.Count;
    }
}

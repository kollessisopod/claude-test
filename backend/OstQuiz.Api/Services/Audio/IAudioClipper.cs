namespace OstQuiz.Api.Services.Audio;

/// <summary>A trimmed clip produced for a given guess step.</summary>
public record GeneratedClip(int Step, int DurationSeconds, byte[] Data, string ContentType);

public interface IAudioClipper
{
    /// <summary>True when the trimming backend (ffmpeg) is usable in this environment.</summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Produces one trimmed clip per finite duration step (steps 0..4 → 1,2,3,5,10s).
    /// The final step (full audio) is not produced; callers fall back to the full object.
    /// Returns an empty list if trimming is unavailable or fails.
    /// </summary>
    Task<IReadOnlyList<GeneratedClip>> GenerateAsync(byte[] fullAudio, string extension, string contentType, CancellationToken ct = default);
}

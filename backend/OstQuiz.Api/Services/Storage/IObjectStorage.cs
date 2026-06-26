namespace OstQuiz.Api.Services.Storage;

public record ObjectStream(Stream Content, string ContentType, long? ContentLength);

public interface IObjectStorage
{
    /// <summary>Ensures the audio and cover buckets exist. Safe to call repeatedly.</summary>
    Task EnsureBucketsAsync(CancellationToken ct = default);

    Task PutAudioAsync(string key, Stream content, string contentType, CancellationToken ct = default);
    Task PutCoverAsync(string key, Stream content, string contentType, CancellationToken ct = default);

    /// <summary>Opens the audio object for streaming back to the client.</summary>
    Task<ObjectStream> GetAudioAsync(string key, CancellationToken ct = default);

    Task<bool> AudioExistsAsync(string key, CancellationToken ct = default);

    /// <summary>Opens a cover image for streaming back to the client.</summary>
    Task<ObjectStream> GetCoverAsync(string key, CancellationToken ct = default);

    Task<bool> CoverExistsAsync(string key, CancellationToken ct = default);
}

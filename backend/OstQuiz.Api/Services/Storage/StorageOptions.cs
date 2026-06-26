namespace OstQuiz.Api.Services.Storage;

public class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>S3-compatible endpoint, e.g. http://minio:9000</summary>
    public string ServiceUrl { get; set; } = "http://minio:9000";
    public string AccessKey { get; set; } = "minioadmin";
    public string SecretKey { get; set; } = "minioadmin";
    public string AudioBucket { get; set; } = "audio";
    public string CoverBucket { get; set; } = "covers";

    /// <summary>MinIO requires path-style addressing (bucket in the path, not the host).</summary>
    public bool ForcePathStyle { get; set; } = true;
}

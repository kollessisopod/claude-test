namespace OstQuiz.Api.Services.Security;

public class StepTokenOptions
{
    public const string SectionName = "Tokens";

    /// <summary>HMAC signing secret for progression tokens. MUST be overridden in production.</summary>
    public string Secret { get; set; } = "dev-insecure-token-secret-change-me";
}

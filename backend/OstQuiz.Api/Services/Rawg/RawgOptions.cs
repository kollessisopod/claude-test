namespace OstQuiz.Api.Services.Rawg;

public class RawgOptions
{
    public const string SectionName = "Rawg";

    public string ApiKey { get; set; } = "";
    public string BaseUrl { get; set; } = "https://api.rawg.io/api";

    /// <summary>Default number of games pulled into the autocomplete pool on import.</summary>
    public int DefaultImportCount = 200;
}

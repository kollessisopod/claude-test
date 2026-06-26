using System.Text.Json.Serialization;

namespace OstQuiz.Api.Services.Rawg;

// Minimal projections of the RAWG API responses we consume.

public class RawgListResponse
{
    [JsonPropertyName("results")]
    public List<RawgGame> Results { get; set; } = new();
}

public class RawgGame
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("slug")] public string? Slug { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("released")] public string? Released { get; set; }
    [JsonPropertyName("metacritic")] public int? Metacritic { get; set; }
    [JsonPropertyName("background_image")] public string? BackgroundImage { get; set; }
    [JsonPropertyName("genres")] public List<RawgNamed> Genres { get; set; } = new();

    // Present on the detail endpoint only.
    [JsonPropertyName("publishers")] public List<RawgNamed> Publishers { get; set; } = new();
    [JsonPropertyName("developers")] public List<RawgNamed> Developers { get; set; } = new();
}

public class RawgNamed
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
}

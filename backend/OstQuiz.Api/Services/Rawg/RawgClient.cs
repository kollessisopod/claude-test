using System.Globalization;
using Microsoft.Extensions.Options;

namespace OstQuiz.Api.Services.Rawg;

/// <summary>Thin typed client over the RAWG HTTP API.</summary>
public class RawgClient
{
    private readonly HttpClient _http;
    private readonly RawgOptions _opts;

    public RawgClient(HttpClient http, IOptions<RawgOptions> opts)
    {
        _http = http;
        _opts = opts.Value;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_opts.ApiKey);

    /// <summary>Pulls up to <paramref name="count"/> games ordered by metacritic (desc).</summary>
    public async Task<List<RawgGame>> GetTopGamesAsync(int count, CancellationToken ct = default)
    {
        var results = new List<RawgGame>();
        const int pageSize = 40;
        var page = 1;
        while (results.Count < count)
        {
            var url = $"{_opts.BaseUrl}/games?key={_opts.ApiKey}&ordering=-metacritic&page_size={pageSize}&page={page}";
            var resp = await _http.GetFromJsonAsync<RawgListResponse>(url, ct);
            if (resp is null || resp.Results.Count == 0) break;
            results.AddRange(resp.Results);
            page++;
        }
        return results.Take(count).ToList();
    }

    /// <summary>Fetches publisher/developer detail for a single game.</summary>
    public Task<RawgGame?> GetGameDetailAsync(int rawgId, CancellationToken ct = default)
        => _http.GetFromJsonAsync<RawgGame>($"{_opts.BaseUrl}/games/{rawgId}?key={_opts.ApiKey}", ct);

    public static DateOnly? ParseReleased(string? released)
        => DateOnly.TryParse(released, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
}

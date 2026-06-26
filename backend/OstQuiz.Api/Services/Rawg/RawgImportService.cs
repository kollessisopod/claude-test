using Microsoft.EntityFrameworkCore;
using OstQuiz.Api.Data;
using OstQuiz.Api.Domain;

namespace OstQuiz.Api.Services.Rawg;

/// <summary>
/// Imports a pool of games from RAWG into the catalog and enriches individual
/// games with publisher/developer detail on demand.
/// </summary>
public class RawgImportService(AppDbContext db, RawgClient rawg, ILogger<RawgImportService> log)
{
    /// <summary>Upserts the top-N games by metacritic into the catalog. Returns count imported/updated.</summary>
    public async Task<int> ImportTopGamesAsync(int count, CancellationToken ct = default)
    {
        var games = await rawg.GetTopGamesAsync(count, ct);
        var byRawgId = await db.Games
            .Where(g => g.RawgId != null)
            .ToDictionaryAsync(g => g.RawgId!.Value, ct);

        var affected = 0;
        foreach (var r in games)
        {
            if (!byRawgId.TryGetValue(r.Id, out var game))
            {
                game = new Game { Name = r.Name, RawgId = r.Id };
                db.Games.Add(game);
                byRawgId[r.Id] = game;
            }

            game.Name = r.Name;
            game.Slug = r.Slug;
            game.Genres = r.Genres.Select(g => g.Name).ToList();
            game.ReleaseDate = RawgClient.ParseReleased(r.Released);
            game.MetacriticScore = r.Metacritic;
            // CoverImageKey stays null here; covers are uploaded via the admin app.
            affected++;
        }

        await db.SaveChangesAsync(ct);
        log.LogInformation("RAWG import upserted {Count} games", affected);
        return affected;
    }

    /// <summary>Fills publisher/developer for a game from the RAWG detail endpoint if not already set.</summary>
    public async Task EnrichDetailAsync(Game game, CancellationToken ct = default)
    {
        if (game.RawgId is null || !rawg.IsConfigured) return;
        if (game.EnrichedAt is not null) return;

        var detail = await rawg.GetGameDetailAsync(game.RawgId.Value, ct);
        if (detail is null) return;

        game.Publisher = detail.Publishers.FirstOrDefault()?.Name ?? game.Publisher;
        game.Developer = detail.Developers.FirstOrDefault()?.Name ?? game.Developer;
        game.EnrichedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}

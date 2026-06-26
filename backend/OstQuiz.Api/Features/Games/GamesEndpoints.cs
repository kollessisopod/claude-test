using Microsoft.EntityFrameworkCore;
using OstQuiz.Api.Contracts;
using OstQuiz.Api.Data;
using OstQuiz.Api.Services.Storage;

namespace OstQuiz.Api.Features.Games;

public static class GamesEndpoints
{
    public static void MapGameEndpoints(this IEndpointRouteBuilder app)
    {
        var grp = app.MapGroup("/api/games").WithTags("Games");

        // Autocomplete pool for the guess box. Returns id + name only.
        grp.MapGet("", async (string? search, int? limit, AppDbContext db, CancellationToken ct) =>
        {
            var take = Math.Clamp(limit ?? 20, 1, 50);
            var q = db.Games.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(search))
                q = q.Where(g => EF.Functions.ILike(g.Name, $"%{search}%"));

            var results = await q.OrderBy(g => g.Name)
                .Take(take)
                .Select(g => new GameOption(g.Id, g.Name))
                .ToListAsync(ct);

            return Results.Ok(results);
        });

        // The game's cover image (shown at game end). 404 when none exists.
        grp.MapGet("/{id:int}/cover", async (int id, AppDbContext db, IObjectStorage storage, CancellationToken ct) =>
        {
            var key = await db.Games.Where(g => g.Id == id).Select(g => g.CoverImageKey).FirstOrDefaultAsync(ct);
            if (key is null || !await storage.CoverExistsAsync(key, ct)) return Results.NotFound();

            var obj = await storage.GetCoverAsync(key, ct);
            return Results.Stream(obj.Content, obj.ContentType);
        });
    }
}

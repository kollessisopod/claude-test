using Microsoft.EntityFrameworkCore;
using OstQuiz.Api.Contracts;
using OstQuiz.Api.Data;
using OstQuiz.Api.Domain;
using OstQuiz.Api.Services.Audio;
using OstQuiz.Api.Services.Rawg;
using OstQuiz.Api.Services.Storage;

namespace OstQuiz.Api.Features.Admin;

public static class AdminEndpoints
{
    public const string AdminKeyHeader = "X-Admin-Key";

    public static void MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var grp = app.MapGroup("/api/admin").WithTags("Admin").AddEndpointFilter(AdminKeyFilter);

        // Pull/refresh the autocomplete pool from RAWG.
        grp.MapPost("/games/import", async (ImportRequest? req, RawgClient rawg, RawgImportService import,
            RawgOptions opts, CancellationToken ct) =>
        {
            if (!rawg.IsConfigured)
                return Results.Problem("RAWG API key is not configured (set Rawg__ApiKey).", statusCode: 503);

            var count = req?.Count ?? opts.DefaultImportCount;
            var n = await import.ImportTopGamesAsync(count, ct);
            return Results.Ok(new ImportResponse(n));
        });

        // Create a puzzle for a date: requires an audio file; cover is optional and uploaded separately.
        grp.MapPost("/puzzles", async (HttpRequest http, AppDbContext db, IObjectStorage storage,
            PuzzleClipService clipSvc, CancellationToken ct) =>
        {
            if (!http.HasFormContentType)
                return Results.BadRequest(new { message = "multipart/form-data required" });

            var form = await http.ReadFormAsync(ct);
            var audio = form.Files.GetFile("audio");
            if (audio is null || audio.Length == 0)
                return Results.BadRequest(new { message = "audio file is mandatory" });

            if (!DateOnly.TryParse(form["date"], out var date))
                return Results.BadRequest(new { message = "valid 'date' (yyyy-MM-dd) is required" });

            // Resolve the answer game by internal id or RAWG id.
            Game? game = null;
            if (int.TryParse(form["gameId"], out var gameId))
                game = await db.Games.FindAsync([gameId], ct);
            else if (int.TryParse(form["rawgId"], out var rawgId))
                game = await db.Games.FirstOrDefaultAsync(g => g.RawgId == rawgId, ct);

            if (game is null)
                return Results.BadRequest(new { message = "game not found; provide a valid gameId or rawgId" });

            if (await db.Puzzles.AnyAsync(p => p.PuzzleDate == date, ct))
                return Results.Conflict(new { message = $"a puzzle already exists for {date:yyyy-MM-dd}" });

            var ext = string.IsNullOrWhiteSpace(Path.GetExtension(audio.FileName))
                ? ".mp3" : Path.GetExtension(audio.FileName);
            var contentType = audio.ContentType ?? "audio/mpeg";

            // Read the upload once: used for both the full object and clip generation.
            byte[] bytes;
            await using (var s = audio.OpenReadStream())
            {
                using var ms = new MemoryStream();
                await s.CopyToAsync(ms, ct);
                bytes = ms.ToArray();
            }

            var audioKey = $"puzzles/{date:yyyy-MM-dd}/full{ext}";
            using (var ms = new MemoryStream(bytes))
                await storage.PutAudioAsync(audioKey, ms, contentType, ct);

            // Optional covers. Game cover is shared per game; album cover is per puzzle.
            var gameCover = form.Files.GetFile("gameCover");
            if (gameCover is { Length: > 0 })
            {
                var key = $"games/{game.Id}/cover{Path.GetExtension(gameCover.FileName)}";
                await using var s = gameCover.OpenReadStream();
                await storage.PutCoverAsync(key, s, gameCover.ContentType ?? "image/jpeg", ct);
                game.CoverImageKey = key;
            }

            string? albumCoverKey = null;
            var albumCover = form.Files.GetFile("albumCover");
            if (albumCover is { Length: > 0 })
            {
                albumCoverKey = $"puzzles/{date:yyyy-MM-dd}/album{Path.GetExtension(albumCover.FileName)}";
                await using var s = albumCover.OpenReadStream();
                await storage.PutCoverAsync(albumCoverKey, s, albumCover.ContentType ?? "image/jpeg", ct);
            }

            var puzzle = new Puzzle
            {
                PuzzleDate = date,
                GameId = game.Id,
                AudioKey = audioKey,
                AlbumCoverKey = albumCoverKey,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            db.Puzzles.Add(puzzle);
            await db.SaveChangesAsync(ct);

            // Generate the per-step trimmed clips (ffmpeg). Falls back to full audio if unavailable.
            var clipCount = await clipSvc.GenerateForPuzzleAsync(puzzle, bytes, ext, contentType, ct);

            return Results.Ok(new CreatePuzzleResponse(puzzle.Id, date, audioKey, clipCount));
        }).DisableAntiforgery();
    }

    private static async ValueTask<object?> AdminKeyFilter(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        var cfg = ctx.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var expected = cfg["Admin:Key"];
        var provided = ctx.HttpContext.Request.Headers[AdminKeyHeader].ToString();
        if (string.IsNullOrEmpty(expected) || provided != expected)
            return Results.Unauthorized();
        return await next(ctx);
    }
}

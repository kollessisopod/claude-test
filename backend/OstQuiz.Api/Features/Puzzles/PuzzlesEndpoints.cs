using OstQuiz.Api.Domain;
using OstQuiz.Api.Services.Storage;

namespace OstQuiz.Api.Features.Puzzles;

public static class PuzzlesEndpoints
{
    public static void MapPuzzleEndpoints(this IEndpointRouteBuilder app)
    {
        var grp = app.MapGroup("/api/puzzles").WithTags("Puzzles");

        // Archive: a page of playable dates (today or earlier), newest first.
        grp.MapGet("", async (int? skip, int? take, PuzzleService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetArchiveAsync(skip ?? 0, take ?? 20, ct)));

        // A specific day's puzzle metadata + an initial progression token. No answer/hints leaked.
        grp.MapGet("/{date}", async (DateOnly date, PuzzleService svc, CancellationToken ct) =>
        {
            var p = await svc.GetForDateAsync(date, ct);
            return p is null
                ? Results.NotFound(new { message = "No puzzle for that date." })
                : Results.Ok(svc.ToPuzzleResponse(p));
        });

        // Streams the audio clip for the requested step — only if the token authorizes that step.
        grp.MapGet("/{date}/audio", async (DateOnly date, int step, string? token,
            PuzzleService svc, IObjectStorage storage, CancellationToken ct) =>
        {
            var p = await svc.GetForDateAsync(date, ct);
            if (p is null) return Results.NotFound();

            var unlocked = svc.UnlockedStep(p, token);
            if (unlocked < 0) return Results.Json(new { message = "invalid progression token" }, statusCode: 401);
            if (step < 0 || step >= GameRules.TotalGuesses) return Results.BadRequest(new { message = "step out of range" });
            if (step > unlocked) return Results.Json(new { message = "step not unlocked" }, statusCode: 403);

            var key = PuzzleService.ClipKeyForStep(p, step);
            if (!await storage.AudioExistsAsync(key, ct)) return Results.NotFound(new { message = "audio missing" });

            var obj = await storage.GetAudioAsync(key, ct);
            return Results.Stream(obj.Content, obj.ContentType);
        });

        // The hint unlocked at the requested step (1..5) — only if the token authorizes that step.
        grp.MapGet("/{date}/hint", async (DateOnly date, int step, string? token, PuzzleService svc, CancellationToken ct) =>
        {
            var p = await svc.GetForDateAsync(date, ct);
            if (p is null) return Results.NotFound();

            var unlocked = svc.UnlockedStep(p, token);
            if (unlocked < 0) return Results.Json(new { message = "invalid progression token" }, statusCode: 401);
            if (step > unlocked) return Results.Json(new { message = "step not unlocked" }, statusCode: 403);

            var hint = await svc.GetHintAsync(p, step, ct);
            return hint is null ? Results.BadRequest(new { message = "step out of range" }) : Results.Ok(hint);
        });

        // Submit a guess. The step is derived from the signed token, never trusted from the client.
        grp.MapPost("/{date}/guess", async (DateOnly date, Contracts.GuessRequest req, PuzzleService svc, CancellationToken ct) =>
        {
            var p = await svc.GetForDateAsync(date, ct);
            if (p is null) return Results.NotFound();

            var step = svc.UnlockedStep(p, req.Token);
            if (step < 0) return Results.Json(new { message = "invalid progression token" }, statusCode: 401);

            return Results.Ok(await svc.EvaluateGuessAsync(p, step, req.GuessGameId, ct));
        });

        // The album/OST cover for a puzzle (shown at game end). 404 when none was uploaded.
        grp.MapGet("/{date}/album-cover", async (DateOnly date, PuzzleService svc, IObjectStorage storage, CancellationToken ct) =>
        {
            var p = await svc.GetForDateAsync(date, ct);
            if (p?.AlbumCoverKey is null || !await storage.CoverExistsAsync(p.AlbumCoverKey, ct))
                return Results.NotFound();

            var obj = await storage.GetCoverAsync(p.AlbumCoverKey, ct);
            return Results.Stream(obj.Content, obj.ContentType);
        });
    }
}

using Microsoft.EntityFrameworkCore;
using OstQuiz.Api.Domain;
using OstQuiz.Api.Services.Audio;
using OstQuiz.Api.Services.Storage;

namespace OstQuiz.Api.Data;

/// <summary>
/// Development seed: ensures buckets exist, inserts a small catalog of games with
/// full metadata (so hints work without a RAWG key), and creates one playable
/// puzzle for "today" backed by a synthesized audio clip in MinIO.
/// </summary>
public static class DbSeeder
{
    private static readonly (string Name, string[] Genres, string Released, int Meta, string Pub, string Dev, string? Franchise)[] SeedGames =
    {
        ("The Legend of Zelda: Breath of the Wild", new[] { "Action", "Adventure" }, "2017-03-03", 97, "Nintendo", "Nintendo EPD", "The Legend of Zelda"),
        ("The Legend of Zelda: Tears of the Kingdom", new[] { "Action", "Adventure" }, "2023-05-12", 96, "Nintendo", "Nintendo EPD", "The Legend of Zelda"),
        ("Hollow Knight", new[] { "Action", "Metroidvania" }, "2017-02-24", 90, "Team Cherry", "Team Cherry", null),
        ("The Witcher 3: Wild Hunt", new[] { "RPG" }, "2015-05-18", 93, "CD Projekt", "CD Projekt Red", "The Witcher"),
        ("Undertale", new[] { "RPG", "Indie" }, "2015-09-15", 92, "tobyfox", "tobyfox", null),
        ("DOOM", new[] { "Shooter" }, "2016-05-13", 85, "Bethesda Softworks", "id Software", "DOOM"),
        ("DOOM Eternal", new[] { "Shooter" }, "2020-03-20", 88, "Bethesda Softworks", "id Software", "DOOM"),
        ("Celeste", new[] { "Platformer", "Indie" }, "2018-01-25", 88, "Maddy Makes Games", "Maddy Makes Games", null),
        ("Dark Souls", new[] { "RPG", "Action" }, "2011-09-22", 89, "Bandai Namco", "FromSoftware", "Dark Souls"),
        ("Dark Souls III", new[] { "RPG", "Action" }, "2016-04-12", 89, "Bandai Namco", "FromSoftware", "Dark Souls"),
        ("Stardew Valley", new[] { "Simulation", "Indie" }, "2016-02-26", 89, "ConcernedApe", "ConcernedApe", null),
    };

    public static async Task SeedAsync(AppDbContext db, IObjectStorage storage, PuzzleClipService clipSvc, ILogger logger, CancellationToken ct = default)
    {
        await storage.EnsureBucketsAsync(ct);

        if (!await db.Games.AnyAsync(ct))
        {
            foreach (var s in SeedGames)
            {
                db.Games.Add(new Game
                {
                    Name = s.Name,
                    Genres = s.Genres.ToList(),
                    ReleaseDate = DateOnly.Parse(s.Released),
                    MetacriticScore = s.Meta,
                    Publisher = s.Pub,
                    Developer = s.Dev,
                    Franchise = s.Franchise,
                    EnrichedAt = DateTimeOffset.UtcNow,
                });
            }
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Seeded {Count} games", SeedGames.Length);
        }

        // Seed the last 5 days so there's an archive to browse and replay.
        // Today's answer (DOOM) has a franchise sibling (DOOM Eternal) to demo the franchise indicator.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        (int Offset, string Game, double Freq)[] plan =
        {
            (0, "DOOM", 440),
            (1, "The Witcher 3: Wild Hunt", 392),
            (2, "Hollow Knight", 523),
            (3, "Celeste", 349),
            (4, "Stardew Valley", 294),
        };

        foreach (var item in plan)
        {
            var date = today.AddDays(-item.Offset);
            if (await db.Puzzles.AnyAsync(p => p.PuzzleDate == date, ct)) continue;

            var answer = await db.Games.FirstAsync(g => g.Name == item.Game, ct);
            var audioKey = $"puzzles/{date:yyyy-MM-dd}/full.wav";
            var wav = SampleAudio.GenerateTone(durationSeconds: 12, frequencyHz: item.Freq);
            using (var ms = new MemoryStream(wav))
                await storage.PutAudioAsync(audioKey, ms, "audio/wav", ct);

            var puzzle = new Puzzle
            {
                PuzzleDate = date,
                GameId = answer.Id,
                AudioKey = audioKey,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            db.Puzzles.Add(puzzle);
            await db.SaveChangesAsync(ct);

            // Generate per-step clips (no-op if ffmpeg is unavailable).
            await clipSvc.GenerateForPuzzleAsync(puzzle, wav, ".wav", "audio/wav", ct);
            logger.LogInformation("Seeded puzzle ({Date}) -> {Game}", date, answer.Name);
        }
    }
}

/// <summary>Generates a tiny PCM WAV in-memory so the seed has a playable clip with no external assets.</summary>
internal static class SampleAudio
{
    public static byte[] GenerateTone(int durationSeconds, double frequencyHz, int sampleRate = 44100)
    {
        var totalSamples = durationSeconds * sampleRate;
        const short bitsPerSample = 16;
        const short channels = 1;
        var dataBytes = totalSamples * channels * (bitsPerSample / 8);

        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);

        // RIFF header
        w.Write("RIFF"u8.ToArray());
        w.Write(36 + dataBytes);
        w.Write("WAVE"u8.ToArray());
        // fmt chunk
        w.Write("fmt "u8.ToArray());
        w.Write(16);
        w.Write((short)1); // PCM
        w.Write(channels);
        w.Write(sampleRate);
        w.Write(sampleRate * channels * (bitsPerSample / 8)); // byte rate
        w.Write((short)(channels * (bitsPerSample / 8)));      // block align
        w.Write(bitsPerSample);
        // data chunk
        w.Write("data"u8.ToArray());
        w.Write(dataBytes);

        const double amplitude = 0.25 * short.MaxValue;
        for (var i = 0; i < totalSamples; i++)
        {
            var sample = (short)(amplitude * Math.Sin(2 * Math.PI * frequencyHz * i / sampleRate));
            w.Write(sample);
        }

        w.Flush();
        return ms.ToArray();
    }
}

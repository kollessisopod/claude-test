using System.Diagnostics;
using OstQuiz.Api.Domain;

namespace OstQuiz.Api.Services.Audio;

/// <summary>
/// Trims audio into per-step clips by shelling out to ffmpeg. Each clip is the first
/// N seconds of the source, re-encoded (not stream-copied) so the cut is sample-accurate.
/// Degrades gracefully: if ffmpeg is missing or a clip fails, that clip is skipped and
/// the API falls back to serving the full audio for that step.
/// </summary>
public class FfmpegAudioClipper : IAudioClipper
{
    private readonly ILogger<FfmpegAudioClipper> _log;
    private readonly Lazy<bool> _available;

    public FfmpegAudioClipper(ILogger<FfmpegAudioClipper> log)
    {
        _log = log;
        _available = new Lazy<bool>(ProbeFfmpeg);
    }

    public bool IsAvailable => _available.Value;

    public async Task<IReadOnlyList<GeneratedClip>> GenerateAsync(
        byte[] fullAudio, string extension, string contentType, CancellationToken ct = default)
    {
        var clips = new List<GeneratedClip>();
        if (!IsAvailable)
        {
            _log.LogWarning("ffmpeg not available; skipping clip generation (will serve full audio for every step)");
            return clips;
        }

        if (string.IsNullOrWhiteSpace(extension)) extension = ".mp3";

        var workDir = Path.Combine(Path.GetTempPath(), "ostquiz-clips", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);
        var inputPath = Path.Combine(workDir, $"input{extension}");
        try
        {
            await File.WriteAllBytesAsync(inputPath, fullAudio, ct);

            for (var step = 0; step < GameRules.DurationSteps.Length; step++)
            {
                var seconds = GameRules.DurationSteps[step];
                if (seconds is null) continue; // final step = full audio, no clip

                var outputPath = Path.Combine(workDir, $"clip{step}{extension}");
                if (await RunFfmpegAsync(inputPath, outputPath, seconds.Value, ct) && File.Exists(outputPath))
                {
                    var data = await File.ReadAllBytesAsync(outputPath, ct);
                    clips.Add(new GeneratedClip(step, seconds.Value, data, contentType));
                }
                else
                {
                    _log.LogWarning("Failed to generate {Seconds}s clip for step {Step}", seconds, step);
                }
            }
        }
        finally
        {
            try { Directory.Delete(workDir, recursive: true); } catch { /* best-effort cleanup */ }
        }

        return clips;
    }

    private async Task<bool> RunFfmpegAsync(string input, string output, int seconds, CancellationToken ct)
    {
        // -t limits duration from the start; re-encode for an accurate cut at arbitrary points.
        var psi = new ProcessStartInfo("ffmpeg")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add("-y");
        psi.ArgumentList.Add("-i"); psi.ArgumentList.Add(input);
        psi.ArgumentList.Add("-t"); psi.ArgumentList.Add(seconds.ToString());
        psi.ArgumentList.Add("-map"); psi.ArgumentList.Add("0:a");
        psi.ArgumentList.Add(output);

        using var proc = Process.Start(psi);
        if (proc is null) return false;
        var stderr = await proc.StandardError.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);
        if (proc.ExitCode != 0)
            _log.LogWarning("ffmpeg exited {Code}: {Err}", proc.ExitCode, stderr.Length > 500 ? stderr[^500..] : stderr);
        return proc.ExitCode == 0;
    }

    private bool ProbeFfmpeg()
    {
        try
        {
            using var proc = Process.Start(new ProcessStartInfo("ffmpeg", "-version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            });
            if (proc is null) return false;
            proc.WaitForExit(5000);
            return proc.HasExited && proc.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}

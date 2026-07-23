using System.Text.Json;
using RoyalNewsDesk.Core.Formatting;
using RoyalNewsDesk.Core.Processes;
using RoyalNewsDesk.Core.Tools;

namespace RoyalNewsDesk.Core.Audio;

/// <summary>
/// Two-pass ffmpeg loudnorm to -16 LUFS / -1.5 dBTP (voice standard;
/// YouTube nudges playback toward -14, which is safe from below). Linear
/// mode keeps dynamics and duration, so the timeline stays valid.
/// </summary>
public sealed class LoudnessNormalizer(IProcessRunner runner, IToolLocator locator)
{
    private const string Target = "I=-16:TP=-1.5:LRA=11";

    /// <returns>False when measuring failed and the file was resampled without normalizing.</returns>
    public async Task<bool> NormalizeAsync(string inputWav, string outputWav48k, CancellationToken ct)
    {
        var ffmpeg = locator.GetToolPath(ExternalTool.Ffmpeg);

        var measure = await runner.RunAsync(new ProcessSpec
        {
            ExePath = ffmpeg,
            Arguments =
            [
                "-y", "-nostdin", "-hide_banner",
                "-i", inputWav,
                "-af", "loudnorm=" + Target + ":print_format=json",
                "-f", "null", "-",
            ],
            Timeout = TimeSpan.FromMinutes(10),
        }, ct).ConfigureAwait(false);

        var stats = measure.ExitCode == 0 ? ParseLoudnormJson(measure.StdErrTail) : null;
        var filter = stats is null
            ? "anull"
            : Inv.F($"loudnorm={Target}:measured_I={stats.InputI}:measured_TP={stats.InputTp}:measured_LRA={stats.InputLra}:measured_thresh={stats.InputThresh}:offset={stats.TargetOffset}:linear=true");

        var apply = await runner.RunAsync(new ProcessSpec
        {
            ExePath = ffmpeg,
            Arguments =
            [
                "-y", "-nostdin", "-hide_banner", "-loglevel", "error",
                "-i", inputWav,
                "-af", filter,
                "-ar", "48000", "-ac", "1",
                "-c:a", "pcm_s16le",
                outputWav48k,
            ],
            Timeout = TimeSpan.FromMinutes(10),
        }, ct).ConfigureAwait(false);

        if (!apply.Success)
        {
            throw new ToolExecutionException(
                ExternalTool.Ffmpeg,
                Inv.F($"loudnorm apply failed, exit {apply.ExitCode}. {apply.StdErrTail}"));
        }

        return stats is not null;
    }

    private sealed record LoudnormStats(string InputI, string InputTp, string InputLra, string InputThresh, string TargetOffset);

    private static LoudnormStats? ParseLoudnormJson(string stderr)
    {
        var start = stderr.LastIndexOf('{');
        var end = stderr.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(stderr[start..(end + 1)]);
            var root = doc.RootElement;
            return new LoudnormStats(
                root.GetProperty("input_i").GetString()!,
                root.GetProperty("input_tp").GetString()!,
                root.GetProperty("input_lra").GetString()!,
                root.GetProperty("input_thresh").GetString()!,
                root.GetProperty("target_offset").GetString()!);
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

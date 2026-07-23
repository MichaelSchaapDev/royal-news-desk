using System.Globalization;
using System.Text.Json;
using RoyalNewsDesk.Core.Audio;
using RoyalNewsDesk.Core.Processes;
using RoyalNewsDesk.Core.Tools;

namespace RoyalNewsDesk.Core.LipSync;

/// <summary>
/// Runs rhubarb.exe once over the whole voice track. Silence between
/// segments yields rest shapes by itself, so no per-segment splitting.
/// </summary>
public sealed class RhubarbLipSyncEngine(IProcessRunner runner, IToolLocator locator) : ILipSyncEngine
{
    public async Task<MouthCueTrack> AnalyzeAsync(
        string wavPath,
        string transcriptPath,
        string outputJsonPath,
        IProgress<double>? progress,
        CancellationToken ct)
    {
        var audioDuration = WavFile.ReadInfo(wavPath).Duration;
        var timeout = TimeSpan.FromSeconds(Math.Max(120, audioDuration.TotalSeconds * 6));

        var spec = new ProcessSpec
        {
            ExePath = locator.GetToolPath(ExternalTool.Rhubarb),
            Arguments =
            [
                "-f", "json",
                "-o", outputJsonPath,
                "--dialogFile", transcriptPath,
                "-r", "pocketSphinx",
                "--extendedShapes", "GHX",
                "--machineReadable",
                wavPath,
            ],
            Timeout = timeout,
            OnErrorLine = line => ReportProgress(line, progress),
        };

        var result = await runner.RunAsync(spec, ct).ConfigureAwait(false);
        if (result.TimedOut)
        {
            throw new ToolExecutionException(ExternalTool.Rhubarb, "Timed out after " + timeout.ToString());
        }

        if (result.ExitCode != 0 || !File.Exists(outputJsonPath))
        {
            throw new ToolExecutionException(
                ExternalTool.Rhubarb,
                Formatting.Inv.F($"Exit {result.ExitCode}. {result.StdErrTail}"));
        }

        return Parse(await File.ReadAllTextAsync(outputJsonPath, ct).ConfigureAwait(false));
    }

    public static MouthCueTrack Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var duration = doc.RootElement.GetProperty("metadata").GetProperty("duration").GetDouble();
        var cues = new List<MouthCue>();
        foreach (var cue in doc.RootElement.GetProperty("mouthCues").EnumerateArray())
        {
            cues.Add(new MouthCue(
                cue.GetProperty("start").GetDouble(),
                cue.GetProperty("end").GetDouble(),
                Enum.Parse<MouthShape>(cue.GetProperty("value").GetString()!)));
        }

        return new MouthCueTrack(duration, cues);
    }

    private static void ReportProgress(string stderrLine, IProgress<double>? progress)
    {
        if (progress is null || !stderrLine.Contains("\"progress\"", StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(stderrLine);
            if (doc.RootElement.TryGetProperty("value", out var value)
                && value.ValueKind == JsonValueKind.Number)
            {
                progress.Report(value.GetDouble());
            }
        }
        catch (JsonException)
        {
            // Not a machine-readable line; ignore.
        }
    }
}

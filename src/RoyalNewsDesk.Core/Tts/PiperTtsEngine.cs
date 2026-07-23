using System.Globalization;
using System.Text;
using System.Text.Json;
using RoyalNewsDesk.Core.Audio;
using RoyalNewsDesk.Core.Processes;
using RoyalNewsDesk.Core.Tools;

namespace RoyalNewsDesk.Core.Tts;

/// <summary>
/// Drives piper.exe in json-input mode: one process per episode (model
/// loading is the slow part), one wav per sentence (exact durations drive
/// subtitles and lip-sync).
/// </summary>
public sealed class PiperTtsEngine(IProcessRunner runner, IToolLocator locator) : ITtsEngine
{
    // One line per sentence; piper wants lowercase keys and no pretty printing.
    private static readonly JsonSerializerOptions PiperJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task<IReadOnlyList<TtsSentenceResult>> SynthesizeAsync(
        TtsBatch batch,
        IProgress<double>? progress,
        CancellationToken ct)
    {
        if (batch.Sentences.Count == 0)
        {
            return [];
        }

        await RunPiperAsync(batch, batch.Sentences, progress, ct).ConfigureAwait(false);

        // Piper very occasionally skips a sentence; retry stragglers once in
        // a fresh process before giving up.
        var missing = batch.Sentences.Where(s => !IsCompleteWav(s.WavPath)).ToList();
        if (missing.Count > 0)
        {
            await RunPiperAsync(batch, missing, progress: null, ct).ConfigureAwait(false);
        }

        var stillMissing = batch.Sentences.FirstOrDefault(s => !IsCompleteWav(s.WavPath));
        if (stillMissing is not null)
        {
            throw new ToolExecutionException(
                ExternalTool.Piper,
                "No audio was produced for sentence: " + stillMissing.Text);
        }

        var results = new List<TtsSentenceResult>(batch.Sentences.Count);
        foreach (var sentence in batch.Sentences)
        {
            var info = WavFile.ReadInfo(sentence.WavPath);
            results.Add(new TtsSentenceResult(
                sentence.Ordinal,
                sentence.Text,
                sentence.WavPath,
                info.Samples,
                info.SampleRate));
        }

        return results;
    }

    private async Task RunPiperAsync(
        TtsBatch batch,
        IReadOnlyList<TtsSentence> sentences,
        IProgress<double>? progress,
        CancellationToken ct)
    {
        var piperPath = locator.GetToolPath(ExternalTool.Piper);
        var espeakData = Path.Combine(Path.GetDirectoryName(piperPath)!, "espeak-ng-data");

        var stdin = new StringBuilder();
        foreach (var sentence in sentences)
        {
            // System.Text.Json handles escaping; forward slashes sidestep
            // any backslash-escaping quirks inside piper's json parser.
            stdin.Append(JsonSerializer.Serialize(
                new PiperLine(sentence.Text, sentence.WavPath.Replace('\\', '/')),
                PiperJson));
            stdin.Append('\n');
        }

        var spec = new ProcessSpec
        {
            ExePath = piperPath,
            Arguments =
            [
                "--model", batch.ModelPath,
                "--config", batch.ConfigPath,
                "--espeak_data", espeakData,
                "--sentence_silence", "0",
                "--length_scale", batch.LengthScale.ToString("0.00", CultureInfo.InvariantCulture),
                "--json-input",
                "--quiet",
            ],
            StdinText = stdin.ToString(),
            Timeout = TimeSpan.FromSeconds(90 + 5 * sentences.Count),
        };

        using var poller = progress is null
            ? null
            : new Timer(
                _ => progress.Report(CountDone(sentences) / (double)sentences.Count),
                null,
                TimeSpan.FromMilliseconds(500),
                TimeSpan.FromMilliseconds(500));

        var result = await runner.RunAsync(spec, ct).ConfigureAwait(false);
        progress?.Report(CountDone(sentences) / (double)sentences.Count);

        if (result.TimedOut)
        {
            throw new ToolExecutionException(ExternalTool.Piper, "Timed out. " + result.StdErrTail);
        }

        if (result.ExitCode != 0)
        {
            throw new ToolExecutionException(
                ExternalTool.Piper,
                Formatting.Inv.F($"Exit {result.ExitCode}. {result.StdErrTail}"));
        }
    }

    private static int CountDone(IReadOnlyList<TtsSentence> sentences) =>
        sentences.Count(s => IsCompleteWav(s.WavPath));

    private static bool IsCompleteWav(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return info.Exists && info.Length > 44;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private sealed record PiperLine(string Text, string Output_file);
}

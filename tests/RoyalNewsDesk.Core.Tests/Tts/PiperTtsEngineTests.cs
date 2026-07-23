using System.Text.Json;
using RoyalNewsDesk.Core.Audio;
using RoyalNewsDesk.Core.Processes;
using RoyalNewsDesk.Core.Tools;
using RoyalNewsDesk.Core.Tts;

namespace RoyalNewsDesk.Core.Tests.Tts;

public class PiperTtsEngineTests
{
    private sealed class FakeToolLocator(string baseDir) : IToolLocator
    {
        public string GetToolPath(ExternalTool tool) => Path.Combine(baseDir, tool + ".exe");
    }

    /// <summary>Pretends to be piper: parses stdin json lines and writes tiny wavs.</summary>
    private sealed class FakePiperRunner : IProcessRunner
    {
        public List<ProcessSpec> Specs { get; } = [];

        public HashSet<string> SkipOutputs { get; } = [];

        public Task<ProcessResult> RunAsync(ProcessSpec spec, CancellationToken ct)
        {
            Specs.Add(spec);
            foreach (var line in spec.StdinText!.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var doc = JsonDocument.Parse(line);
                var output = doc.RootElement.GetProperty("output_file").GetString()!;
                if (SkipOutputs.Remove(output))
                {
                    continue;
                }

                WavFile.WritePcm16(output, 22050, 1, new short[2205]);
            }

            return Task.FromResult(new ProcessResult(0, "", "", TimeSpan.FromMilliseconds(5)));
        }
    }

    private static TtsBatch Batch(TempDir temp, params string[] texts)
    {
        var sentences = texts
            .Select((text, i) => new TtsSentence(
                i,
                text,
                Path.Combine(temp.Path, "s" + i.ToString("0000", System.Globalization.CultureInfo.InvariantCulture) + ".wav")))
            .ToList();
        return new TtsBatch(sentences, @"C:\models\voice.onnx", @"C:\models\voice.onnx.json", 1.0);
    }

    [Fact]
    public async Task SynthesizesPerSentenceWavs()
    {
        using var temp = new TempDir();
        var runner = new FakePiperRunner();
        var engine = new PiperTtsEngine(runner, new FakeToolLocator(temp.Path));

        var results = await engine.SynthesizeAsync(Batch(temp, "One.", "Two."), null, CancellationToken.None);

        Assert.Equal(2, results.Count);
        Assert.All(results, r =>
        {
            Assert.Equal(22050, r.SampleRate);
            Assert.Equal(2205, r.Samples);
            Assert.True(File.Exists(r.WavPath));
        });

        var spec = Assert.Single(runner.Specs);
        Assert.Contains("--json-input", spec.Arguments);
        Assert.Contains("--sentence_silence", spec.Arguments);
        Assert.Contains("1.00", spec.Arguments); // length scale, invariant format

        var lines = spec.StdinText!.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        var first = JsonDocument.Parse(lines[0]).RootElement;
        Assert.Equal("One.", first.GetProperty("text").GetString());
        Assert.Contains("/", first.GetProperty("output_file").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RetriesMissingSentencesOnce()
    {
        using var temp = new TempDir();
        var runner = new FakePiperRunner();
        var engine = new PiperTtsEngine(runner, new FakeToolLocator(temp.Path));
        var batch = Batch(temp, "One.", "Two.", "Three.");
        runner.SkipOutputs.Add(batch.Sentences[1].WavPath.Replace('\\', '/'));

        var results = await engine.SynthesizeAsync(batch, null, CancellationToken.None);

        Assert.Equal(3, results.Count);
        Assert.Equal(2, runner.Specs.Count);
        var retryLines = runner.Specs[1].StdinText!.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Single(retryLines);
        Assert.Contains("Two.", retryLines[0], StringComparison.Ordinal);
    }
}

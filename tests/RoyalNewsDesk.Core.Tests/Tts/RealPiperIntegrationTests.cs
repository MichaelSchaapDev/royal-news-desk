using RoyalNewsDesk.Core.Audio;
using RoyalNewsDesk.Core.Processes;
using RoyalNewsDesk.Core.Tools;
using RoyalNewsDesk.Core.Tts;

namespace RoyalNewsDesk.Core.Tests.Tts;

/// <summary>
/// Runs the real piper.exe with the real downloaded voice. Self-skips when
/// either is absent (CI has neither), so it only guards developer machines.
/// </summary>
public class RealPiperIntegrationTests
{
    [Fact]
    public async Task SynthesizesAudibleSpeech()
    {
        var toolsDir = FindToolsBinDir();
        var modelsRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RoyalNewsDeskStudio",
            "models",
            "en_GB-cori-high");
        var modelPath = Path.Combine(modelsRoot, "en_GB-cori-high.onnx");
        if (toolsDir is null || !File.Exists(modelPath))
        {
            return; // Not a machine with tools and a voice; nothing to verify.
        }

        using var temp = new TempDir();
        var engine = new PiperTtsEngine(new ProcessRunner(), new InstalledToolLocator(toolsDir));
        var batch = new TtsBatch(
            [
                new TtsSentence(0, "Good evening, and welcome to the Royal News Desk.", Path.Combine(temp.Path, "s0.wav")),
                new TtsSentence(1, "Tonight we separate royal fact from royal fiction.", Path.Combine(temp.Path, "s1.wav")),
            ],
            modelPath,
            modelPath + ".json",
            LengthScale: 1.0);

        var results = await engine.SynthesizeAsync(batch, null, CancellationToken.None);

        Assert.Equal(2, results.Count);
        foreach (var result in results)
        {
            Assert.Equal(22050, result.SampleRate);
            var duration = result.Samples / 22050.0;
            Assert.InRange(duration, 1.0, 10.0);

            // Silence would mean piper "succeeded" without speaking.
            var samples = WavFile.ReadMonoPcm16(result.WavPath);
            var rms = Math.Sqrt(samples.Select(s => (double)s * s).Average());
            Assert.True(rms > 100, "Audio is near-silent, RMS " + rms.ToString("0", System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    private static string? FindToolsBinDir()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 8 && dir is not null; i++)
        {
            var candidate = Path.Combine(dir, "tools", "bin");
            if (File.Exists(Path.Combine(candidate, "piper", "piper.exe")))
            {
                return candidate;
            }

            dir = Path.GetDirectoryName(dir);
        }

        return null;
    }
}

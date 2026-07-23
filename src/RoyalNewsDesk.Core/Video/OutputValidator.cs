using System.Globalization;
using System.Text;
using System.Text.Json;
using RoyalNewsDesk.Core.Formatting;
using RoyalNewsDesk.Core.Processes;
using RoyalNewsDesk.Core.Tools;

namespace RoyalNewsDesk.Core.Video;

/// <summary>
/// Refuses to call an episode done until the file proves itself: right
/// codecs and geometry, duration within tolerance, audible audio, and a
/// faststart layout. A broken file with an .mp4 extension is still a failure.
/// </summary>
public sealed class OutputValidator(IProcessRunner runner, IToolLocator locator)
{
    public async Task<IReadOnlyList<string>> ValidateAsync(
        string videoPath,
        Timeline timeline,
        CancellationToken ct)
    {
        var issues = new List<string>();

        var probe = await runner.RunAsync(new ProcessSpec
        {
            ExePath = locator.GetToolPath(ExternalTool.Ffprobe),
            Arguments =
            [
                "-v", "error",
                "-print_format", "json",
                "-show_format", "-show_streams",
                videoPath,
            ],
            Timeout = TimeSpan.FromMinutes(2),
        }, ct).ConfigureAwait(false);

        if (probe.ExitCode != 0)
        {
            issues.Add("ffprobe failed: " + probe.StdErrTail);
            return issues;
        }

        try
        {
            using var doc = JsonDocument.Parse(probe.StdOutTail);
            var streams = doc.RootElement.GetProperty("streams").EnumerateArray().ToList();
            var video = streams.FirstOrDefault(s => s.GetProperty("codec_type").GetString() == "video");
            var audio = streams.FirstOrDefault(s => s.GetProperty("codec_type").GetString() == "audio");

            if (video.ValueKind != JsonValueKind.Object)
            {
                issues.Add("no video stream");
            }
            else
            {
                Check(issues, video.GetProperty("codec_name").GetString() == "h264", "video codec is not h264");
                Check(issues, video.GetProperty("width").GetInt32() == 1920, "width is not 1920");
                Check(issues, video.GetProperty("height").GetInt32() == 1080, "height is not 1080");
                Check(issues, video.GetProperty("pix_fmt").GetString() == "yuv420p", "pixel format is not yuv420p");
                Check(issues, video.GetProperty("r_frame_rate").GetString() == "25/1", "frame rate is not 25");
            }

            if (audio.ValueKind != JsonValueKind.Object)
            {
                issues.Add("no audio stream");
            }
            else
            {
                Check(issues, audio.GetProperty("codec_name").GetString() == "aac", "audio codec is not aac");
                Check(issues, audio.GetProperty("sample_rate").GetString() == "48000", "audio is not 48 kHz");
            }

            var duration = double.Parse(
                doc.RootElement.GetProperty("format").GetProperty("duration").GetString()!,
                CultureInfo.InvariantCulture);
            if (Math.Abs(duration - timeline.TotalDuration) > 0.35)
            {
                issues.Add(Inv.F($"duration {duration:0.00}s is off the plan ({timeline.TotalDuration:0.00}s)"));
            }
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or FormatException)
        {
            issues.Add("could not read probe output: " + ex.Message);
        }

        await CheckAudibleAsync(videoPath, issues, ct).ConfigureAwait(false);
        CheckFastStart(videoPath, issues);
        return issues;
    }

    private async Task CheckAudibleAsync(string videoPath, List<string> issues, CancellationToken ct)
    {
        var result = await runner.RunAsync(new ProcessSpec
        {
            ExePath = locator.GetToolPath(ExternalTool.Ffmpeg),
            Arguments =
            [
                "-nostdin", "-hide_banner",
                "-i", videoPath,
                "-map", "0:a:0",
                "-af", "volumedetect",
                "-f", "null", "-",
            ],
            Timeout = TimeSpan.FromMinutes(10),
        }, ct).ConfigureAwait(false);

        var line = result.StdErrTail
            .Split('\n')
            .FirstOrDefault(l => l.Contains("mean_volume:", StringComparison.Ordinal));
        if (line is null)
        {
            issues.Add("could not measure audio level");
            return;
        }

        var value = line[(line.IndexOf("mean_volume:", StringComparison.Ordinal) + "mean_volume:".Length)..]
            .Replace("dB", "", StringComparison.Ordinal)
            .Trim();
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var mean))
        {
            if (mean < -50)
            {
                issues.Add(Inv.F($"audio is near-silent (mean {mean:0.0} dB)"));
            }
            else if (mean > -5)
            {
                issues.Add(Inv.F($"audio is clipping (mean {mean:0.0} dB)"));
            }
        }
    }

    private static void CheckFastStart(string videoPath, List<string> issues)
    {
        // With +faststart the moov box sits before mdat near the file start.
        var buffer = new byte[262_144];
        using var stream = File.OpenRead(videoPath);
        var read = stream.Read(buffer, 0, buffer.Length);
        var moov = IndexOf(buffer, read, "moov"u8);
        var mdat = IndexOf(buffer, read, "mdat"u8);
        if (moov < 0 || (mdat >= 0 && mdat < moov))
        {
            issues.Add("file is not faststart (moov after mdat)");
        }
    }

    private static int IndexOf(byte[] haystack, int length, ReadOnlySpan<byte> needle)
    {
        return haystack.AsSpan(0, length).IndexOf(needle);
    }

    private static void Check(List<string> issues, bool condition, string issue)
    {
        if (!condition)
        {
            issues.Add(issue);
        }
    }
}

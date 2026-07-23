using System.Globalization;
using RoyalNewsDesk.Core.Formatting;
using RoyalNewsDesk.Core.Processes;
using RoyalNewsDesk.Core.Tools;

namespace RoyalNewsDesk.Core.Video;

/// <summary>
/// Runs the ffmpeg renders: intro and outro cards, the body graph, the
/// stream-copy concat, and the final mux with the master audio. Every call
/// uses the episode work directory as cwd with relative paths, and every
/// part shares identical encode constants so concat can stream-copy.
/// </summary>
public sealed class VideoComposer(IProcessRunner runner, IToolLocator locator)
{
    private static readonly string[] EncodeConstants =
    [
        "-c:v", "libx264",
        "-crf", "19",
        "-profile:v", "high", "-level", "4.0",
        "-pix_fmt", "yuv420p",
        "-r", "25",
        "-g", "50", "-keyint_min", "25", "-sc_threshold", "0",
        "-colorspace", "bt709", "-color_primaries", "bt709", "-color_trc", "bt709",
        "-video_track_timescale", "12800",
        "-an",
    ];

    /// <summary>Fade/zoom render of a still card into a video part.</summary>
    public async Task RenderCardAsync(
        string workDir,
        string cardRelativePath,
        string outputRelativePath,
        double duration,
        bool higherQuality,
        CancellationToken ct)
    {
        var frames = (int)Math.Round(duration * Timeline.Fps);
        var fadeOutStart = duration - (duration >= 6 ? 0.8 : 0.4);
        var filter = Inv.F(
            $"[0:v]scale=3840:2160,zoompan=z='min(1.0+{0.06 / frames:0.0000000}*on,1.06)':x='iw/2-(iw/zoom/2)':y='ih/2-(ih/zoom/2)':d={frames}:s=1920x1080:fps=25,fade=t=in:st=0:d=0.5,fade=t=out:st={fadeOutStart:0.000}:d={duration - fadeOutStart:0.000},format=yuv420p[v]");

        var result = await runner.RunAsync(new ProcessSpec
        {
            ExePath = locator.GetToolPath(ExternalTool.Ffmpeg),
            WorkingDirectory = workDir,
            Arguments =
            [
                "-y", "-nostdin", "-hide_banner", "-loglevel", "warning",
                "-i", cardRelativePath,
                "-filter_complex", filter,
                "-map", "[v]",
                "-frames:v", Inv.I(frames),
                "-preset", Preset(higherQuality),
                .. EncodeConstants,
                outputRelativePath,
            ],
            Timeout = TimeSpan.FromMinutes(5),
        }, ct).ConfigureAwait(false);

        ThrowOnFailure(result, "card render");
    }

    /// <summary>The main body render from the prepared plan and graph file.</summary>
    public async Task RenderBodyAsync(
        string workDir,
        BodyRenderPlan plan,
        bool higherQuality,
        IProgress<double>? progress,
        CancellationToken ct)
    {
        File.WriteAllText(Path.Combine(workDir, "parts", "body_graph.txt"), plan.FilterGraph);
        var expectedUs = plan.TotalFrames / (double)Timeline.Fps * 1_000_000;

        var result = await runner.RunAsync(new ProcessSpec
        {
            ExePath = locator.GetToolPath(ExternalTool.Ffmpeg),
            WorkingDirectory = workDir,
            Arguments =
            [
                "-y", "-nostdin", "-hide_banner", "-loglevel", "warning",
                "-progress", "pipe:1", "-nostats",
                .. plan.InputArguments,
                "-filter_complex_script", "parts/body_graph.txt",
                "-map", "[vout]",
                "-frames:v", Inv.I(plan.TotalFrames),
                "-preset", Preset(higherQuality),
                .. EncodeConstants,
                "parts/10_body.mp4",
            ],
            Timeout = TimeSpan.FromHours(2),
            OnOutputLine = line => ReportFfmpegProgress(line, expectedUs, progress),
        }, ct).ConfigureAwait(false);

        ThrowOnFailure(result, "body render");
    }

    /// <summary>Concat the parts (video copy) and mux the master audio, faststart.</summary>
    public async Task FinalMuxAsync(
        string workDir,
        string outputRelativePath,
        bool higherQuality,
        CancellationToken ct)
    {
        File.WriteAllText(
            Path.Combine(workDir, "parts", "parts.txt"),
            "ffconcat version 1.0\nfile '00_intro.mp4'\nfile '10_body.mp4'\nfile '20_outro.mp4'\n");

        var copyResult = await RunMuxAsync(workDir, outputRelativePath, reencode: false, higherQuality, ct)
            .ConfigureAwait(false);
        if (!copyResult.Success)
        {
            // Stream copy should always work (identical encoders); if a build
            // quirk breaks it, re-encoding is slower but always correct.
            var reencoded = await RunMuxAsync(workDir, outputRelativePath, reencode: true, higherQuality, ct)
                .ConfigureAwait(false);
            ThrowOnFailure(reencoded, "final mux");
        }
    }

    private async Task<ProcessResult> RunMuxAsync(
        string workDir,
        string outputRelativePath,
        bool reencode,
        bool higherQuality,
        CancellationToken ct)
    {
        List<string> arguments =
        [
            "-y", "-nostdin", "-hide_banner", "-loglevel", "warning",
            "-f", "concat", "-safe", "0", "-i", "parts/parts.txt",
            "-i", "audio/master_audio.wav",
            "-map", "0:v:0", "-map", "1:a:0",
        ];
        if (reencode)
        {
            arguments.Add("-preset");
            arguments.Add(Preset(higherQuality));
            arguments.AddRange(EncodeConstants.Where(a => a != "-an"));
        }
        else
        {
            arguments.AddRange(["-c:v", "copy"]);
        }

        arguments.AddRange(
        [
            "-c:a", "aac", "-b:a", "192k", "-ar", "48000", "-ac", "2",
            "-movflags", "+faststart",
            outputRelativePath,
        ]);

        return await runner.RunAsync(new ProcessSpec
        {
            ExePath = locator.GetToolPath(ExternalTool.Ffmpeg),
            WorkingDirectory = workDir,
            Arguments = arguments,
            Timeout = TimeSpan.FromHours(1),
        }, ct).ConfigureAwait(false);
    }

    private static string Preset(bool higherQuality) => higherQuality ? "medium" : "veryfast";

    private static void ReportFfmpegProgress(string line, double expectedUs, IProgress<double>? progress)
    {
        if (progress is null || !line.StartsWith("out_time_us=", StringComparison.Ordinal))
        {
            return;
        }

        if (double.TryParse(line["out_time_us=".Length..], NumberStyles.Float, CultureInfo.InvariantCulture, out var us)
            && expectedUs > 0)
        {
            progress.Report(Math.Clamp(us / expectedUs, 0, 1));
        }
    }

    private static void ThrowOnFailure(ProcessResult result, string what)
    {
        if (result.TimedOut)
        {
            throw new ToolExecutionException(ExternalTool.Ffmpeg, what + " timed out. " + result.StdErrTail);
        }

        if (result.ExitCode != 0)
        {
            throw new ToolExecutionException(
                ExternalTool.Ffmpeg,
                Inv.F($"{what} failed, exit {result.ExitCode}. {result.StdErrTail}"));
        }
    }
}

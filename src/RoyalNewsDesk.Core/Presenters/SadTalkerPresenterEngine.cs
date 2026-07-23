using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using RoyalNewsDesk.Core.Formatting;
using RoyalNewsDesk.Core.Processes;
using RoyalNewsDesk.Core.Tools;

namespace RoyalNewsDesk.Core.Presenters;

/// <summary>
/// Drives a downloaded SadTalker bundle: copies the portrait and voice into
/// an ASCII job folder, runs the bundled Python with a confined environment,
/// parses tqdm progress from stderr, harvests the timestamped output, and
/// normalizes it to the pipeline's 25 fps h264 contract.
/// </summary>
public sealed partial class SadTalkerPresenterEngine(
    IProcessRunner runner,
    IToolLocator locator,
    IPresenterEngineManager manager) : IPresenterEngine
{
    // Stage label → (order, weight without enhancer, weight with enhancer).
    // Labels as SadTalker prints them; each tqdm desc already ends in a colon,
    // so rendered lines carry a double colon ("Face Renderer::  40%|...").
    private static readonly (string Label, double Plain, double Enhanced)[] Stages =
    [
        ("landmark Det", 0.02, 0.01),
        ("3DMM Extraction", 0.03, 0.02),
        ("mel", 0.03, 0.01),
        ("audio2exp", 0.02, 0.01),
        ("Face Renderer", 0.70, 0.40),
        ("Face Enhancer", 0.00, 0.40),
        ("seamlessClone", 0.20, 0.15),
    ];

    [GeneratedRegex(@"^\s*(?<stage>[A-Za-z0-9 ]+?):{1,2}\s*(?<pct>\d+)%\|")]
    private static partial Regex TqdmLine();

    public async Task<PresenterTrack> RenderAsync(
        PresenterRequest request,
        IProgress<double>? progress,
        CancellationToken ct)
    {
        var engineId = request.EngineId
            ?? throw new ArgumentException("Photoreal render needs an engine id.", nameof(request));
        var portrait = request.PortraitPath
            ?? throw new ArgumentException("Photoreal render needs a portrait.", nameof(request));
        var audio = request.AudioWavPath
            ?? throw new ArgumentException("Photoreal render needs a voice wav.", nameof(request));

        var engineDir = manager.GetEngineDir(engineId);
        var python = manager.GetEntrypointPath(engineId);
        if (!File.Exists(python))
        {
            throw new PresenterRenderException(engineId, "engine is not installed");
        }

        var isCpu = engineId.EndsWith("cpu", StringComparison.OrdinalIgnoreCase);
        var jobId = Guid.NewGuid().ToString("N");
        var jobDir = Path.Combine(engineDir, "jobs", jobId);
        var jobOut = Path.Combine(jobDir, "out");

        try
        {
            Directory.CreateDirectory(jobOut);
            var sourceName = "source" + SafePortraitExtension(portrait);
            File.Copy(portrait, Path.Combine(jobDir, sourceName), overwrite: true);
            File.Copy(audio, Path.Combine(jobDir, "audio.wav"), overwrite: true);

            await RunEngineAsync(engineId, engineDir, python, jobId, sourceName, isCpu, request.BodyDuration, progress, ct)
                .ConfigureAwait(false);

            var rawPath = HarvestOutput(engineId, jobOut);
            await NormalizeAsync(engineId, request.Paths.WorkDir, rawPath, ct).ConfigureAwait(false);
            progress?.Report(0.95);

            var (width, height) = await ProbeGeometryAsync(
                engineId,
                Path.Combine(request.Paths.PresenterDir, "presenter.mp4"),
                ct).ConfigureAwait(false);
            progress?.Report(1.0);

            return new PresenterTrack.Video("presenter/presenter.mp4", width, height);
        }
        finally
        {
            TryDeleteDirectory(jobDir);
        }
    }

    private async Task RunEngineAsync(
        string engineId,
        string engineDir,
        string python,
        string jobId,
        string sourceName,
        bool isCpu,
        double bodyDuration,
        IProgress<double>? progress,
        CancellationToken ct)
    {
        var scriptDir = Path.Combine(engineDir, "engine");
        List<string> arguments =
        [
            "-u", "inference.py",
            "--driven_audio", "../jobs/" + jobId + "/audio.wav",
            "--source_image", "../jobs/" + jobId + "/" + sourceName,
            "--result_dir", "../jobs/" + jobId + "/out",
            "--checkpoint_dir", "./checkpoints",
            "--preprocess", "full",
            "--still",
            "--pose_style", "0",
            "--expression_scale", "1.0",
        ];
        if (isCpu)
        {
            arguments.AddRange(["--cpu", "--size", "256", "--batch_size", "1"]);
        }
        else
        {
            arguments.AddRange(["--size", "512", "--enhancer", "gfpgan", "--batch_size", "2"]);
        }

        var audioMinutes = Math.Max(0.05, bodyDuration / 60.0);
        var hardCap = isCpu
            ? TimeSpan.FromMinutes(Math.Min(360, 30 + audioMinutes * 90))
            : TimeSpan.FromMinutes(10 + audioMinutes * 8);
        var liveness = isCpu ? TimeSpan.FromMinutes(15) : TimeSpan.FromMinutes(5);

        var parser = new TqdmProgressParser(withEnhancer: !isCpu, progress);
        var lastActivity = DateTime.UtcNow;
        using var livenessCts = new CancellationTokenSource();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, livenessCts.Token);
        using var watchdog = new Timer(
            _ =>
            {
                if (DateTime.UtcNow - lastActivity > liveness)
                {
                    livenessCts.Cancel();
                }
            },
            null,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(30));

        var spec = new ProcessSpec
        {
            ExePath = python,
            WorkingDirectory = scriptDir,
            Arguments = arguments,
            Timeout = hardCap,
            EnvironmentOverrides = BuildEnvironment(engineDir, isCpu),
            OnOutputLine = _ => lastActivity = DateTime.UtcNow,
            OnErrorLine = line =>
            {
                lastActivity = DateTime.UtcNow;
                parser.Consume(line);
            },
        };

        ProcessResult result;
        try
        {
            result = await runner.RunAsync(spec, linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new PresenterRenderException(
                engineId,
                Inv.F($"no output for {liveness.TotalMinutes:0} minutes; the render was stopped"));
        }
        catch (ProcessStartException ex)
        {
            throw new PresenterRenderException(engineId, ex.Message, ex);
        }

        if (result.TimedOut)
        {
            throw new PresenterRenderException(engineId, "render hit the time limit. " + result.StdErrTail);
        }

        if (result.ExitCode != 0)
        {
            throw new PresenterRenderException(
                engineId,
                Inv.F($"exit {result.ExitCode}. {result.StdErrTail}"));
        }
    }

    private static Dictionary<string, string?> BuildEnvironment(string engineDir, bool isCpu)
    {
        var systemRoot = Environment.GetEnvironmentVariable("SystemRoot") ?? @"C:\Windows";
        var cache = Path.Combine(engineDir, "cache");
        var tmp = Path.Combine(engineDir, "tmp");
        Directory.CreateDirectory(cache);
        Directory.CreateDirectory(tmp);

        var env = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["PATH"] = string.Join(
                ';',
                Path.Combine(engineDir, "bin"),
                Path.Combine(engineDir, "python"),
                Environment.SystemDirectory,
                systemRoot,
                Path.Combine(Environment.SystemDirectory, "Wbem")),
            ["PYTHONHOME"] = null,
            ["PYTHONPATH"] = null,
            ["PYTHONNOUSERSITE"] = "1",
            ["PYTHONUTF8"] = "1",
            ["PYTHONIOENCODING"] = "utf-8",
            ["TEMP"] = tmp,
            ["TMP"] = tmp,
            ["TORCH_HOME"] = Path.Combine(cache, "torch"),
            ["HF_HOME"] = Path.Combine(cache, "hf"),
            ["XDG_CACHE_HOME"] = cache,
            ["MPLCONFIGDIR"] = Path.Combine(cache, "mpl"),
            ["NUMBA_CACHE_DIR"] = Path.Combine(cache, "numba"),
            ["HF_HUB_DISABLE_TELEMETRY"] = "1",
            ["DO_NOT_TRACK"] = "1",
            ["CUDA_VISIBLE_DEVICES"] = isCpu ? "-1" : "0",
        };
        if (!isCpu)
        {
            env["CUDA_MODULE_LOADING"] = "LAZY";
        }

        return env;
    }

    private static string HarvestOutput(string engineId, string jobOut)
    {
        var candidates = Directory.Exists(jobOut)
            ? Directory.GetFiles(jobOut, "*.mp4")
            : [];
        if (candidates.Length == 0)
        {
            throw new PresenterRenderException(engineId, "the engine finished without producing a video");
        }

        var newest = candidates.MaxBy(File.GetLastWriteTimeUtc)!;
        if (new FileInfo(newest).Length == 0)
        {
            throw new PresenterRenderException(engineId, "the engine produced an empty video");
        }

        return newest;
    }

    private async Task NormalizeAsync(string engineId, string workDir, string rawPath, CancellationToken ct)
    {
        var result = await runner.RunAsync(new ProcessSpec
        {
            ExePath = locator.GetToolPath(ExternalTool.Ffmpeg),
            WorkingDirectory = workDir,
            Arguments =
            [
                "-y", "-nostdin", "-hide_banner", "-loglevel", "error",
                "-i", rawPath,
                "-r", "25", "-an",
                "-c:v", "libx264", "-crf", "18", "-preset", "veryfast",
                "-pix_fmt", "yuv420p",
                "-colorspace", "bt709", "-color_primaries", "bt709", "-color_trc", "bt709",
                "presenter/presenter.mp4",
            ],
            Timeout = TimeSpan.FromMinutes(30),
        }, ct).ConfigureAwait(false);

        if (!result.Success)
        {
            throw new PresenterRenderException(
                engineId,
                Inv.F($"normalize failed, exit {result.ExitCode}. {result.StdErrTail}"));
        }
    }

    private async Task<(int Width, int Height)> ProbeGeometryAsync(string engineId, string mp4Path, CancellationToken ct)
    {
        var result = await runner.RunAsync(new ProcessSpec
        {
            ExePath = locator.GetToolPath(ExternalTool.Ffprobe),
            Arguments =
            [
                "-v", "error",
                "-print_format", "json",
                "-show_streams",
                mp4Path,
            ],
            Timeout = TimeSpan.FromMinutes(2),
        }, ct).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new PresenterRenderException(engineId, "probe failed: " + result.StdErrTail);
        }

        try
        {
            using var doc = JsonDocument.Parse(result.StdOutTail);
            var video = doc.RootElement.GetProperty("streams").EnumerateArray()
                .First(s => s.GetProperty("codec_type").GetString() == "video");
            return (video.GetProperty("width").GetInt32(), video.GetProperty("height").GetInt32());
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new PresenterRenderException(engineId, "probe output was unreadable", ex);
        }
    }

    private static string SafePortraitExtension(string portraitPath)
    {
        var ext = Path.GetExtension(portraitPath).ToLowerInvariant();
        return ext is ".png" or ".jpg" or ".jpeg" ? ext : ".png";
    }

    private static void TryDeleteDirectory(string dir)
    {
        try
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>Weighted tqdm stage machine mapping engine progress into 0..0.85.</summary>
    private sealed class TqdmProgressParser(bool withEnhancer, IProgress<double>? progress)
    {
        private int _stageIndex = -1;

        public void Consume(string stderrLine)
        {
            if (progress is null)
            {
                return;
            }

            var match = TqdmLine().Match(stderrLine);
            if (!match.Success)
            {
                return;
            }

            var label = match.Groups["stage"].Value.Trim();
            var index = Array.FindIndex(Stages, s => label.StartsWith(s.Label, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                index = Math.Max(0, _stageIndex);
            }

            _stageIndex = Math.Max(_stageIndex, index);
            if (!int.TryParse(match.Groups["pct"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pct))
            {
                return;
            }

            double completed = 0;
            for (var i = 0; i < _stageIndex; i++)
            {
                completed += Weight(i);
            }

            var overall = completed + Weight(_stageIndex) * Math.Clamp(pct, 0, 100) / 100.0;
            progress.Report(Math.Clamp(overall, 0, 1) * 0.85);
        }

        private double Weight(int index) => withEnhancer ? Stages[index].Enhanced : Stages[index].Plain;
    }
}

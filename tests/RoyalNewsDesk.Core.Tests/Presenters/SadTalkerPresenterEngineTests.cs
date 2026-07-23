using RoyalNewsDesk.Core.Presenters;
using RoyalNewsDesk.Core.Processes;
using RoyalNewsDesk.Core.Storage;
using RoyalNewsDesk.Core.Tools;

namespace RoyalNewsDesk.Core.Tests.Presenters;

public class SadTalkerPresenterEngineTests
{
    private sealed class FakeToolLocator(string baseDir) : IToolLocator
    {
        public string GetToolPath(ExternalTool tool) => Path.Combine(baseDir, tool + ".exe");
    }

    private sealed class FakeManager(string root) : IPresenterEngineManager
    {
        public IReadOnlyList<PresenterEngineInfo> Engines => [];

        public bool IsInstalled(string engineId) => true;

        public string GetEngineDir(string engineId) => Path.Combine(root, engineId);

        public string GetEntrypointPath(string engineId) => Path.Combine(root, engineId, "python", "python.exe");

        public Task DownloadAsync(string engineId, IProgress<PresenterInstallProgress>? progress, CancellationToken ct)
            => Task.CompletedTask;

        public void Delete(string engineId)
        {
        }
    }

    /// <summary>Plays python, ffmpeg, and ffprobe according to a small script.</summary>
    private sealed class ScriptedRunner : IProcessRunner
    {
        public List<ProcessSpec> Specs { get; } = [];

        public bool PythonFails { get; set; }

        public bool PythonSkipsOutput { get; set; }

        public bool FfmpegFails { get; set; }

        public Task<ProcessResult> RunAsync(ProcessSpec spec, CancellationToken ct)
        {
            Specs.Add(spec);
            var exe = Path.GetFileName(spec.ExePath);

            if (exe == "python.exe")
            {
                // Real SadTalker lines: each tqdm desc ends in ':', so the
                // rendered line carries a double colon.
                spec.OnErrorLine?.Invoke("landmark Det:: 100%|##########| 1/1 [00:00<00:00]");
                spec.OnErrorLine?.Invoke("3DMM Extraction In Video:: 100%|##########| 1/1 [00:00<00:00]");
                spec.OnErrorLine?.Invoke("mel:: 100%|##########| 8/8 [00:00<00:00]");
                spec.OnErrorLine?.Invoke("audio2exp:: 100%|##########| 1/1 [00:00<00:00]");
                spec.OnErrorLine?.Invoke("Face Renderer:: 50%|#####     | 5/10 [00:05<00:05]");
                if (PythonFails)
                {
                    return Task.FromResult(new ProcessResult(1, "", "Traceback: boom", TimeSpan.FromSeconds(1)));
                }

                if (!PythonSkipsOutput)
                {
                    var resultDirArg = spec.Arguments[Array.IndexOf(spec.Arguments.ToArray(), "--result_dir") + 1];
                    var outDir = Path.GetFullPath(Path.Combine(spec.WorkingDirectory!, resultDirArg));
                    Directory.CreateDirectory(outDir);
                    File.WriteAllBytes(Path.Combine(outDir, "2026_07_23_21.00.00.mp4"), [1, 2, 3]);
                }

                return Task.FromResult(new ProcessResult(0, "The generated video is named: ok.mp4", "", TimeSpan.FromSeconds(1)));
            }

            if (exe == "Ffmpeg.exe")
            {
                if (FfmpegFails)
                {
                    return Task.FromResult(new ProcessResult(1, "", "bad input", TimeSpan.FromSeconds(1)));
                }

                var target = Path.Combine(spec.WorkingDirectory!, "presenter", "presenter.mp4");
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.WriteAllBytes(target, [9, 9, 9]);
                return Task.FromResult(new ProcessResult(0, "", "", TimeSpan.FromSeconds(1)));
            }

            if (exe == "Ffprobe.exe")
            {
                const string json = """{"streams":[{"codec_type":"video","width":512,"height":512}]}""";
                return Task.FromResult(new ProcessResult(0, json, "", TimeSpan.FromSeconds(1)));
            }

            return Task.FromResult(new ProcessResult(0, "", "", TimeSpan.FromSeconds(1)));
        }
    }

    private static (SadTalkerPresenterEngine Engine, ScriptedRunner Runner, PresenterRequest Request, TempDir Temp) Setup(
        string engineId = "sadtalker-cuda")
    {
        var temp = new TempDir();
        var enginesRoot = Path.Combine(temp.Path, "presenters");
        Directory.CreateDirectory(Path.Combine(enginesRoot, engineId, "python"));
        Directory.CreateDirectory(Path.Combine(enginesRoot, engineId, "engine"));
        File.WriteAllText(Path.Combine(enginesRoot, engineId, "python", "python.exe"), "stub");

        var episodePaths = new EpisodePaths(Path.Combine(temp.Path, "episode"));
        episodePaths.EnsureCreated();
        episodePaths.EnsureWorkDirsCreated();

        var portrait = Path.Combine(temp.Path, "presentator foto é.png");
        File.WriteAllBytes(portrait, [1]);
        var audio = Path.Combine(episodePaths.AudioDir, "voice_norm.wav");
        File.WriteAllBytes(audio, [1]);

        var runner = new ScriptedRunner();
        var engine = new SadTalkerPresenterEngine(runner, new FakeToolLocator(temp.Path), new FakeManager(enginesRoot));
        var request = new PresenterRequest
        {
            Paths = episodePaths,
            BodyDuration = 30,
            BlinkSeed = "t",
            PortraitPath = portrait,
            AudioWavPath = audio,
            EngineId = engineId,
        };
        return (engine, runner, request, temp);
    }

    [Fact]
    public async Task RendersAndNormalizesTheVideo()
    {
        var (engine, runner, request, temp) = Setup();
        using var _ = temp;
        var reports = new List<double>();

        var track = await engine.RenderAsync(request, new SyncProgress(reports.Add), CancellationToken.None);

        var video = Assert.IsType<PresenterTrack.Video>(track);
        Assert.Equal("presenter/presenter.mp4", video.Mp4Path);
        Assert.Equal(512, video.Width);

        var python = runner.Specs[0];
        Assert.EndsWith("python.exe", python.ExePath, StringComparison.Ordinal);
        Assert.EndsWith("engine", python.WorkingDirectory, StringComparison.Ordinal);
        Assert.Contains("--still", python.Arguments);
        Assert.Contains("--enhancer", python.Arguments);
        Assert.All(
            python.Arguments.Where(a => a.StartsWith("../jobs/", StringComparison.Ordinal)),
            a => Assert.DoesNotContain("é", a, StringComparison.Ordinal));
        Assert.NotNull(python.EnvironmentOverrides);
        Assert.Equal("1", python.EnvironmentOverrides!["PYTHONNOUSERSITE"]);
        Assert.Equal("0", python.EnvironmentOverrides["CUDA_VISIBLE_DEVICES"]);
        Assert.Null(python.EnvironmentOverrides["PYTHONPATH"]);

        Assert.Contains(reports, r => r is > 0 and <= 0.85);
        Assert.Contains(1.0, reports);

        // The job folder is swept afterwards.
        Assert.Empty(Directory.GetDirectories(Path.Combine(temp.Path, "presenters", "sadtalker-cuda", "jobs")));
    }

    [Fact]
    public async Task CpuVariantUsesCpuFlags()
    {
        var (engine, runner, request, temp) = Setup("sadtalker-cpu");
        using var _ = temp;

        await engine.RenderAsync(request, null, CancellationToken.None);

        var python = runner.Specs[0];
        Assert.Contains("--cpu", python.Arguments);
        Assert.DoesNotContain("--enhancer", python.Arguments);
        Assert.Equal("-1", python.EnvironmentOverrides!["CUDA_VISIBLE_DEVICES"]);
    }

    [Fact]
    public async Task EngineFailureBecomesPresenterRenderException()
    {
        var (engine, runner, request, temp) = Setup();
        using var _ = temp;
        runner.PythonFails = true;

        var ex = await Assert.ThrowsAsync<PresenterRenderException>(() =>
            engine.RenderAsync(request, null, CancellationToken.None));

        Assert.Contains("boom", ex.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingOutputBecomesPresenterRenderException()
    {
        var (engine, runner, request, temp) = Setup();
        using var _ = temp;
        runner.PythonSkipsOutput = true;

        var ex = await Assert.ThrowsAsync<PresenterRenderException>(() =>
            engine.RenderAsync(request, null, CancellationToken.None));

        Assert.Contains("without producing", ex.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NormalizeFailureBecomesPresenterRenderException()
    {
        var (engine, runner, request, temp) = Setup();
        using var _ = temp;
        runner.FfmpegFails = true;

        var ex = await Assert.ThrowsAsync<PresenterRenderException>(() =>
            engine.RenderAsync(request, null, CancellationToken.None));

        Assert.Contains("normalize failed", ex.Detail, StringComparison.Ordinal);
    }

    private sealed class SyncProgress(Action<double> handler) : IProgress<double>
    {
        public void Report(double value) => handler(value);
    }
}

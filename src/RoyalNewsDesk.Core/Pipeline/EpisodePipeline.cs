using System.Text.Json;
using RoyalNewsDesk.Core.Audio;
using RoyalNewsDesk.Core.Graphics;
using RoyalNewsDesk.Core.LipSync;
using RoyalNewsDesk.Core.Models;
using RoyalNewsDesk.Core.Processes;
using RoyalNewsDesk.Core.Script;
using RoyalNewsDesk.Core.Storage;
using RoyalNewsDesk.Core.Subtitles;
using RoyalNewsDesk.Core.Tools;
using RoyalNewsDesk.Core.Tts;
using RoyalNewsDesk.Core.Video;
using RoyalNewsDesk.Core.VoiceModels;

namespace RoyalNewsDesk.Core.Pipeline;

/// <summary>
/// Runs the whole production, one step at a time, reporting structured
/// progress. Fails fast; a full rebuild of the work folder every run keeps
/// the behavior predictable.
/// </summary>
public sealed class EpisodePipeline(
    IProcessRunner runner,
    IToolLocator locator,
    IVoiceModelManager voiceManager,
    IEpisodeStore store,
    ITtsEngine ttsEngine,
    ILipSyncEngine lipSyncEngine,
    string assetsDir)
{
    private const long RequiredFreeBytes = 2L * 1024 * 1024 * 1024;

    public async Task<ProduceResult> ProduceAsync(
        Episode episode,
        AppSettings settings,
        ProduceOptions options,
        IProgress<StepProgress> progress,
        CancellationToken ct)
    {
        foreach (var step in Enum.GetValues<PipelineStepId>())
        {
            progress.Report(new StepProgress(step, StepState.Pending));
        }

        var paths = store.PathsFor(episode.Id);
        var warnings = new List<PipelineWarning>();
        var brand = BrandStyle.From(settings.Branding);
        using var fonts = new FontCatalog(Path.Combine(assetsDir, "fonts"));
        var gfx = new BroadcastGraphics(fonts);
        var composer = new VideoComposer(runner, locator);

        SpeechPlan plan = null!;
        Timeline timeline = null!;
        IReadOnlyList<SubtitleCue> cues = null!;
        MouthCueTrack mouthTrack = null!;
        var tickerContentWidth = 0;
        var outputName = "";

        // CheckTools
        await RunStepAsync(PipelineStepId.CheckTools, progress, ct, async () =>
        {
            var health = await new ToolHealthCheck(locator, runner).CheckAllAsync(ct).ConfigureAwait(false);
            var broken = health.Where(h => !h.Ok).ToList();
            if (broken.Count > 0)
            {
                throw new PipelineException(
                    PipelineStepId.CheckTools,
                    PipelineErrorCode.ToolMissing,
                    string.Join("; ", broken.Select(b => b.Tool + ": " + b.Detail)));
            }

            if (!voiceManager.IsInstalled(episode.VoiceId))
            {
                throw new PipelineException(
                    PipelineStepId.CheckTools,
                    PipelineErrorCode.VoiceModelMissing,
                    episode.VoiceId);
            }
        }).ConfigureAwait(false);

        // PrepareEpisode
        await RunStepAsync(PipelineStepId.PrepareEpisode, progress, ct, () =>
        {
            var drive = Path.GetPathRoot(paths.Root);
            if (drive is not null && new DriveInfo(drive).AvailableFreeSpace < RequiredFreeBytes)
            {
                throw new PipelineException(
                    PipelineStepId.PrepareEpisode,
                    PipelineErrorCode.DiskFull,
                    "less than 2 GB free");
            }

            if (Directory.Exists(paths.WorkDir))
            {
                Directory.Delete(paths.WorkDir, recursive: true);
            }

            paths.EnsureCreated();
            paths.EnsureWorkDirsCreated();

            plan = ScriptParser.Plan(episode, paths.ImagesDir);
            warnings.AddRange(plan.Warnings);

            foreach (var font in Directory.EnumerateFiles(Path.Combine(assetsDir, "fonts"), "*.ttf"))
            {
                File.Copy(font, Path.Combine(paths.FontsDir, Path.GetFileName(font)), overwrite: true);
            }

            return Task.CompletedTask;
        }).ConfigureAwait(false);

        // Voice
        await RunStepAsync(PipelineStepId.Voice, progress, ct, async () =>
        {
            var sentences = plan.AllSentences
                .Select((s, i) => new TtsSentence(
                    i,
                    s.Text,
                    Path.Combine(paths.SentencesDir, Formatting.Inv.F($"s{i:0000}.wav"))))
                .ToList();

            var voiceProgress = new Progress<double>(f => progress.Report(new StepProgress(
                PipelineStepId.Voice,
                StepState.Running,
                Fraction: f,
                ItemIndex: Math.Min(sentences.Count, (int)Math.Round(f * sentences.Count)),
                ItemCount: sentences.Count)));

            var spoken = await ttsEngine.SynthesizeAsync(
                new TtsBatch(
                    sentences,
                    voiceManager.GetModelPath(episode.VoiceId),
                    voiceManager.GetConfigPath(episode.VoiceId),
                    options.ReadingSpeed),
                voiceProgress,
                ct).ConfigureAwait(false);

            var assembled = VoiceTrackAssembler.Assemble(
                plan,
                spoken.ToDictionary(s => s.Ordinal),
                Path.Combine(paths.AudioDir, "voice_body.wav"));

            timeline = new Timeline(
                VoiceTrackAssembler.IntroDuration,
                assembled.Duration,
                VoiceTrackAssembler.OutroDuration,
                assembled.Segments,
                assembled.Sentences);

            var normalizer = new LoudnessNormalizer(runner, locator);
            var normalized = await normalizer.NormalizeAsync(
                Path.Combine(paths.AudioDir, "voice_body.wav"),
                Path.Combine(paths.AudioDir, "voice_norm.wav"),
                ct).ConfigureAwait(false);
            if (!normalized)
            {
                warnings.Add(new PipelineWarning("W601", null, "loudness measurement failed; used plain gain"));
            }

            MasterAudioMixer.Mix(
                timeline,
                Path.Combine(paths.AudioDir, "voice_norm.wav"),
                Path.Combine(assetsDir, "audio", "intro-jingle.wav"),
                Path.Combine(assetsDir, "audio", "outro-sting.wav"),
                Path.Combine(paths.AudioDir, "master_audio.wav"));

            cues = SrtWriter.BuildCues(timeline);

            await File.WriteAllTextAsync(
                Path.Combine(paths.Root, "timeline.json"),
                JsonSerializer.Serialize(timeline, JsonDefaults.Options),
                ct).ConfigureAwait(false);
        }).ConfigureAwait(false);

        // LipSync
        await RunStepAsync(PipelineStepId.LipSync, progress, ct, async () =>
        {
            var transcript = Path.Combine(paths.VisemeDir, "transcript.txt");
            await File.WriteAllLinesAsync(transcript, plan.AllSentences.Select(s => s.Text), ct)
                .ConfigureAwait(false);

            var lipProgress = new Progress<double>(f => progress.Report(new StepProgress(
                PipelineStepId.LipSync, StepState.Running, Fraction: f)));

            try
            {
                mouthTrack = await lipSyncEngine.AnalyzeAsync(
                    Path.Combine(paths.AudioDir, "voice_body.wav"),
                    transcript,
                    Path.Combine(paths.VisemeDir, "visemes.json"),
                    lipProgress,
                    ct).ConfigureAwait(false);
            }
            catch (ToolExecutionException ex)
            {
                warnings.Add(new PipelineWarning("W701", null, ex.Detail));
                mouthTrack = FallbackVisemes.FromSentences(
                    timeline.Sentences.Select(s => (timeline.ToBodyLocal(s.Start), timeline.ToBodyLocal(s.End))),
                    timeline.BodyDuration);
            }
        }).ConfigureAwait(false);

        // Graphics
        await RunStepAsync(PipelineStepId.Graphics, progress, ct, () =>
        {
            using var rasterizer = new SvgRasterizer();
            RasterizeSvg(rasterizer, Path.Combine(assetsDir, "studio", "background.svg"), Path.Combine(paths.GfxDir, "studio_back.png"), 1920, 1080, opaque: true);
            RasterizeSvg(rasterizer, Path.Combine(assetsDir, "studio", "desk-front.svg"), Path.Combine(paths.GfxDir, "desk_front.png"), 1920, 280, opaque: false);

            gfx.RenderDeskBrand(Path.Combine(paths.GfxDir, "desk_brand.png"), brand);
            gfx.RenderTickerBar(Path.Combine(paths.GfxDir, "ticker_bar.png"));
            gfx.RenderTickerBlock(Path.Combine(paths.GfxDir, "ticker_block.png"), brand);
            gfx.RenderLogoBug(Path.Combine(paths.GfxDir, "logo_bug.png"), brand);

            var tickerItems = episode.TickerItems.Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
            if (tickerItems.Count == 0)
            {
                tickerItems = plan.Segments
                    .Where(s => s.Headline is not null)
                    .Select(s => s.Headline!)
                    .ToList();
            }

            tickerContentWidth = gfx.RenderTickerStrip(Path.Combine(paths.GfxDir, "ticker_strip.png"), tickerItems, brand);

            foreach (var segment in timeline.Segments)
            {
                if (segment.HasLowerThird)
                {
                    gfx.RenderLowerThird(
                        Path.Combine(paths.GfxDir, Formatting.Inv.F($"lt_s{segment.Index:00}.png")),
                        segment.Headline!,
                        brand);
                }

                if (segment.HasPanel)
                {
                    gfx.RenderImagePanel(
                        Path.Combine(paths.GfxDir, Formatting.Inv.F($"panel_s{segment.Index:00}.png")),
                        Path.Combine(paths.ImagesDir, segment.ImageFile!),
                        brand);
                }
            }

            gfx.RenderTitleCard(Path.Combine(paths.GfxDir, "intro_card.png"), plan.Title, brand);
            gfx.RenderOutroCard(Path.Combine(paths.GfxDir, "outro_card.png"), brand);
            return Task.CompletedTask;
        }).ConfigureAwait(false);

        // AnchorAnimation
        await RunStepAsync(PipelineStepId.AnchorAnimation, progress, ct, () =>
        {
            var blinks = BlinkScheduler.Schedule(plan.Title, timeline.BodyDuration);
            var intervals = AnchorTimelineBuilder.Build(mouthTrack, blinks, timeline.BodyDuration);
            AnchorStateRenderer.RenderAll(Path.Combine(assetsDir, "anchor"), paths.AnchorDir);
            AnchorTimelineBuilder.WriteFfconcat(Path.Combine(paths.AnchorDir, "anchor.ffconcat"), intervals);
            return Task.CompletedTask;
        }).ConfigureAwait(false);

        // Assemble
        await RunStepAsync(PipelineStepId.Assemble, progress, ct, async () =>
        {
            if (options.BurnInSubtitles)
            {
                SrtWriter.WriteAss(Path.Combine(paths.SubDir, "body.ass"), cues, timeline);
            }

            await composer.RenderCardAsync(
                paths.WorkDir, "gfx/intro_card.png", "parts/00_intro.mp4",
                timeline.IntroDuration, options.HigherQuality, ct).ConfigureAwait(false);
            await composer.RenderCardAsync(
                paths.WorkDir, "gfx/outro_card.png", "parts/20_outro.mp4",
                timeline.OutroDuration, options.HigherQuality, ct).ConfigureAwait(false);

            var bodyPlan = FiltergraphBuilder.Build(
                timeline,
                tickerContentWidth,
                options.StudioAmbience,
                options.BurnInSubtitles);
            var bodyProgress = new Progress<double>(f => progress.Report(new StepProgress(
                PipelineStepId.Assemble, StepState.Running, Fraction: 0.1 + f * 0.8)));
            await composer.RenderBodyAsync(paths.WorkDir, bodyPlan, options.HigherQuality, bodyProgress, ct)
                .ConfigureAwait(false);

            outputName = BuildOutputName(plan.Title, episode.CreatedUtc);
            await composer.FinalMuxAsync(
                paths.WorkDir,
                "../output/" + outputName,
                options.HigherQuality,
                ct).ConfigureAwait(false);
        }).ConfigureAwait(false);

        // Subtitles
        await RunStepAsync(PipelineStepId.Subtitles, progress, ct, () =>
        {
            SrtWriter.WriteSrt(Path.Combine(paths.OutDir, Path.ChangeExtension(outputName, ".srt")), cues);
            return Task.CompletedTask;
        }).ConfigureAwait(false);

        // Thumbnail
        await RunStepAsync(PipelineStepId.Thumbnail, progress, ct, () =>
        {
            var firstImage = timeline.Segments.FirstOrDefault(s => s.ImageFile is not null)?.ImageFile;
            gfx.RenderThumbnail(
                Path.Combine(paths.OutDir, "thumbnail.png"),
                plan.Title,
                firstImage is null ? null : Path.Combine(paths.ImagesDir, firstImage),
                brand);
            return Task.CompletedTask;
        }).ConfigureAwait(false);

        // Export
        ProduceResult result = null!;
        await RunStepAsync(PipelineStepId.Export, progress, ct, async () =>
        {
            var videoPath = Path.Combine(paths.OutDir, outputName);
            var validator = new OutputValidator(runner, locator);
            var issues = await validator.ValidateAsync(videoPath, timeline, ct).ConfigureAwait(false);
            if (issues.Count > 0)
            {
                throw new PipelineException(
                    PipelineStepId.Export,
                    PipelineErrorCode.OutputInvalid,
                    string.Join("; ", issues));
            }

            var exportDir = Path.Combine(settings.OutputFolder, SanitizeFolderName(plan.Title));
            Directory.CreateDirectory(exportDir);
            foreach (var file in Directory.EnumerateFiles(paths.OutDir))
            {
                File.Copy(file, Path.Combine(exportDir, Path.GetFileName(file)), overwrite: true);
            }

            if (!options.KeepWorkFiles && Directory.Exists(paths.WorkDir))
            {
                Directory.Delete(paths.WorkDir, recursive: true);
            }

            var duration = TimeSpan.FromSeconds(timeline.TotalDuration);
            result = new ProduceResult(
                Path.Combine(exportDir, outputName),
                Path.Combine(exportDir, Path.ChangeExtension(outputName, ".srt")),
                Path.Combine(exportDir, "thumbnail.png"),
                duration,
                warnings);
        }).ConfigureAwait(false);

        return result;
    }

    private static async Task RunStepAsync(
        PipelineStepId step,
        IProgress<StepProgress> progress,
        CancellationToken ct,
        Func<Task> action)
    {
        progress.Report(new StepProgress(step, StepState.Running));
        try
        {
            ct.ThrowIfCancellationRequested();
            await action().ConfigureAwait(false);
            progress.Report(new StepProgress(step, StepState.Succeeded));
        }
        catch (OperationCanceledException)
        {
            progress.Report(new StepProgress(step, StepState.Canceled, Error: PipelineErrorCode.Canceled));
            throw;
        }
        catch (PipelineException ex)
        {
            progress.Report(new StepProgress(step, StepState.Failed, Error: ex.Code, TechnicalDetail: ex.TechnicalDetail));
            throw;
        }
        catch (Exception ex)
        {
            var (code, detail) = MapException(ex);
            progress.Report(new StepProgress(step, StepState.Failed, Error: code, TechnicalDetail: detail));
            throw new PipelineException(step, code, detail, ex);
        }
    }

    private static (PipelineErrorCode Code, string Detail) MapException(Exception ex) => ex switch
    {
        ScriptEmptyException => (PipelineErrorCode.ScriptEmpty, ex.Message),
        ToolExecutionException tool => (PipelineErrorCode.ToolFailed, tool.Tool + ": " + tool.Detail),
        ProcessStartException { Reason: ProcessStartFailure.VirusBlocked or ProcessStartFailure.AccessDenied } start =>
            (PipelineErrorCode.ToolBlocked, start.Message),
        ProcessStartException start => (PipelineErrorCode.ToolMissing, start.Message),
        IOException io when io.Message.Contains("space", StringComparison.OrdinalIgnoreCase) =>
            (PipelineErrorCode.DiskFull, io.Message),
        UnauthorizedAccessException => (PipelineErrorCode.AccessDenied, ex.Message),
        _ => (PipelineErrorCode.Unknown, ex.Message),
    };

    private static void RasterizeSvg(SvgRasterizer rasterizer, string svgPath, string pngPath, int width, int height, bool opaque)
    {
        using var surface = SkiaSharp.SKSurface.Create(
            new SkiaSharp.SKImageInfo(width, height, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Premul));
        surface.Canvas.Clear(opaque ? SkiaSharp.SKColors.Black : SkiaSharp.SKColors.Transparent);
        rasterizer.Draw(surface.Canvas, svgPath, new SkiaSharp.SKRect(0, 0, width, height));
        using var image = surface.Snapshot();
        using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(pngPath);
        data.SaveTo(stream);
    }

    private static string BuildOutputName(string title, DateTime createdUtc)
    {
        return SanitizeSlug(title) + "_" + createdUtc.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture) + ".mp4";
    }

    private static string SanitizeSlug(string title)
    {
        var slug = new string(title
            .Where(c => char.IsAsciiLetterOrDigit(c) || c == ' ' || c == '-')
            .ToArray())
            .Trim()
            .Replace(' ', '-');
        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }

        return string.IsNullOrEmpty(slug) ? "episode" : slug[..Math.Min(40, slug.Length)];
    }

    private static string SanitizeFolderName(string title)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var name = new string(title.Where(c => !invalid.Contains(c)).ToArray()).Trim();
        return string.IsNullOrEmpty(name) ? "Episode" : name[..Math.Min(60, name.Length)];
    }
}

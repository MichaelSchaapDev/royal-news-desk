using System.Text;
using RoyalNewsDesk.Core.Formatting;
using RoyalNewsDesk.Core.Presenters;

namespace RoyalNewsDesk.Core.Video;

/// <summary>Everything the body render needs to know about its inputs.</summary>
public sealed record BodyRenderPlan(
    IReadOnlyList<string> InputArguments,
    string FilterGraph,
    int TotalFrames);

/// <summary>
/// Emits the ffmpeg argument list and filter graph for the body render from
/// one input table, so stream indexes can never drift apart. All paths are
/// relative to the episode work directory with forward slashes.
/// </summary>
public static class FiltergraphBuilder
{
    public const int AnchorDefaultX = 560;
    public const int AnchorPanelX = 140;
    public const int AnchorY = 100;
    public const int LowerThirdX = 110;
    public const int LowerThirdRestY = 825;
    public const int PanelX = 1140;
    public const int PanelY = 150;
    public const int TickerY = 1010;
    public const double TickerSpeed = 110;

    // Correspondent frame for the photoreal presenter: video window inside a
    // styled frame, fixed position, clear of the image panels at x=1140.
    public const int PresenterFrameX = 180;
    public const int PresenterFrameY = 100;
    public const int PresenterFrameSize = 840;
    public const int PresenterVideoSize = 780;
    public const int PresenterVideoX = PresenterFrameX + 30;
    public const int PresenterVideoY = PresenterFrameY + 30;

    public static BodyRenderPlan Build(
        Timeline timeline,
        int tickerContentWidth,
        bool ambience,
        bool burnSubtitles,
        PresenterTrack presenter)
    {
        var bodyFrames = (int)Math.Round(timeline.BodyDuration * Timeline.Fps, MidpointRounding.AwayFromZero);
        var inputs = new List<string>();
        var graph = new StringBuilder();
        var inputIndex = 0;

        int AddImageInput(string relativePath, bool loop)
        {
            if (loop)
            {
                inputs.AddRange(["-framerate", "25", "-loop", "1", "-i", relativePath]);
            }
            else
            {
                inputs.AddRange(["-i", relativePath]);
            }

            return inputIndex++;
        }

        // Fixed inputs. [1] is the presenter in either form.
        var bg = AddImageInput("gfx/studio_back.png", loop: !ambience);
        if (presenter is PresenterTrack.Stills stillsTrack)
        {
            inputs.AddRange(["-f", "concat", "-safe", "0", "-i", stillsTrack.FfconcatPath]);
        }
        else
        {
            inputs.AddRange(["-i", ((PresenterTrack.Video)presenter).Mp4Path]);
        }

        var presenterInput = inputIndex++;
        var presenterFrame = presenter is PresenterTrack.Video
            ? AddImageInput("gfx/presenter_frame.png", loop: true)
            : -1;
        var desk = AddImageInput("gfx/desk_front.png", loop: true);
        var tickerBar = AddImageInput("gfx/ticker_bar.png", loop: true);
        var tickerStrip = AddImageInput("gfx/ticker_strip.png", loop: true);
        var tickerBlock = AddImageInput("gfx/ticker_block.png", loop: true);

        var lowerThirdInputs = new Dictionary<int, int>();
        var panelInputs = new Dictionary<int, int>();
        foreach (var segment in timeline.Segments)
        {
            if (segment.HasLowerThird)
            {
                lowerThirdInputs[segment.Index] = AddImageInput(
                    Inv.F($"gfx/lt_s{segment.Index:00}.png"), loop: true);
            }

            if (segment.HasPanel)
            {
                panelInputs[segment.Index] = AddImageInput(
                    Inv.F($"gfx/panel_s{segment.Index:00}.png"), loop: true);
            }
        }

        // Background: the duration master.
        if (ambience)
        {
            var zoomStep = 0.05 / Math.Max(1, bodyFrames);
            graph.Append(Inv.F($"[{bg}:v]scale=3840:2160,zoompan=z='min(1.0+{zoomStep:0.00000000}*on,1.05)':x='iw/2-(iw/zoom/2)':y='ih/2-(ih/zoom/2)':d={bodyFrames}:s=1920x1080:fps=25,format=yuv420p[bg];\n"));
        }
        else
        {
            graph.Append(Inv.F($"[{bg}:v]fps=25,format=yuv420p[bg];\n"));
        }

        if (presenter is PresenterTrack.Stills)
        {
            // The 2D anchor: stills sequence with breathing and the panel slide.
            graph.Append(Inv.F($"[{presenterInput}:v]fps=25,format=rgba[anchor];\n"));
            graph.Append("[bg][anchor]overlay=eval=frame:x='")
                .Append(AnchorXExpression(timeline))
                .Append("':y='")
                .Append(Inv.F($"{AnchorY}+2*sin(2*PI*t/7)"))
                .Append("'[v0];\n");
        }
        else
        {
            // Photoreal video: cover-fit into the correspondent window; tpad
            // clones the last frame so a slightly short clip never starves
            // the overlay before -frames:v truncates the render.
            graph.Append(Inv.F($"[{presenterInput}:v]fps=25,scale={PresenterVideoSize}:{PresenterVideoSize}:force_original_aspect_ratio=increase,crop={PresenterVideoSize}:{PresenterVideoSize},setsar=1,tpad=stop=-1:stop_mode=clone[pres];\n"));
            graph.Append(Inv.F($"[bg][pres]overlay=x={PresenterVideoX}:y={PresenterVideoY}[v0];\n"));
        }

        var current = "v0";
        var next = 1;

        string Chain(string filter)
        {
            var label = "v" + Inv.I(next++);
            graph.Append(filter.Replace("{in}", "[" + current + "]", StringComparison.Ordinal)
                .Replace("{out}", "[" + label + "]", StringComparison.Ordinal));
            current = label;
            return label;
        }

        if (presenter is PresenterTrack.Video)
        {
            Chain(Inv.F($"{{in}}[{presenterFrame}:v]overlay=x={PresenterFrameX}:y={PresenterFrameY}{{out}};\n"));
        }

        Chain(Inv.F($"{{in}}[{desk}:v]overlay=x=0:y=800{{out}};\n"));

        // Lower thirds: fade the alpha, slide up, show only inside the window.
        foreach (var segment in timeline.Segments.Where(s => s.HasLowerThird))
        {
            var start = timeline.ToBodyLocal(segment.LowerThirdStart);
            var end = timeline.ToBodyLocal(segment.LowerThirdEnd);
            var fadeOutStart = Math.Max(start, end - 0.3);
            var label = Inv.F($"lt{segment.Index}");
            graph.Append(Inv.F($"[{lowerThirdInputs[segment.Index]}:v]format=rgba,fade=t=in:st={start:0.000}:d=0.3:alpha=1,fade=t=out:st={fadeOutStart:0.000}:d=0.3:alpha=1[{label}];\n"));
            Chain(Inv.F($"{{in}}[{label}]overlay=eval=frame:x={LowerThirdX}:y='if(lt(t-{start:0.000},0.4),1080-{1080 - LowerThirdRestY}*min((t-{start:0.000})/0.4,1),{LowerThirdRestY})':enable='between(t,{start:0.000},{end:0.000})'{{out}};\n"));
        }

        // Image panels: plain fades, no slide.
        foreach (var segment in timeline.Segments.Where(s => s.HasPanel))
        {
            var start = timeline.ToBodyLocal(segment.PanelStart);
            var end = timeline.ToBodyLocal(segment.PanelEnd);
            var fadeOutStart = Math.Max(start, end - 0.3);
            var label = Inv.F($"pan{segment.Index}");
            graph.Append(Inv.F($"[{panelInputs[segment.Index]}:v]format=rgba,fade=t=in:st={start:0.000}:d=0.35:alpha=1,fade=t=out:st={fadeOutStart:0.000}:d=0.3:alpha=1[{label}];\n"));
            Chain(Inv.F($"{{in}}[{label}]overlay=x={PanelX}:y={PanelY}:enable='between(t,{start:0.000},{end:0.000})'{{out}};\n"));
        }

        // Ticker: bar, endless crawl, brand block masking the left edge.
        Chain(Inv.F($"{{in}}[{tickerBar}:v]overlay=x=0:y={TickerY}{{out}};\n"));
        Chain(Inv.F($"{{in}}[{tickerStrip}:v]overlay=eval=frame:x='-mod(t*{TickerSpeed:0.0},{tickerContentWidth})':y={TickerY}{{out}};\n"));
        Chain(Inv.F($"{{in}}[{tickerBlock}:v]overlay=x=0:y={TickerY}{{out}};\n"));

        if (burnSubtitles)
        {
            Chain("{in}subtitles=filename=sub/body.ass:fontsdir=gfx/fonts{out};\n");
        }

        graph.Append('[').Append(current).Append("]format=yuv420p[vout]\n");

        return new BodyRenderPlan(inputs, graph.ToString(), bodyFrames);
    }

    /// <summary>Piecewise anchor x: slides to the side while a panel shows.</summary>
    private static string AnchorXExpression(Timeline timeline)
    {
        var panels = timeline.Segments.Where(s => s.HasPanel).ToList();
        if (panels.Count == 0)
        {
            return Inv.I(AnchorDefaultX);
        }

        var expression = Inv.I(AnchorDefaultX);
        foreach (var panel in panels.OrderByDescending(p => p.PanelStart))
        {
            var slideInStart = timeline.ToBodyLocal(panel.PanelStart) - 0.5;
            var slideOutStart = timeline.ToBodyLocal(panel.PanelEnd);
            var inExpr = Inv.F($"if(lt(t,{slideInStart + 0.5:0.000}),{AnchorDefaultX}+({AnchorPanelX}-{AnchorDefaultX})*max(0,(t-{slideInStart:0.000}))/0.5,if(lt(t,{slideOutStart:0.000}),{AnchorPanelX},if(lt(t,{slideOutStart + 0.5:0.000}),{AnchorPanelX}+({AnchorDefaultX}-{AnchorPanelX})*(t-{slideOutStart:0.000})/0.5,{expression})))");
            expression = Inv.F($"if(gte(t,{slideInStart:0.000}),{inExpr},{expression})");
        }

        return expression;
    }
}

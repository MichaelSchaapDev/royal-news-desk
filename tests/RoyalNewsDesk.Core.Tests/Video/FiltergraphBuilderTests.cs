using RoyalNewsDesk.Core.Presenters;
using RoyalNewsDesk.Core.Video;

namespace RoyalNewsDesk.Core.Tests.Video;

public class FiltergraphBuilderTests
{
    private static Timeline SmallTimeline()
    {
        var segments = new List<SegmentTiming>
        {
            new(0, "Headline one", null, 4.0, 12.0, 4.5, 11.5, 0, 0),
            new(1, "Headline two", "seg-02.jpg", 12.0, 24.0, 12.5, 20.0, 12.4, 23.5),
        };
        var sentences = new List<SentenceTiming>
        {
            new(0, 0, "One.", 4.3, 8.0),
            new(1, 1, "Two.", 12.5, 20.0),
        };
        return new Timeline(4.0, 20.0, 8.0, segments, sentences);
    }

    private static readonly PresenterTrack StillsTrack = new PresenterTrack.Stills("gfx/anchor/anchor.ffconcat");

    private static readonly PresenterTrack VideoTrack = new PresenterTrack.Video("presenter/presenter.mp4", 512, 512);

    [Fact]
    public void StillsBranchKeepsAnchorBehavior()
    {
        var plan = FiltergraphBuilder.Build(SmallTimeline(), 4000, ambience: true, burnSubtitles: false, StillsTrack);

        Assert.Contains("gfx/anchor/anchor.ffconcat", plan.InputArguments);
        Assert.Contains("-f", plan.InputArguments);
        Assert.Contains("100+2*sin(2*PI*t/7)", plan.FilterGraph, StringComparison.Ordinal);
        Assert.Contains("560", plan.FilterGraph, StringComparison.Ordinal);
        Assert.DoesNotContain("presenter_frame", plan.FilterGraph, StringComparison.Ordinal);
        Assert.Equal(500, plan.TotalFrames); // 20 s body at 25 fps
    }

    [Fact]
    public void VideoBranchFramesThePresenter()
    {
        var plan = FiltergraphBuilder.Build(SmallTimeline(), 4000, ambience: true, burnSubtitles: false, VideoTrack);

        Assert.Contains("presenter/presenter.mp4", plan.InputArguments);
        Assert.Contains("gfx/presenter_frame.png", plan.InputArguments);
        Assert.DoesNotContain("anchor.ffconcat", string.Join(" ", plan.InputArguments), StringComparison.Ordinal);

        Assert.Contains(
            "scale=780:780:force_original_aspect_ratio=increase,crop=780:780",
            plan.FilterGraph,
            StringComparison.Ordinal);
        Assert.Contains("tpad=stop=-1:stop_mode=clone", plan.FilterGraph, StringComparison.Ordinal);
        Assert.Contains("overlay=x=210:y=130", plan.FilterGraph, StringComparison.Ordinal);
        Assert.Contains("overlay=x=180:y=100", plan.FilterGraph, StringComparison.Ordinal);
        Assert.DoesNotContain("2*sin(2*PI*t/7)", plan.FilterGraph, StringComparison.Ordinal);
        Assert.Equal(500, plan.TotalFrames);
    }

    [Fact]
    public void BothBranchesKeepPanelsAndTicker()
    {
        foreach (var presenter in new[] { StillsTrack, VideoTrack })
        {
            var plan = FiltergraphBuilder.Build(SmallTimeline(), 4000, ambience: false, burnSubtitles: false, presenter);

            Assert.Contains("overlay=x=1140:y=150", plan.FilterGraph, StringComparison.Ordinal);
            Assert.Contains("-mod(t*110.0,4000)", plan.FilterGraph, StringComparison.Ordinal);
            Assert.Contains("format=yuv420p[vout]", plan.FilterGraph, StringComparison.Ordinal);
        }
    }
}

using RoyalNewsDesk.Core.Graphics;
using RoyalNewsDesk.Core.LipSync;

namespace RoyalNewsDesk.Core.Presenters;

/// <summary>
/// The 2D anchor: mouth cues plus deterministic blinks become a frame-exact
/// pose list rendered as an ffconcat stills sequence.
/// </summary>
public sealed class AnimatedPresenterEngine(string assetsDir) : IPresenterEngine
{
    public Task<PresenterTrack> RenderAsync(
        PresenterRequest request,
        IProgress<double>? progress,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var mouths = request.MouthCues
            ?? throw new ArgumentException("The animated presenter needs mouth cues.", nameof(request));

        var blinks = BlinkScheduler.Schedule(request.BlinkSeed, request.BodyDuration);
        var intervals = AnchorTimelineBuilder.Build(mouths, blinks, request.BodyDuration);
        AnchorStateRenderer.RenderAll(Path.Combine(assetsDir, "anchor"), request.Paths.AnchorDir);
        AnchorTimelineBuilder.WriteFfconcat(
            Path.Combine(request.Paths.AnchorDir, "anchor.ffconcat"),
            intervals);

        progress?.Report(1);
        return Task.FromResult<PresenterTrack>(new PresenterTrack.Stills("gfx/anchor/anchor.ffconcat"));
    }
}

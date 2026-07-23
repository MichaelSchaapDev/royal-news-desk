namespace RoyalNewsDesk.Core.Presenters;

/// <summary>
/// What the Presenter step hands to the body render. Paths are relative to
/// the episode work directory with forward slashes.
/// </summary>
public abstract record PresenterTrack
{
    private PresenterTrack()
    {
    }

    /// <summary>A stills sequence (the 2D anchor), consumed as an ffconcat list.</summary>
    public sealed record Stills(string FfconcatPath) : PresenterTrack;

    /// <summary>A normalized talking-head video (25 fps h264).</summary>
    public sealed record Video(string Mp4Path, int Width, int Height) : PresenterTrack;
}

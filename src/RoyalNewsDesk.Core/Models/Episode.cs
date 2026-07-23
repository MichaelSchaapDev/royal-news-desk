namespace RoyalNewsDesk.Core.Models;

/// <summary>An episode project as stored in episode.json.</summary>
public sealed class Episode
{
    public int SchemaVersion { get; set; } = 1;

    public required string Id { get; set; }

    public string Title { get; set; } = "";

    public DateTime CreatedUtc { get; set; }

    /// <summary>Piper voice id, e.g. "en_GB-cori-high".</summary>
    public string VoiceId { get; set; } = "";

    /// <summary>How this episode is presented. Older files load as Animated.</summary>
    public Presenters.PresenterStyle PresenterStyle { get; set; } = Presenters.PresenterStyle.Animated;

    public List<Segment> Segments { get; set; } = [];

    /// <summary>Headlines for the ticker. Empty falls back to segment headlines.</summary>
    public List<string> TickerItems { get; set; } = [];
}

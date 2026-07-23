namespace RoyalNewsDesk.Core.Video;

/// <summary>One spoken sentence on the master timeline (seconds).</summary>
public sealed record SentenceTiming(
    int SegmentIndex,
    int Ordinal,
    string Text,
    double Start,
    double End);

/// <summary>One story segment on the master timeline, with its overlay windows.</summary>
public sealed record SegmentTiming(
    int Index,
    string? Headline,
    string? ImageFile,
    double Start,
    double End,
    double LowerThirdStart,
    double LowerThirdEnd,
    double PanelStart,
    double PanelEnd)
{
    public bool HasLowerThird => Headline is not null && LowerThirdEnd > LowerThirdStart;

    public bool HasPanel => ImageFile is not null && PanelEnd > PanelStart;
}

/// <summary>
/// The single source of truth for when everything happens. All times are
/// master-timeline seconds; the body part is rendered standalone, so its
/// consumers subtract the intro via <see cref="ToBodyLocal"/>.
/// </summary>
public sealed record Timeline(
    double IntroDuration,
    double BodyDuration,
    double OutroDuration,
    IReadOnlyList<SegmentTiming> Segments,
    IReadOnlyList<SentenceTiming> Sentences)
{
    public const int Fps = 25;

    public double BodyStart => IntroDuration;

    public double BodyEnd => IntroDuration + BodyDuration;

    public double OutroStart => BodyEnd;

    public double TotalDuration => BodyEnd + OutroDuration;

    public double ToBodyLocal(double masterTime) => masterTime - IntroDuration;

    /// <summary>Floor to the 25 fps grid (for window starts).</summary>
    public static double FloorFrame(double seconds) => Math.Floor(seconds * Fps) / Fps;

    /// <summary>Ceil to the 25 fps grid (for window ends).</summary>
    public static double CeilFrame(double seconds) => Math.Ceiling(seconds * Fps) / Fps;
}

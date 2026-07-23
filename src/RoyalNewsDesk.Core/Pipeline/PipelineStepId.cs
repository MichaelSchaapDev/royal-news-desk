namespace RoyalNewsDesk.Core.Pipeline;

/// <summary>
/// The user-visible pipeline steps, in run order. The app localizes these;
/// Core never produces user-facing text for them.
/// </summary>
public enum PipelineStepId
{
    CheckTools,
    PrepareEpisode,
    Voice,
    LipSync,
    Graphics,
    Presenter,
    Assemble,
    Subtitles,
    Thumbnail,
    Export,
}

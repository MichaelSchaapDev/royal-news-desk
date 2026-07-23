namespace RoyalNewsDesk.Core.Presenters;

/// <summary>
/// A presenter render failed. Deliberately its own type: the pipeline
/// catches exactly this to fall back to the animated presenter, and the
/// downloaded engines stay out of the bundled-tool exception family.
/// </summary>
public sealed class PresenterRenderException : Exception
{
    public PresenterRenderException(string engineId, string detail, Exception? inner = null)
        : base(engineId + " failed: " + detail, inner)
    {
        EngineId = engineId;
        Detail = detail;
    }

    public string EngineId { get; }

    public string Detail { get; }
}

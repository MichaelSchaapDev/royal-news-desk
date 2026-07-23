namespace RoyalNewsDesk.Core.Presenters;

/// <summary>How the on-screen presenter is produced. Chosen per episode.</summary>
public enum PresenterStyle
{
    /// <summary>The 2D animated news reader (stills sequence).</summary>
    Animated,

    /// <summary>An AI talking-head video made from one photo.</summary>
    Photoreal,
}

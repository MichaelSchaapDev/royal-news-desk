using RoyalNewsDesk.Core.LipSync;
using RoyalNewsDesk.Core.Storage;

namespace RoyalNewsDesk.Core.Presenters;

/// <summary>Everything either presenter engine could need; each reads its slice.</summary>
public sealed record PresenterRequest
{
    public required EpisodePaths Paths { get; init; }

    /// <summary>Seconds the track must cover.</summary>
    public required double BodyDuration { get; init; }

    /// <summary>Deterministic seed for blinks (the episode title).</summary>
    public required string BlinkSeed { get; init; }

    /// <summary>Mouth cues; the animated engine requires these.</summary>
    public MouthCueTrack? MouthCues { get; init; }

    /// <summary>Absolute path to the portrait photo (photoreal only).</summary>
    public string? PortraitPath { get; init; }

    /// <summary>Absolute path to the driving voice wav (photoreal only).</summary>
    public string? AudioWavPath { get; init; }

    /// <summary>Engine id from the presenter catalog (photoreal only).</summary>
    public string? EngineId { get; init; }
}

/// <summary>Produces the on-screen presenter track for one episode.</summary>
public interface IPresenterEngine
{
    /// <exception cref="PresenterRenderException">The render failed; the caller may fall back.</exception>
    Task<PresenterTrack> RenderAsync(PresenterRequest request, IProgress<double>? progress, CancellationToken ct);
}

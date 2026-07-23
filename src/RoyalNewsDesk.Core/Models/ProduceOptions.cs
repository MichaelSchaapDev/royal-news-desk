namespace RoyalNewsDesk.Core.Models;

/// <summary>Per-run pipeline options, derived from settings at produce time.</summary>
public sealed record ProduceOptions
{
    public bool KeepWorkFiles { get; init; }

    public bool BurnInSubtitles { get; init; }

    public bool StudioAmbience { get; init; } = true;

    public bool HigherQuality { get; init; }

    public double ReadingSpeed { get; init; } = 1.0;

    public string PhotorealEngineId { get; init; } = "sadtalker-cpu";

    public string? PhotorealPortraitPath { get; init; }

    public static ProduceOptions From(AppSettings settings) => new()
    {
        KeepWorkFiles = settings.KeepWorkFiles,
        BurnInSubtitles = settings.BurnInSubtitles,
        StudioAmbience = settings.StudioAmbience,
        HigherQuality = settings.HigherQuality,
        ReadingSpeed = settings.ReadingSpeed,
        PhotorealEngineId = settings.PhotorealEngineId,
        PhotorealPortraitPath = settings.PhotorealPortraitPath,
    };
}

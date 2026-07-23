namespace RoyalNewsDesk.Core.Models;

public enum AppTheme
{
    Light,
    Dark,
}

/// <summary>User settings as stored in settings.json.</summary>
public sealed class AppSettings
{
    public int SchemaVersion { get; set; } = 1;

    /// <summary>UI language: "nl" or "en".</summary>
    public string Language { get; set; } = "nl";

    public AppTheme Theme { get; set; } = AppTheme.Light;

    public string VoiceId { get; set; } = "en_GB-cori-high";

    /// <summary>Piper length scale. 1.0 is normal; higher is slower speech.</summary>
    public double ReadingSpeed { get; set; } = 1.0;

    public Branding Branding { get; set; } = new();

    /// <summary>Where finished videos land. Empty means the default Videos folder.</summary>
    public string OutputFolder { get; set; } = "";

    public bool KeepWorkFiles { get; set; }

    public bool BurnInSubtitles { get; set; }

    /// <summary>Slow push-in on the studio background.</summary>
    public bool StudioAmbience { get; set; } = true;

    /// <summary>Slower x264 preset for slightly better quality.</summary>
    public bool HigherQuality { get; set; }

    /// <summary>Photoreal engine id from the presenter catalog.</summary>
    public string PhotorealEngineId { get; set; } = "sadtalker-cpu";

    /// <summary>Absolute path to the presenter portrait photo; null until chosen.</summary>
    public string? PhotorealPortraitPath { get; set; }
}

namespace RoyalNewsDesk.Core.Storage;

/// <summary>
/// Folder layout inside one episode project. Short generated names keep full
/// paths well under Windows limits.
/// </summary>
public sealed class EpisodePaths(string root)
{
    public string Root { get; } = root;

    public string EpisodeFile => Path.Combine(Root, "episode.json");

    /// <summary>User images, copied in so the project is self-contained.</summary>
    public string ImagesDir => Path.Combine(Root, "images");

    /// <summary>Rebuilt on every produce run; safe to delete.</summary>
    public string WorkDir => Path.Combine(Root, "work");

    public string AudioDir => Path.Combine(WorkDir, "audio");

    public string SentencesDir => Path.Combine(AudioDir, "sentences");

    public string VisemeDir => Path.Combine(WorkDir, "viseme");

    public string GfxDir => Path.Combine(WorkDir, "gfx");

    public string AnchorDir => Path.Combine(GfxDir, "anchor");

    public string FontsDir => Path.Combine(GfxDir, "fonts");

    public string PartsDir => Path.Combine(WorkDir, "parts");

    /// <summary>Photoreal presenter intermediates (raw and normalized video).</summary>
    public string PresenterDir => Path.Combine(WorkDir, "presenter");

    public string SubDir => Path.Combine(WorkDir, "sub");

    /// <summary>Deliverables, copied to the user's output folder on export.</summary>
    public string OutDir => Path.Combine(Root, "output");

    public string LogsDir => Path.Combine(Root, "logs");

    public void EnsureCreated()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(ImagesDir);
        Directory.CreateDirectory(OutDir);
        Directory.CreateDirectory(LogsDir);
    }

    public void EnsureWorkDirsCreated()
    {
        Directory.CreateDirectory(SentencesDir);
        Directory.CreateDirectory(VisemeDir);
        Directory.CreateDirectory(AnchorDir);
        Directory.CreateDirectory(FontsDir);
        Directory.CreateDirectory(PartsDir);
        Directory.CreateDirectory(SubDir);
        Directory.CreateDirectory(PresenterDir);
    }
}

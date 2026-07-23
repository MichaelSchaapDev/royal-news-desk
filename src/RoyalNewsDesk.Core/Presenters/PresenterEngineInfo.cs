namespace RoyalNewsDesk.Core.Presenters;

/// <summary>One downloadable file of a presenter engine bundle.</summary>
public sealed class PresenterEngineFile
{
    public required string FileName { get; init; }

    public required string Sha256 { get; init; }

    public required long SizeBytes { get; init; }

    /// <summary>Download locations, tried in order.</summary>
    public required IReadOnlyList<string> Urls { get; init; }

    /// <summary>Zip archives are verified, unpacked into the engine folder, then deleted.</summary>
    public bool Extract { get; init; }
}

/// <summary>A photoreal presenter engine as listed in the embedded catalog.</summary>
public sealed class PresenterEngineInfo
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public required string License { get; init; }

    /// <summary>Relative path of the runnable inside the engine folder.</summary>
    public required string Entrypoint { get; init; }

    /// <summary>Approximate size on disk after extraction, for the preflight check.</summary>
    public long ExtractedSizeBytes { get; init; }

    /// <summary>True when the engine needs an NVIDIA card; shown in the UI.</summary>
    public bool RequiresNvidiaGpu { get; init; }

    public required IReadOnlyList<PresenterEngineFile> Files { get; init; }

    public long TotalBytes => Files.Sum(f => f.SizeBytes);
}

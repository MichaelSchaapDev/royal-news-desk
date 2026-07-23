namespace RoyalNewsDesk.Core.VoiceModels;

/// <summary>One downloadable file of a voice model.</summary>
public sealed class VoiceFile
{
    public required string FileName { get; init; }

    public required string Sha256 { get; init; }

    public required long SizeBytes { get; init; }

    /// <summary>Download locations, tried in order.</summary>
    public required IReadOnlyList<string> Urls { get; init; }
}

/// <summary>A Piper voice as listed in the embedded catalog.</summary>
public sealed class VoiceInfo
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public required string Quality { get; init; }

    public required int SampleRate { get; init; }

    public required string License { get; init; }

    public required IReadOnlyList<VoiceFile> Files { get; init; }

    public long TotalBytes => Files.Sum(f => f.SizeBytes);
}

namespace RoyalNewsDesk.Core.Models;

/// <summary>What a finished produce run delivers.</summary>
public sealed record ProduceResult(
    string VideoPath,
    string SubtitlePath,
    string ThumbnailPath,
    TimeSpan Duration,
    IReadOnlyList<PipelineWarning> Warnings);

/// <summary>A non-fatal problem found while producing, shown to the user afterwards.</summary>
/// <param name="Code">Stable warning code, e.g. "W201". The app maps it to localized text.</param>
/// <param name="LineNumber">1-based script line the warning points at, if any.</param>
/// <param name="Detail">Free-form technical detail (file name, clamped value), never localized.</param>
public sealed record PipelineWarning(string Code, int? LineNumber, string? Detail);

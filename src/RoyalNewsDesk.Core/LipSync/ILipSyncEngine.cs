namespace RoyalNewsDesk.Core.LipSync;

/// <summary>Audio to mouth shapes. V1 is Rhubarb; a paid avatar provider replaces this later.</summary>
public interface ILipSyncEngine
{
    /// <param name="wavPath">Mono speech wav (the pre-loudnorm voice track).</param>
    /// <param name="transcriptPath">Plain-text file with the exact spoken text; improves accuracy.</param>
    /// <param name="outputJsonPath">Where the raw analysis lands (kept for debugging).</param>
    Task<MouthCueTrack> AnalyzeAsync(
        string wavPath,
        string transcriptPath,
        string outputJsonPath,
        IProgress<double>? progress,
        CancellationToken ct);
}

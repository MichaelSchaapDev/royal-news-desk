namespace RoyalNewsDesk.Core.VoiceModels;

/// <summary>Overall download progress across all files of one voice.</summary>
public sealed record DownloadProgress(string FileName, long BytesReceived, long TotalBytes)
{
    public double Fraction => TotalBytes > 0 ? Math.Min(1.0, (double)BytesReceived / TotalBytes) : 0;
}

public interface IVoiceModelManager
{
    IReadOnlyList<VoiceInfo> Voices { get; }

    bool IsInstalled(string voiceId);

    /// <summary>Full path to the .onnx model file (may not exist yet).</summary>
    string GetModelPath(string voiceId);

    /// <summary>Full path to the .onnx.json config file (may not exist yet).</summary>
    string GetConfigPath(string voiceId);

    /// <exception cref="VoiceDownloadException">Network failure, checksum mismatch, or disk trouble.</exception>
    Task DownloadAsync(string voiceId, IProgress<DownloadProgress>? progress, CancellationToken ct);

    void Delete(string voiceId);
}

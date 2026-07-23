namespace RoyalNewsDesk.Core.Presenters;

public enum PresenterInstallPhase
{
    Downloading,
    Extracting,
}

/// <summary>Overall install progress: download fills 0..0.9, extraction 0.9..1.0.</summary>
public sealed record PresenterInstallProgress(PresenterInstallPhase Phase, string FileName, double Fraction);

public interface IPresenterEngineManager
{
    IReadOnlyList<PresenterEngineInfo> Engines { get; }

    bool IsInstalled(string engineId);

    string GetEngineDir(string engineId);

    string GetEntrypointPath(string engineId);

    /// <exception cref="PresenterInstallException">Network, checksum, disk, or extraction trouble.</exception>
    Task DownloadAsync(string engineId, IProgress<PresenterInstallProgress>? progress, CancellationToken ct);

    void Delete(string engineId);
}

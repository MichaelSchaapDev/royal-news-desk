using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using RoyalNewsDesk.Core.Storage;

namespace RoyalNewsDesk.Core.Presenters;

/// <summary>
/// Downloads presenter engine bundles with streaming SHA256 verification,
/// then extracts their zip parts into the engine folder. Mirrors the voice
/// model manager, plus the extraction phase voices never need.
/// </summary>
public sealed class PresenterEngineManager(AppPaths paths, HttpClient http, PresenterCatalog catalog)
    : IPresenterEngineManager
{
    private const string MarkerFileName = "installed.json";
    private const int CopyBufferSize = 81920;
    private const double DownloadShare = 0.9;

    public IReadOnlyList<PresenterEngineInfo> Engines => catalog.Engines;

    public bool IsInstalled(string engineId)
    {
        var engine = catalog.Find(engineId);
        if (engine is null)
        {
            return false;
        }

        return File.Exists(Path.Combine(GetEngineDir(engineId), MarkerFileName))
            && File.Exists(GetEntrypointPath(engineId));
    }

    public string GetEngineDir(string engineId) => Path.Combine(paths.PresentersRoot, engineId);

    public string GetEntrypointPath(string engineId)
    {
        var engine = catalog.Find(engineId)
            ?? throw new ArgumentException("Unknown presenter engine: " + engineId, nameof(engineId));
        return Path.Combine(GetEngineDir(engineId), engine.Entrypoint.Replace('/', Path.DirectorySeparatorChar));
    }

    public async Task DownloadAsync(
        string engineId,
        IProgress<PresenterInstallProgress>? progress,
        CancellationToken ct)
    {
        var engine = catalog.Find(engineId)
            ?? throw new ArgumentException("Unknown presenter engine: " + engineId, nameof(engineId));
        if (IsInstalled(engineId))
        {
            return;
        }

        var dir = GetEngineDir(engineId);
        CheckDiskSpace(engine, dir);

        // Zips make resume-after-partial-extract unreliable; start clean.
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }

        Directory.CreateDirectory(dir);

        var totalBytes = engine.TotalBytes;
        long doneBytes = 0;
        var archives = new List<(string Path, string FileName)>();

        foreach (var file in engine.Files)
        {
            var finalPath = Path.Combine(dir, file.FileName);
            await DownloadOneAsync(engine, file, finalPath, doneBytes, totalBytes, progress, ct)
                .ConfigureAwait(false);
            doneBytes += file.SizeBytes;
            if (file.Extract)
            {
                archives.Add((finalPath, file.FileName));
            }
        }

        for (var i = 0; i < archives.Count; i++)
        {
            var extractStart = DownloadShare + (1 - DownloadShare) * i / archives.Count;
            var extractEnd = DownloadShare + (1 - DownloadShare) * (i + 1) / archives.Count;
            ExtractArchive(archives[i].Path, dir, archives[i].FileName, extractStart, extractEnd, progress, ct);
            File.Delete(archives[i].Path);
        }

        var marker = new InstalledMarker(
            engineId,
            engine.Entrypoint,
            engine.Files.ToDictionary(f => f.FileName, f => f.Sha256),
            DateTime.UtcNow);
        await File.WriteAllTextAsync(
            Path.Combine(dir, MarkerFileName),
            JsonSerializer.Serialize(marker, JsonDefaults.Options),
            ct).ConfigureAwait(false);
        progress?.Report(new PresenterInstallProgress(PresenterInstallPhase.Extracting, "", 1));
    }

    public void Delete(string engineId)
    {
        var dir = GetEngineDir(engineId);
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private void CheckDiskSpace(PresenterEngineInfo engine, string dir)
    {
        var drive = Path.GetPathRoot(Path.GetFullPath(dir));
        if (drive is null)
        {
            return;
        }

        var needed = Math.Max(engine.ExtractedSizeBytes, engine.TotalBytes * 2)
            + engine.TotalBytes
            + 2L * 1024 * 1024 * 1024;
        if (new DriveInfo(drive).AvailableFreeSpace < needed)
        {
            throw new PresenterInstallException(
                PresenterInstallFailure.Disk,
                string.Create(CultureInfo.InvariantCulture, $"Needs about {needed / 1_000_000_000} GB free disk space."));
        }
    }

    private async Task DownloadOneAsync(
        PresenterEngineInfo engine,
        PresenterEngineFile file,
        string finalPath,
        long doneBytes,
        long totalBytes,
        IProgress<PresenterInstallProgress>? progress,
        CancellationToken ct)
    {
        var partialPath = finalPath + ".partial";
        Exception? lastError = null;

        foreach (var url in file.Urls)
        {
            try
            {
                await DownloadFromUrlAsync(file, url, partialPath, doneBytes, totalBytes, progress, ct)
                    .ConfigureAwait(false);
                File.Move(partialPath, finalPath, overwrite: true);
                return;
            }
            catch (HttpRequestException ex)
            {
                lastError = ex;
                TryDelete(partialPath);
            }
            catch (IOException ex)
            {
                TryDelete(partialPath);
                throw new PresenterInstallException(
                    PresenterInstallFailure.Disk,
                    "Could not write " + file.FileName,
                    ex);
            }
            catch (OperationCanceledException)
            {
                TryDelete(partialPath);
                throw;
            }
        }

        throw new PresenterInstallException(
            PresenterInstallFailure.Network,
            "All download locations failed for " + file.FileName,
            lastError);
    }

    private async Task DownloadFromUrlAsync(
        PresenterEngineFile file,
        string url,
        string partialPath,
        long doneBytes,
        long totalBytes,
        IProgress<PresenterInstallProgress>? progress,
        CancellationToken ct)
    {
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var contentLength = response.Content.Headers.ContentLength;
        if (contentLength is { } length && Math.Abs(length - file.SizeBytes) > file.SizeBytes / 10 + 1024)
        {
            throw new HttpRequestException(string.Create(
                CultureInfo.InvariantCulture,
                $"Unexpected size for {file.FileName}: got {length}, expected {file.SizeBytes}"));
        }

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        await using (var source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
        await using (var target = new FileStream(partialPath, FileMode.Create, FileAccess.Write, FileShare.None, CopyBufferSize))
        {
            var buffer = new byte[CopyBufferSize];
            long received = 0;
            int read;
            while ((read = await source.ReadAsync(buffer.AsMemory(), ct).ConfigureAwait(false)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                hash.AppendData(buffer, 0, read);
                received += read;
                progress?.Report(new PresenterInstallProgress(
                    PresenterInstallPhase.Downloading,
                    file.FileName,
                    Math.Min(DownloadShare, DownloadShare * (doneBytes + received) / Math.Max(1, totalBytes))));
            }
        }

        var actual = Convert.ToHexStringLower(hash.GetHashAndReset());
        if (!string.Equals(actual, file.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            TryDelete(partialPath);
            throw new PresenterInstallException(
                PresenterInstallFailure.ChecksumMismatch,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Checksum mismatch for {file.FileName}: got {actual}, expected {file.Sha256}"));
        }
    }

    private static void ExtractArchive(
        string archivePath,
        string targetDir,
        string fileName,
        double fractionStart,
        double fractionEnd,
        IProgress<PresenterInstallProgress>? progress,
        CancellationToken ct)
    {
        var root = Path.GetFullPath(targetDir);
        try
        {
            using var archive = ZipFile.OpenRead(archivePath);
            var total = Math.Max(1, archive.Entries.Count);
            var index = 0;
            foreach (var entry in archive.Entries)
            {
                ct.ThrowIfCancellationRequested();
                index++;

                var destination = Path.GetFullPath(Path.Combine(root, entry.FullName));
                if (!destination.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(destination, root, StringComparison.OrdinalIgnoreCase))
                {
                    throw new PresenterInstallException(
                        PresenterInstallFailure.Extraction,
                        "Archive entry escapes the engine folder: " + entry.FullName);
                }

                if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
                {
                    Directory.CreateDirectory(LongPath(destination));
                    continue;
                }

                Directory.CreateDirectory(LongPath(Path.GetDirectoryName(destination)!));
                using var source = entry.Open();
                using var target = new FileStream(
                    LongPath(destination), FileMode.Create, FileAccess.Write, FileShare.None, CopyBufferSize);
                source.CopyTo(target);

                if (index % 50 == 0 || index == total)
                {
                    progress?.Report(new PresenterInstallProgress(
                        PresenterInstallPhase.Extracting,
                        fileName,
                        fractionStart + (fractionEnd - fractionStart) * index / total));
                }
            }
        }
        catch (InvalidDataException ex)
        {
            throw new PresenterInstallException(
                PresenterInstallFailure.Extraction,
                "Archive is corrupt: " + fileName,
                ex);
        }
        catch (IOException ex)
        {
            throw new PresenterInstallException(
                PresenterInstallFailure.Disk,
                "Could not extract " + fileName,
                ex);
        }
    }

    /// <summary>Deep Python trees can pass MAX_PATH; the \\?\ prefix sidesteps it.</summary>
    private static string LongPath(string path) =>
        path.StartsWith(@"\\", StringComparison.Ordinal) ? path : @"\\?\" + path;

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
    }

    private sealed record InstalledMarker(
        string EngineId,
        string Entrypoint,
        Dictionary<string, string> Files,
        DateTime VerifiedUtc);
}

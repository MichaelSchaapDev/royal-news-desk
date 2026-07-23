using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using RoyalNewsDesk.Core.Storage;

namespace RoyalNewsDesk.Core.VoiceModels;

/// <summary>
/// Downloads voice models to the app data folder with streaming SHA256
/// verification and an atomic rename, so a half-finished or tampered
/// download can never pass for an installed voice.
/// </summary>
public sealed class VoiceModelManager(AppPaths paths, HttpClient http, VoiceCatalog catalog) : IVoiceModelManager
{
    private const string MarkerFileName = "installed.json";
    private const int CopyBufferSize = 81920;

    public IReadOnlyList<VoiceInfo> Voices => catalog.Voices;

    public bool IsInstalled(string voiceId)
    {
        var voice = catalog.Find(voiceId);
        if (voice is null)
        {
            return false;
        }

        var dir = VoiceDir(voiceId);
        if (!File.Exists(Path.Combine(dir, MarkerFileName)))
        {
            return false;
        }

        foreach (var file in voice.Files)
        {
            var path = Path.Combine(dir, file.FileName);
            if (!File.Exists(path) || new FileInfo(path).Length != file.SizeBytes)
            {
                return false;
            }
        }

        return true;
    }

    public string GetModelPath(string voiceId) => Path.Combine(VoiceDir(voiceId), voiceId + ".onnx");

    public string GetConfigPath(string voiceId) => Path.Combine(VoiceDir(voiceId), voiceId + ".onnx.json");

    public async Task DownloadAsync(string voiceId, IProgress<DownloadProgress>? progress, CancellationToken ct)
    {
        var voice = catalog.Find(voiceId)
            ?? throw new ArgumentException("Unknown voice: " + voiceId, nameof(voiceId));
        var dir = VoiceDir(voiceId);
        Directory.CreateDirectory(dir);

        var totalBytes = voice.TotalBytes;
        long doneBytes = 0;

        foreach (var file in voice.Files)
        {
            var finalPath = Path.Combine(dir, file.FileName);
            if (File.Exists(finalPath) && new FileInfo(finalPath).Length == file.SizeBytes)
            {
                doneBytes += file.SizeBytes;
                progress?.Report(new DownloadProgress(file.FileName, doneBytes, totalBytes));
                continue;
            }

            await DownloadOneAsync(file, finalPath, doneBytes, totalBytes, progress, ct).ConfigureAwait(false);
            doneBytes += file.SizeBytes;
        }

        var marker = new InstalledMarker(
            voiceId,
            voice.Files.ToDictionary(f => f.FileName, f => f.Sha256),
            DateTime.UtcNow);
        var markerPath = Path.Combine(dir, MarkerFileName);
        await File.WriteAllTextAsync(
            markerPath,
            JsonSerializer.Serialize(marker, JsonDefaults.Options),
            ct).ConfigureAwait(false);
    }

    public void Delete(string voiceId)
    {
        var dir = VoiceDir(voiceId);
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private async Task DownloadOneAsync(
        VoiceFile file,
        string finalPath,
        long doneBytes,
        long totalBytes,
        IProgress<DownloadProgress>? progress,
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
                throw new VoiceDownloadException(VoiceDownloadFailure.Disk, "Could not write " + file.FileName, ex);
            }
            catch (OperationCanceledException)
            {
                TryDelete(partialPath);
                throw;
            }
        }

        throw new VoiceDownloadException(
            VoiceDownloadFailure.Network,
            "All download locations failed for " + file.FileName,
            lastError);
    }

    private async Task DownloadFromUrlAsync(
        VoiceFile file,
        string url,
        string partialPath,
        long doneBytes,
        long totalBytes,
        IProgress<DownloadProgress>? progress,
        CancellationToken ct)
    {
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        // HuggingFace serves a small HTML error page for wrong paths; catch it
        // before wasting a full "download" on it.
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
                progress?.Report(new DownloadProgress(file.FileName, doneBytes + received, totalBytes));
            }
        }

        var actual = Convert.ToHexStringLower(hash.GetHashAndReset());
        if (!string.Equals(actual, file.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            TryDelete(partialPath);
            throw new VoiceDownloadException(
                VoiceDownloadFailure.ChecksumMismatch,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Checksum mismatch for {file.FileName}: got {actual}, expected {file.Sha256}"));
        }
    }

    private string VoiceDir(string voiceId) => Path.Combine(paths.ModelsRoot, voiceId);

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
        string VoiceId,
        Dictionary<string, string> Files,
        DateTime VerifiedUtc);
}

namespace RoyalNewsDesk.Core.Storage;

/// <summary>
/// Moves the downloaded AI files (voices and presenter engines) to a new
/// folder: copy everything, then delete the source. The source only goes
/// away after every byte arrived, so a failure or cancellation mid-way
/// leaves the old location fully usable.
/// </summary>
public static class AiStorageMover
{
    private const int CopyBufferSize = 1 << 20;

    private static readonly string[] SubFolders = ["models", "presenters"];

    /// <summary>Total bytes currently stored under the AI subfolders of a root.</summary>
    public static long MeasureBytes(string aiRoot)
    {
        long total = 0;
        foreach (var sub in SubFolders)
        {
            var dir = Path.Combine(aiRoot, sub);
            if (!Directory.Exists(LongPath(dir)))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(LongPath(dir), "*", SearchOption.AllDirectories))
            {
                total += new FileInfo(file).Length;
            }
        }

        return total;
    }

    /// <summary>Free bytes on the drive that holds <paramref name="path"/>.</summary>
    public static long FreeBytesAt(string path)
    {
        var root = Path.GetPathRoot(Path.GetFullPath(path));
        return string.IsNullOrEmpty(root) ? 0 : new DriveInfo(root).AvailableFreeSpace;
    }

    /// <summary>True when <paramref name="candidate"/> equals or sits inside <paramref name="root"/>.</summary>
    public static bool IsSameOrNested(string root, string candidate)
    {
        var a = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var b = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate));
        return b.Equals(a, StringComparison.OrdinalIgnoreCase)
            || b.StartsWith(a + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || a.StartsWith(b + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    public static async Task MoveAsync(
        string oldAiRoot,
        string newAiRoot,
        IProgress<double>? progress,
        CancellationToken ct)
    {
        if (IsSameOrNested(oldAiRoot, newAiRoot))
        {
            throw new ArgumentException("The new folder must not overlap the old one.", nameof(newAiRoot));
        }

        var totalBytes = Math.Max(1, MeasureBytes(oldAiRoot));
        long copied = 0;

        foreach (var sub in SubFolders)
        {
            var sourceDir = Path.Combine(oldAiRoot, sub);
            if (!Directory.Exists(LongPath(sourceDir)))
            {
                continue;
            }

            var targetDir = Path.Combine(newAiRoot, sub);
            foreach (var sourceFile in Directory.EnumerateFiles(LongPath(sourceDir), "*", SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();
                var relative = Path.GetRelativePath(LongPath(sourceDir), sourceFile);
                var targetFile = Path.Combine(targetDir, relative);
                Directory.CreateDirectory(LongPath(Path.GetDirectoryName(targetFile)!));

                await using var source = await OpenWithRetryAsync(
                    () => new FileStream(
                        sourceFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, CopyBufferSize,
                        FileOptions.Asynchronous | FileOptions.SequentialScan),
                    ct).ConfigureAwait(false);
                await using var target = await OpenWithRetryAsync(
                    () => new FileStream(
                        LongPath(targetFile), FileMode.Create, FileAccess.Write, FileShare.None, CopyBufferSize,
                        FileOptions.Asynchronous),
                    ct).ConfigureAwait(false);

                var buffer = new byte[CopyBufferSize];
                int read;
                while ((read = await source.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                    copied += read;
                    progress?.Report(Math.Min(1.0, (double)copied / totalBytes));
                }
            }
        }

        foreach (var sub in SubFolders)
        {
            var sourceDir = Path.Combine(oldAiRoot, sub);
            if (Directory.Exists(LongPath(sourceDir)))
            {
                Directory.Delete(LongPath(sourceDir), recursive: true);
            }
        }

        progress?.Report(1.0);
    }

    /// <summary>
    /// Virus scanners, the search indexer and thumbnail readers grab fresh
    /// files for a moment; a sharing violation here is almost always gone a
    /// second later, so retry briefly before giving up.
    /// </summary>
    private static async Task<FileStream> OpenWithRetryAsync(Func<FileStream> open, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return open();
            }
            catch (IOException) when (attempt < 4)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(400 << attempt), ct).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Deep Python trees can pass MAX_PATH; the \\?\ prefix sidesteps it.</summary>
    private static string LongPath(string path) =>
        path.StartsWith(@"\\", StringComparison.Ordinal) ? path : @"\\?\" + path;
}

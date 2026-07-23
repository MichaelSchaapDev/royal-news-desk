using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using RoyalNewsDesk.Core.Presenters;
using RoyalNewsDesk.Core.Storage;

namespace RoyalNewsDesk.Core.Tests.Presenters;

public class PresenterEngineManagerTests
{
    private static byte[] BuildZip(params (string Name, byte[] Content)[] entries)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                var entry = archive.CreateEntry(name);
                using var entryStream = entry.Open();
                entryStream.Write(content);
            }
        }

        return stream.ToArray();
    }

    private static string Sha(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static PresenterCatalog Catalog(byte[] zipBytes, params string[] urls) => new(
    [
        new PresenterEngineInfo
        {
            Id = "test-engine",
            DisplayName = "Test engine",
            License = "test",
            Entrypoint = "python/python.exe",
            ExtractedSizeBytes = 1_000,
            Files =
            [
                new PresenterEngineFile
                {
                    FileName = "engine.zip",
                    Sha256 = Sha(zipBytes),
                    SizeBytes = zipBytes.Length,
                    Urls = urls.Length > 0 ? urls : ["https://example.test/engine.zip"],
                    Extract = true,
                },
            ],
        },
    ]);

    private sealed class FakeHandler(Func<string, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<string> Requested { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var url = request.RequestUri!.ToString();
            Requested.Add(url);
            return Task.FromResult(responder(url));
        }
    }

    private static HttpResponseMessage Bytes(byte[] payload) => new(HttpStatusCode.OK)
    {
        Content = new ByteArrayContent(payload),
    };

    [Fact]
    public async Task DownloadsExtractsAndMarksInstalled()
    {
        using var temp = new TempDir();
        var paths = new AppPaths(temp.Path);
        var zip = BuildZip(
            ("python/python.exe", "fake python"u8.ToArray()),
            ("engine/inference.py", "print('hi')"u8.ToArray()));
        var handler = new FakeHandler(_ => Bytes(zip));
        using var http = new HttpClient(handler);
        var manager = new PresenterEngineManager(paths, http, Catalog(zip));

        var reports = new List<PresenterInstallProgress>();
        Assert.False(manager.IsInstalled("test-engine"));
        await manager.DownloadAsync("test-engine", new SyncProgress(reports.Add), CancellationToken.None);

        Assert.True(manager.IsInstalled("test-engine"));
        Assert.True(File.Exists(manager.GetEntrypointPath("test-engine")));
        Assert.True(File.Exists(Path.Combine(manager.GetEngineDir("test-engine"), "engine", "inference.py")));
        Assert.False(File.Exists(Path.Combine(manager.GetEngineDir("test-engine"), "engine.zip")));
        Assert.Contains(reports, r => r.Phase == PresenterInstallPhase.Downloading);
        Assert.Contains(reports, r => r.Phase == PresenterInstallPhase.Extracting);
        Assert.Equal(1.0, reports[^1].Fraction, 3);

        manager.Delete("test-engine");
        Assert.False(manager.IsInstalled("test-engine"));
    }

    [Fact]
    public async Task ChecksumMismatchInstallsNothing()
    {
        using var temp = new TempDir();
        var paths = new AppPaths(temp.Path);
        var goodZip = BuildZip(("python/python.exe", "real"u8.ToArray()));
        var badZip = BuildZip(("python/python.exe", "evil"u8.ToArray()));
        var handler = new FakeHandler(_ => Bytes(badZip));
        using var http = new HttpClient(handler);
        var manager = new PresenterEngineManager(paths, http, Catalog(goodZip));

        var ex = await Assert.ThrowsAsync<PresenterInstallException>(() =>
            manager.DownloadAsync("test-engine", null, CancellationToken.None));

        Assert.Equal(PresenterInstallFailure.ChecksumMismatch, ex.Reason);
        Assert.False(manager.IsInstalled("test-engine"));
        Assert.False(File.Exists(manager.GetEntrypointPath("test-engine")));
    }

    [Fact]
    public async Task ZipSlipEntriesAreRefused()
    {
        using var temp = new TempDir();
        var paths = new AppPaths(temp.Path);
        var zip = BuildZip(
            ("python/python.exe", "ok"u8.ToArray()),
            ("../evil.txt", "escape"u8.ToArray()));
        var handler = new FakeHandler(_ => Bytes(zip));
        using var http = new HttpClient(handler);
        var manager = new PresenterEngineManager(paths, http, Catalog(zip));

        var ex = await Assert.ThrowsAsync<PresenterInstallException>(() =>
            manager.DownloadAsync("test-engine", null, CancellationToken.None));

        Assert.Equal(PresenterInstallFailure.Extraction, ex.Reason);
        Assert.False(File.Exists(Path.Combine(paths.PresentersRoot, "evil.txt")));
        Assert.False(manager.IsInstalled("test-engine"));
    }

    [Fact]
    public async Task FallsBackToMirrorUrl()
    {
        using var temp = new TempDir();
        var paths = new AppPaths(temp.Path);
        var zip = BuildZip(("python/python.exe", "mirror"u8.ToArray()));
        var handler = new FakeHandler(url => url.Contains("primary", StringComparison.Ordinal)
            ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
            : Bytes(zip));
        using var http = new HttpClient(handler);
        var manager = new PresenterEngineManager(
            paths, http, Catalog(zip, "https://primary.test/engine.zip", "https://mirror.test/engine.zip"));

        await manager.DownloadAsync("test-engine", null, CancellationToken.None);

        Assert.True(manager.IsInstalled("test-engine"));
        Assert.Contains(handler.Requested, u => u.Contains("mirror", StringComparison.Ordinal));
    }

    private sealed class SyncProgress(Action<PresenterInstallProgress> handler) : IProgress<PresenterInstallProgress>
    {
        public void Report(PresenterInstallProgress value) => handler(value);
    }
}

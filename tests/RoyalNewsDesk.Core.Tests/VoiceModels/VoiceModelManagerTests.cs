using System.Net;
using System.Security.Cryptography;
using System.Text;
using RoyalNewsDesk.Core.Storage;
using RoyalNewsDesk.Core.VoiceModels;

namespace RoyalNewsDesk.Core.Tests.VoiceModels;

public class VoiceModelManagerTests
{
    private static readonly byte[] ModelBytes = Encoding.ASCII.GetBytes("fake onnx model bytes for testing");
    private static readonly byte[] ConfigBytes = Encoding.ASCII.GetBytes("{ \"fake\": true }");

    private static string Sha(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static VoiceCatalog TestCatalog(string modelSha, string configSha, params string[] modelUrls) => new(
    [
        new VoiceInfo
        {
            Id = "test-voice",
            DisplayName = "Test voice",
            Quality = "medium",
            SampleRate = 22050,
            License = "test",
            Files =
            [
                new VoiceFile
                {
                    FileName = "test-voice.onnx",
                    Sha256 = modelSha,
                    SizeBytes = ModelBytes.Length,
                    Urls = modelUrls.Length > 0 ? modelUrls : ["https://example.test/model.onnx"],
                },
                new VoiceFile
                {
                    FileName = "test-voice.onnx.json",
                    Sha256 = configSha,
                    SizeBytes = ConfigBytes.Length,
                    Urls = ["https://example.test/model.onnx.json"],
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
    public async Task DownloadsVerifiesAndMarksInstalled()
    {
        using var temp = new TempDir();
        var paths = new AppPaths(temp.Path);
        var handler = new FakeHandler(url => url.EndsWith(".json", StringComparison.Ordinal)
            ? Bytes(ConfigBytes)
            : Bytes(ModelBytes));
        using var http = new HttpClient(handler);
        var manager = new VoiceModelManager(paths, http, TestCatalog(Sha(ModelBytes), Sha(ConfigBytes)));

        var reports = new List<DownloadProgress>();
        Assert.False(manager.IsInstalled("test-voice"));
        await manager.DownloadAsync("test-voice", new SyncProgress(reports.Add), CancellationToken.None);

        Assert.True(manager.IsInstalled("test-voice"));
        Assert.True(File.Exists(manager.GetModelPath("test-voice")));
        Assert.True(File.Exists(manager.GetConfigPath("test-voice")));
        Assert.NotEmpty(reports);
        Assert.Equal(ModelBytes.Length + ConfigBytes.Length, reports[^1].BytesReceived);

        manager.Delete("test-voice");
        Assert.False(manager.IsInstalled("test-voice"));
    }

    [Fact]
    public async Task ChecksumMismatchFailsAndLeavesNothing()
    {
        using var temp = new TempDir();
        var paths = new AppPaths(temp.Path);
        var handler = new FakeHandler(url => url.EndsWith(".json", StringComparison.Ordinal)
            ? Bytes(ConfigBytes)
            : Bytes(Encoding.ASCII.GetBytes("tampered bytes of the same length!")));
        using var http = new HttpClient(handler);
        var manager = new VoiceModelManager(paths, http, TestCatalog(Sha(ModelBytes), Sha(ConfigBytes)));

        var ex = await Assert.ThrowsAsync<VoiceDownloadException>(() =>
            manager.DownloadAsync("test-voice", null, CancellationToken.None));

        Assert.Equal(VoiceDownloadFailure.ChecksumMismatch, ex.Reason);
        Assert.False(manager.IsInstalled("test-voice"));
        Assert.False(File.Exists(manager.GetModelPath("test-voice")));
        Assert.False(File.Exists(manager.GetModelPath("test-voice") + ".partial"));
    }

    [Fact]
    public async Task FallsBackToMirrorUrl()
    {
        using var temp = new TempDir();
        var paths = new AppPaths(temp.Path);
        var handler = new FakeHandler(url =>
        {
            if (url.Contains("primary", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }

            return url.EndsWith(".json", StringComparison.Ordinal) ? Bytes(ConfigBytes) : Bytes(ModelBytes);
        });
        using var http = new HttpClient(handler);
        var manager = new VoiceModelManager(
            paths,
            http,
            TestCatalog(Sha(ModelBytes), Sha(ConfigBytes), "https://primary.test/model.onnx", "https://mirror.test/model.onnx"));

        await manager.DownloadAsync("test-voice", null, CancellationToken.None);

        Assert.True(manager.IsInstalled("test-voice"));
        Assert.Contains(handler.Requested, u => u.Contains("mirror", StringComparison.Ordinal));
    }

    /// <summary>Synchronous IProgress so tests see every report without a SynchronizationContext.</summary>
    private sealed class SyncProgress(Action<DownloadProgress> handler) : IProgress<DownloadProgress>
    {
        public void Report(DownloadProgress value) => handler(value);
    }
}

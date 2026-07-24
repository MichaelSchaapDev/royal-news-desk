using RoyalNewsDesk.Core.Storage;

namespace RoyalNewsDesk.Core.Tests.Storage;

public sealed class AiStorageMoverTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "rnd-mover-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string Prepare(string name, params (string RelativePath, int Bytes)[] files)
    {
        var root = Path.Combine(_root, name);
        foreach (var (relative, bytes) in files)
        {
            var path = Path.Combine(root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, new byte[bytes]);
        }

        return root;
    }

    [Fact]
    public async Task Move_copies_everything_and_removes_the_source()
    {
        var oldRoot = Prepare(
            "old",
            (Path.Combine("models", "voice", "voice.onnx"), 2048),
            (Path.Combine("presenters", "engine", "python", "python.exe"), 512),
            (Path.Combine("presenters", "engine", "installed.json"), 16));
        var newRoot = Path.Combine(_root, "new");
        var fractions = new List<double>();

        await AiStorageMover.MoveAsync(
            oldRoot, newRoot, new SyncProgress(fractions.Add), CancellationToken.None);

        Assert.True(File.Exists(Path.Combine(newRoot, "models", "voice", "voice.onnx")));
        Assert.True(File.Exists(Path.Combine(newRoot, "presenters", "engine", "python", "python.exe")));
        Assert.True(File.Exists(Path.Combine(newRoot, "presenters", "engine", "installed.json")));
        Assert.False(Directory.Exists(Path.Combine(oldRoot, "models")));
        Assert.False(Directory.Exists(Path.Combine(oldRoot, "presenters")));
        Assert.Equal(1.0, fractions[^1]);
        Assert.Equal(2048 + 512 + 16, AiStorageMover.MeasureBytes(newRoot));
    }

    [Fact]
    public async Task Cancellation_leaves_the_source_untouched()
    {
        var oldRoot = Prepare(
            "old",
            (Path.Combine("models", "voice", "big.onnx"), 4 * 1024 * 1024));
        var newRoot = Path.Combine(_root, "new");
        using var cts = new CancellationTokenSource();
        var progress = new SyncProgress(_ => cts.Cancel());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            AiStorageMover.MoveAsync(oldRoot, newRoot, progress, cts.Token));

        Assert.True(File.Exists(Path.Combine(oldRoot, "models", "voice", "big.onnx")));
        Assert.Equal(4 * 1024 * 1024, AiStorageMover.MeasureBytes(oldRoot));
    }

    [Fact]
    public async Task Overlapping_folders_are_refused()
    {
        var oldRoot = Prepare("old", (Path.Combine("models", "a.bin"), 8));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            AiStorageMover.MoveAsync(oldRoot, Path.Combine(oldRoot, "sub"), null, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            AiStorageMover.MoveAsync(oldRoot, oldRoot, null, CancellationToken.None));
    }

    [Fact]
    public void Nesting_check_matches_prefixes_by_segment()
    {
        Assert.True(AiStorageMover.IsSameOrNested(@"C:\data", @"C:\data"));
        Assert.True(AiStorageMover.IsSameOrNested(@"C:\data", @"C:\data\inner"));
        Assert.True(AiStorageMover.IsSameOrNested(@"C:\data\inner", @"C:\data"));
        Assert.False(AiStorageMover.IsSameOrNested(@"C:\data", @"C:\database"));
    }

    [Fact]
    public void Paths_follow_the_ai_root_override()
    {
        var paths = new AppPaths(@"C:\default");
        Assert.Equal(@"C:\default\models", paths.ModelsRoot);

        paths.AiRootOverride = @"D:\ai";
        Assert.Equal(@"D:\ai\models", paths.ModelsRoot);
        Assert.Equal(@"D:\ai\presenters", paths.PresentersRoot);
        Assert.Equal(@"C:\default\settings.json", paths.SettingsFile);
        Assert.Equal(@"C:\default\episodes", paths.EpisodesRoot);

        paths.AiRootOverride = "  ";
        Assert.Equal(@"C:\default\models", paths.ModelsRoot);
    }

    private sealed class SyncProgress(Action<double> handler) : IProgress<double>
    {
        public void Report(double value) => handler(value);
    }
}

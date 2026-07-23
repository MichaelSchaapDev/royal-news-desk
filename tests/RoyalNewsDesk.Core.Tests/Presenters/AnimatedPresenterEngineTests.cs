using RoyalNewsDesk.Core.LipSync;
using RoyalNewsDesk.Core.Presenters;
using RoyalNewsDesk.Core.Storage;
using RoyalNewsDesk.Core.Tests.Graphics;

namespace RoyalNewsDesk.Core.Tests.Presenters;

public class AnimatedPresenterEngineTests
{
    [Fact]
    public async Task RendersStillsTrackWithPosesAndFfconcat()
    {
        var anchorAssets = TestAssets.FindAnchorAssetsDir();
        Assert.NotNull(anchorAssets);
        var assetsDir = Path.GetDirectoryName(anchorAssets)!;

        using var temp = new TempDir();
        var paths = new EpisodePaths(temp.Path);
        paths.EnsureCreated();
        paths.EnsureWorkDirsCreated();

        var engine = new AnimatedPresenterEngine(assetsDir);
        var mouths = new MouthCueTrack(2.0,
        [
            new MouthCue(0.0, 1.0, MouthShape.B),
            new MouthCue(1.0, 2.0, MouthShape.X),
        ]);

        var track = await engine.RenderAsync(
            new PresenterRequest
            {
                Paths = paths,
                BodyDuration = 2.0,
                BlinkSeed = "Test Episode",
                MouthCues = mouths,
            },
            null,
            CancellationToken.None);

        var stills = Assert.IsType<PresenterTrack.Stills>(track);
        Assert.Equal("gfx/anchor/anchor.ffconcat", stills.FfconcatPath);
        Assert.True(File.Exists(Path.Combine(paths.AnchorDir, "anchor.ffconcat")));
        Assert.Equal(18, Directory.GetFiles(paths.AnchorDir, "*.png").Length);
    }

    [Fact]
    public async Task RequiresMouthCues()
    {
        using var temp = new TempDir();
        var paths = new EpisodePaths(temp.Path);
        var engine = new AnimatedPresenterEngine(@"C:\nowhere");

        await Assert.ThrowsAsync<ArgumentException>(() => engine.RenderAsync(
            new PresenterRequest { Paths = paths, BodyDuration = 1, BlinkSeed = "x" },
            null,
            CancellationToken.None));
    }
}

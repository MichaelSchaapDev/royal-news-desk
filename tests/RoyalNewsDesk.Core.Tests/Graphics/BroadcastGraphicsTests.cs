using RoyalNewsDesk.Core.Graphics;
using RoyalNewsDesk.Core.Models;
using SkiaSharp;

namespace RoyalNewsDesk.Core.Tests.Graphics;

public class BroadcastGraphicsTests : IDisposable
{
    private readonly TempDir _temp = new();
    private readonly FontCatalog _fonts;
    private readonly BroadcastGraphics _gfx;
    private readonly BrandStyle _brand = BrandStyle.From(new Branding());

    public BroadcastGraphicsTests()
    {
        var assets = TestAssets.FindAnchorAssetsDir();
        Assert.NotNull(assets);
        _fonts = new FontCatalog(Path.Combine(Path.GetDirectoryName(assets)!, "fonts"));
        _gfx = new BroadcastGraphics(_fonts);
    }

    public void Dispose()
    {
        _fonts.Dispose();
        _temp.Dispose();
    }

    private string Out(string name) => Path.Combine(_temp.Path, name);

    private static SKBitmap Decode(string path)
    {
        var bitmap = SKBitmap.Decode(path);
        Assert.NotNull(bitmap);
        return bitmap;
    }

    [Fact]
    public void LowerThirdHasAccentEdgeAndText()
    {
        var path = Out("lt.png");
        _gfx.RenderLowerThird(path, "Palace denies secret abdication plan", _brand);

        using var bitmap = Decode(path);
        Assert.Equal(BroadcastGraphics.LowerThirdWidth, bitmap.Width);
        Assert.Equal(BroadcastGraphics.LowerThirdHeight, bitmap.Height);

        var edge = bitmap.GetPixel(5, 85);
        Assert.True(edge.Red > 150 && edge.Green > 110 && edge.Blue < 100, "Accent edge missing: " + edge);
    }

    [Fact]
    public void TickerStripReportsContentWidthAndLoops()
    {
        var path = Out("strip.png");
        var contentWidth = _gfx.RenderTickerStrip(path, ["First headline", "Second headline"], _brand);

        using var bitmap = Decode(path);
        Assert.True(contentWidth >= 1920 * 2 - 400, "Content should span about two screens, got " + contentWidth.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(contentWidth + 1920, bitmap.Width);

        // Seamless wrap: column 0 equals column contentWidth.
        for (var y = 10; y < 60; y += 10)
        {
            Assert.Equal(bitmap.GetPixel(3, y), bitmap.GetPixel(contentWidth + 3, y));
        }
    }

    [Fact]
    public void CardsAndPlatesRender()
    {
        _gfx.RenderTitleCard(Out("title.png"), "A Very Long Episode Title That Wraps Onto Two Lines Neatly", _brand);
        _gfx.RenderOutroCard(Out("outro.png"), _brand);
        _gfx.RenderTickerBar(Out("bar.png"));
        _gfx.RenderTickerBlock(Out("block.png"), _brand);

        using var title = Decode(Out("title.png"));
        Assert.Equal(BroadcastGraphics.CardWidth, title.Width);
        using var block = Decode(Out("block.png"));
        Assert.Equal(BroadcastGraphics.TickerBlockWidth, block.Width);
    }

    [Fact]
    public void PresenterFrameHasTransparentWindowAndAccent()
    {
        var path = Out("presenter_frame.png");
        _gfx.RenderPresenterFrame(path, _brand);

        using var bitmap = Decode(path);
        Assert.Equal(840, bitmap.Width);
        Assert.Equal(840, bitmap.Height);

        // The window is punched out; the video must show through.
        Assert.Equal(0, bitmap.GetPixel(420, 420).Alpha);
        Assert.Equal(0, bitmap.GetPixel(60, 60).Alpha);

        // White border on the left edge band.
        var border = bitmap.GetPixel(20, 420);
        Assert.True(border.Alpha > 200 && border.Red > 230 && border.Green > 230 && border.Blue > 230, "Border not white: " + border);

        // Gold accent near the bottom edge of the frame.
        var accent = bitmap.GetPixel(420, 824);
        Assert.True(accent.Red > 150 && accent.Green > 110 && accent.Blue < 100, "Accent missing: " + accent);
    }

    [Fact]
    public void ThumbnailRendersWithAndWithoutImage()
    {
        var photo = Out("photo.png");
        using (var surface = SKSurface.Create(new SKImageInfo(400, 300)))
        {
            surface.Canvas.Clear(SKColors.DarkOliveGreen);
            using var img = surface.Snapshot();
            using var data = img.Encode(SKEncodedImageFormat.Png, 90);
            using var fs = File.Create(photo);
            data.SaveTo(fs);
        }

        _gfx.RenderThumbnail(Out("thumb1.png"), "The Coronation Portrait Story", photo, _brand);
        _gfx.RenderThumbnail(Out("thumb2.png"), "Royal Week", null, _brand);

        using var thumb = Decode(Out("thumb1.png"));
        Assert.Equal(1280, thumb.Width);
        Assert.Equal(720, thumb.Height);
        Assert.True(new FileInfo(Out("thumb1.png")).Length < 2_000_000, "Thumbnail must stay under YouTube's 2 MB limit");
    }
}

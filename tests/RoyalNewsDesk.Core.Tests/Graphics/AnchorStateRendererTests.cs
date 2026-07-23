using RoyalNewsDesk.Core.Graphics;
using SkiaSharp;

namespace RoyalNewsDesk.Core.Tests.Graphics;

public class AnchorStateRendererTests
{
    [Fact]
    public void RendersAllEighteenStates()
    {
        var assetsDir = TestAssets.FindAnchorAssetsDir();
        Assert.NotNull(assetsDir);

        using var temp = new TempDir();
        var rendered = AnchorStateRenderer.RenderAll(assetsDir, temp.Path);

        Assert.Equal(18, rendered.Count);
        Assert.Contains("X_open", rendered.Keys);
        Assert.Contains("D_closed", rendered.Keys);

        using var bitmap = SKBitmap.Decode(rendered["X_open"]);
        Assert.Equal(AnchorStateRenderer.Width, bitmap.Width);
        Assert.Equal(AnchorStateRenderer.Height, bitmap.Height);

        // The face area must be painted (not a blank transparent sheet).
        var opaque = 0;
        for (var y = 200; y < 400; y += 10)
        {
            for (var x = 300; x < 500; x += 10)
            {
                if (bitmap.GetPixel(x, y).Alpha > 200)
                {
                    opaque++;
                }
            }
        }

        Assert.True(opaque > 300, "Face area is mostly transparent; SVG layers did not draw.");

        // Top-left corner stays transparent (the anchor is a cutout).
        Assert.Equal(0, bitmap.GetPixel(5, 5).Alpha);
    }
}

public static class TestAssets
{
    public static string? FindAnchorAssetsDir()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 8 && dir is not null; i++)
        {
            var candidate = Path.Combine(dir, "assets", "anchor");
            if (File.Exists(Path.Combine(candidate, "base.svg")))
            {
                return candidate;
            }

            dir = Path.GetDirectoryName(dir);
        }

        return null;
    }
}

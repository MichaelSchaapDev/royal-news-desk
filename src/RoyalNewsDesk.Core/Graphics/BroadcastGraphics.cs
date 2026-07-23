using SkiaSharp;

namespace RoyalNewsDesk.Core.Graphics;

/// <summary>
/// Renders every text-bearing broadcast graphic as a PNG. No ffmpeg drawtext
/// anywhere: pre-rendered images sidestep font-path escaping on Windows and
/// give full typographic control.
/// </summary>
public sealed class BroadcastGraphics(FontCatalog fonts)
{
    public const int LowerThirdWidth = 1500;
    public const int LowerThirdHeight = 170;
    public const int TickerHeight = 70;
    public const int TickerBlockWidth = 340;
    public const int CardWidth = 2112;
    public const int CardHeight = 1188;

    /// <summary>Headline bar: accent edge, channel chip, headline up to two lines.</summary>
    public void RenderLowerThird(string path, string headline, BrandStyle brand)
    {
        using var surface = CreateSurface(LowerThirdWidth, LowerThirdHeight);
        var canvas = surface.Canvas;

        using var background = Fill(BrandStyle.Ink.WithAlpha(235));
        canvas.DrawRoundRect(new SKRect(0, 0, LowerThirdWidth, LowerThirdHeight), 14, 14, background);
        using var accent = Fill(brand.Accent);
        canvas.DrawRoundRect(new SKRect(0, 0, 12, LowerThirdHeight), 6, 6, accent);

        using var chipFont = new SKFont(fonts.SansSemiBold, 26);
        using var chipPaint = Fill(brand.Accent);
        canvas.DrawText(brand.ChannelName.ToUpperInvariant(), 42, 46, SKTextAlign.Left, chipFont, chipPaint);

        var (lines, size) = TextLayout.WrapToWidth(fonts.HeadlineBold, headline, LowerThirdWidth - 84, 54, 34, 2);
        using var headlineFont = new SKFont(fonts.HeadlineBold, size);
        using var headlinePaint = Fill(SKColors.White);
        if (lines.Count == 1)
        {
            canvas.DrawText(lines[0], 42, 118, SKTextAlign.Left, headlineFont, headlinePaint);
        }
        else
        {
            canvas.DrawText(lines[0], 42, 100, SKTextAlign.Left, headlineFont, headlinePaint);
            canvas.DrawText(lines[^1], 42, 100 + size * 1.12f, SKTextAlign.Left, headlineFont, headlinePaint);
        }

        Save(surface, path);
    }

    /// <summary>Translucent full-width bar the crawl runs over.</summary>
    public void RenderTickerBar(string path)
    {
        using var surface = CreateSurface(1920, TickerHeight);
        using var fill = Fill(new SKColor(0x07, 0x0D, 0x1C, 225));
        surface.Canvas.DrawRect(0, 0, 1920, TickerHeight, fill);
        Save(surface, path);
    }

    /// <summary>Opaque brand block that masks the crawl's left edge.</summary>
    public void RenderTickerBlock(string path, BrandStyle brand)
    {
        using var surface = CreateSurface(TickerBlockWidth, TickerHeight);
        var canvas = surface.Canvas;
        using var fill = Fill(brand.Accent);
        canvas.DrawRect(0, 0, TickerBlockWidth, TickerHeight, fill);

        var label = brand.ChannelName.ToUpperInvariant();
        var size = TextLayout.FitSize(fonts.SansBold, label, TickerBlockWidth - 36, 30, 16);
        using var font = new SKFont(fonts.SansBold, size);
        using var text = Fill(BrandStyle.Ink);
        TextLayout.DrawCentered(canvas, label, TickerBlockWidth / 2f, TickerHeight / 2f + size * 0.36f, font, text);
        Save(surface, path);
    }

    /// <summary>
    /// The crawl strip: items joined with bullets, repeated past two screen
    /// widths, then the first 1920 px appended again so the wrap at
    /// x = -contentWidth is seamless.
    /// </summary>
    /// <returns>The content width in pixels (the modulus for the scroll expression).</returns>
    public int RenderTickerStrip(string path, IReadOnlyList<string> items, BrandStyle brand)
    {
        const int maxStripWidth = 16000;
        var content = string.Join("   •   ", items.Where(i => !string.IsNullOrWhiteSpace(i)));
        if (content.Length == 0)
        {
            content = brand.Tagline.Length > 0 ? brand.Tagline : brand.ChannelName;
        }

        content += "   •   ";
        using var font = new SKFont(fonts.SansRegular, 34);
        // Whole-pixel advance: every copy starts on an integer x, so the wrap
        // from x = -contentWidth back to 0 lands on identical pixels.
        var unit = Math.Max(1, (int)MathF.Ceiling(font.MeasureText(content)));

        var repeats = Math.Max(1, (int)Math.Ceiling(2.0 * 1920 / unit));
        while (repeats > 1 && unit * repeats + 1920 > maxStripWidth)
        {
            repeats--;
        }

        var contentWidth = unit * repeats;
        var totalWidth = contentWidth + 1920;

        using var surface = CreateSurface(totalWidth, TickerHeight);
        var canvas = surface.Canvas;
        using var text = Fill(new SKColor(0xE8, 0xEC, 0xF5));
        var baseline = TickerHeight / 2f + 12f;
        for (var i = 0; i < repeats + 1920 / unit + 2; i++)
        {
            canvas.DrawText(content, i * (float)unit, baseline, SKTextAlign.Left, font, text);
        }

        Save(surface, path);
        return contentWidth;
    }

    /// <summary>Intro card: crown, channel name, episode title, tagline.</summary>
    public void RenderTitleCard(string path, string title, BrandStyle brand)
    {
        using var surface = CreateSurface(CardWidth, CardHeight);
        var canvas = surface.Canvas;
        DrawCardBackground(canvas, brand);

        DrawCrown(canvas, brand.Accent, CardWidth / 2f, 220, 1.6f);

        using var channelFont = new SKFont(fonts.HeadlineMedium, 72);
        using var accentPaint = Fill(brand.Accent);
        TextLayout.DrawCentered(canvas, brand.ChannelName, CardWidth / 2f, 420, channelFont, accentPaint);

        using var divider = Fill(brand.Accent.WithAlpha(160));
        canvas.DrawRect(CardWidth / 2f - 220, 458, 440, 4, divider);

        var (lines, size) = TextLayout.WrapToWidth(fonts.HeadlineBold, title, CardWidth - 400, 120, 64, 2);
        using var titleFont = new SKFont(fonts.HeadlineBold, size);
        using var titlePaint = Fill(SKColors.White);
        var y = lines.Count == 1 ? 640f : 600f;
        foreach (var line in lines)
        {
            TextLayout.DrawCentered(canvas, line, CardWidth / 2f, y, titleFont, titlePaint);
            y += size * 1.15f;
        }

        if (brand.Tagline.Length > 0)
        {
            using var taglineFont = new SKFont(fonts.SansRegular, 44);
            using var taglinePaint = Fill(new SKColor(0xB9, 0xC4, 0xDE));
            TextLayout.DrawCentered(canvas, brand.Tagline, CardWidth / 2f, 900, taglineFont, taglinePaint);
        }

        Save(surface, path);
    }

    /// <summary>Outro card. On-screen copy is English, like the channel.</summary>
    public void RenderOutroCard(string path, BrandStyle brand)
    {
        using var surface = CreateSurface(CardWidth, CardHeight);
        var canvas = surface.Canvas;
        DrawCardBackground(canvas, brand);

        DrawCrown(canvas, brand.Accent, CardWidth / 2f, 250, 1.4f);

        using var thanksFont = new SKFont(fonts.HeadlineBold, 104);
        using var white = Fill(SKColors.White);
        TextLayout.DrawCentered(canvas, "Thank you for watching", CardWidth / 2f, 560, thanksFont, white);

        using var channelFont = new SKFont(fonts.HeadlineMedium, 64);
        using var accentPaint = Fill(brand.Accent);
        TextLayout.DrawCentered(canvas, brand.ChannelName, CardWidth / 2f, 700, channelFont, accentPaint);

        using var subFont = new SKFont(fonts.SansRegular, 42);
        using var subPaint = Fill(new SKColor(0xB9, 0xC4, 0xDE));
        TextLayout.DrawCentered(
            canvas,
            "New fact-checks regularly. Subscribe so you never miss one.",
            CardWidth / 2f,
            830,
            subFont,
            subPaint);

        Save(surface, path);
    }

    /// <summary>700x520 framed episode image for the side panel, ready to overlay.</summary>
    public void RenderImagePanel(string path, string imagePath, BrandStyle brand)
    {
        const int width = 700;
        const int height = 520;
        using var surface = CreateSurface(width, height);
        var canvas = surface.Canvas;

        var frame = new SKRect(10, 10, width - 10, height - 10);
        using var shadow = Fill(new SKColor(0, 0, 0, 90));
        canvas.DrawRoundRect(new SKRect(16, 18, width - 4, height - 2), 16, 16, shadow);
        using var border = Fill(SKColors.White);
        canvas.DrawRoundRect(frame, 14, 14, border);
        using var accent = Fill(brand.Accent);
        canvas.DrawRoundRect(new SKRect(frame.Left, frame.Bottom - 8, frame.Right, frame.Bottom), 4, 4, accent);

        using var bitmap = SKBitmap.Decode(imagePath);
        if (bitmap is not null)
        {
            using var image = SKImage.FromBitmap(bitmap);
            var inner = new SKRect(frame.Left + 8, frame.Top + 8, frame.Right - 8, frame.Bottom - 14);
            canvas.Save();
            canvas.ClipRoundRect(new SKRoundRect(inner, 8), antialias: true);
            DrawCover(canvas, image, inner);
            canvas.Restore();
        }

        Save(surface, path);
    }

    /// <summary>1280x720 thumbnail: title left, episode image (if any) right.</summary>
    public void RenderThumbnail(string path, string title, string? imagePath, BrandStyle brand)
    {
        const int width = 1280;
        const int height = 720;
        using var surface = CreateSurface(width, height);
        var canvas = surface.Canvas;

        using (var gradient = new SKPaint())
        {
            gradient.Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0),
                new SKPoint(width, height),
                [brand.Primary, brand.PrimaryDark],
                SKShaderTileMode.Clamp);
            canvas.DrawRect(0, 0, width, height, gradient);
        }

        using (var chip = Fill(brand.Accent))
        using (var chipFont = new SKFont(fonts.SansBold, 34))
        using (var chipText = Fill(BrandStyle.Ink))
        {
            var label = brand.ChannelName.ToUpperInvariant();
            var chipWidth = new SKFont(fonts.SansBold, 34).MeasureText(label) + 48;
            canvas.DrawRoundRect(new SKRect(56, 48, 56 + chipWidth, 116), 10, 10, chip);
            canvas.DrawText(label, 80, 96, SKTextAlign.Left, chipFont, chipText);
        }

        DrawCrown(canvas, brand.Accent, width - 130, 90, 0.75f);

        var hasImage = imagePath is not null && File.Exists(imagePath);
        var textWidth = hasImage ? width * 0.52f : width - 160f;
        var (lines, size) = TextLayout.WrapToWidth(fonts.HeadlineBold, title, textWidth, 110, 56, 3);
        using var titleFont = new SKFont(fonts.HeadlineBold, size);
        using var shadow = Fill(new SKColor(0, 0, 0, 140));
        using var titlePaint = Fill(SKColors.White);
        var y = height / 2f - (lines.Count - 1) * size * 0.6f + 40;
        foreach (var line in lines)
        {
            canvas.DrawText(line, 60 + 4, y + 4, SKTextAlign.Left, titleFont, shadow);
            canvas.DrawText(line, 60, y, SKTextAlign.Left, titleFont, titlePaint);
            y += size * 1.12f;
        }

        if (hasImage)
        {
            using var bitmap = SKBitmap.Decode(imagePath);
            if (bitmap is not null)
            {
                var frame = new SKRect(width - 480, 160, width - 60, 560);
                canvas.Save();
                canvas.RotateDegrees(-3, frame.MidX, frame.MidY);
                using var border = Fill(SKColors.White);
                canvas.DrawRoundRect(SKRect.Inflate(frame, 10, 10), 14, 14, border);
                using var image = SKImage.FromBitmap(bitmap);
                canvas.Save();
                canvas.ClipRoundRect(new SKRoundRect(frame, 8), antialias: true);
                DrawCover(canvas, image, frame);
                canvas.Restore();
                canvas.Restore();
            }
        }

        Save(surface, path);
    }

    private void DrawCardBackground(SKCanvas canvas, BrandStyle brand)
    {
        using var gradient = new SKPaint();
        gradient.Shader = SKShader.CreateLinearGradient(
            new SKPoint(0, 0),
            new SKPoint(0, CardHeight),
            [brand.Primary, brand.PrimaryDark],
            SKShaderTileMode.Clamp);
        canvas.DrawRect(0, 0, CardWidth, CardHeight, gradient);

        using var vignette = new SKPaint();
        vignette.Shader = SKShader.CreateRadialGradient(
            new SKPoint(CardWidth / 2f, CardHeight / 2f),
            CardWidth * 0.7f,
            [SKColors.Transparent, new SKColor(0, 0, 0, 110)],
            SKShaderTileMode.Clamp);
        canvas.DrawRect(0, 0, CardWidth, CardHeight, vignette);
    }

    private static void DrawCover(SKCanvas canvas, SKImage image, SKRect target)
    {
        var scale = Math.Max(target.Width / image.Width, target.Height / image.Height);
        var w = image.Width * scale;
        var h = image.Height * scale;
        var rect = new SKRect(
            target.MidX - w / 2,
            target.MidY - h / 2,
            target.MidX + w / 2,
            target.MidY + h / 2);
        canvas.DrawImage(image, rect, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
    }

    private static void DrawCrown(SKCanvas canvas, SKColor color, float centerX, float centerY, float scale)
    {
        using var paint = Fill(color);
        canvas.Save();
        canvas.Translate(centerX - 62 * scale, centerY - 40 * scale);
        canvas.Scale(scale);

        var builder = new SKPathBuilder();
        builder.MoveTo(0, 60);
        builder.LineTo(12, 18);
        builder.LineTo(40, 44);
        builder.LineTo(62, 6);
        builder.LineTo(84, 44);
        builder.LineTo(112, 18);
        builder.LineTo(124, 60);
        builder.Close();
        using var path = builder.Detach();
        canvas.DrawPath(path, paint);
        canvas.DrawRoundRect(new SKRect(0, 64, 124, 78), 6, 6, paint);
        canvas.DrawCircle(12, 14, 7, paint);
        canvas.DrawCircle(62, 2, 7, paint);
        canvas.DrawCircle(112, 14, 7, paint);
        canvas.Restore();
    }

    private static SKSurface CreateSurface(int width, int height) =>
        SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));

    private static SKPaint Fill(SKColor color) => new() { Color = color, IsAntialias = true };

    private static void Save(SKSurface surface, string path)
    {
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(path);
        data.SaveTo(stream);
    }
}

using System.Globalization;
using System.Text;
using RoyalNewsDesk.Core.Models;
using RoyalNewsDesk.Core.Video;

namespace RoyalNewsDesk.Core.Subtitles;

/// <summary>
/// Builds subtitle cues straight from the timeline: no speech recognition,
/// the timings are already sample-exact.
/// </summary>
public static class SrtWriter
{
    private const int MaxLineLength = 42;
    private const int MaxCueLength = MaxLineLength * 2;
    private const double MinCueSeconds = 1.0;
    private const double CueGapSeconds = 0.08;

    public static IReadOnlyList<SubtitleCue> BuildCues(Timeline timeline)
    {
        var cues = new List<SubtitleCue>();
        foreach (var sentence in timeline.Sentences)
        {
            var chunks = SplitIntoChunks(sentence.Text);
            var totalChars = chunks.Sum(c => c.Length);
            var start = sentence.Start;
            var duration = sentence.End - sentence.Start;

            for (var i = 0; i < chunks.Count; i++)
            {
                var share = totalChars == 0 ? 1.0 / chunks.Count : chunks[i].Length / (double)totalChars;
                var end = i == chunks.Count - 1 ? sentence.End : start + duration * share;
                cues.Add(new SubtitleCue(
                    TimeSpan.FromSeconds(Math.Round(start, 2)),
                    TimeSpan.FromSeconds(Math.Round(end, 2)),
                    Wrap(chunks[i])));
                start = end;
            }
        }

        // Enforce minimum duration by borrowing from the following silence.
        for (var i = 0; i < cues.Count; i++)
        {
            var cue = cues[i];
            var minEnd = cue.Start + TimeSpan.FromSeconds(MinCueSeconds);
            if (cue.End < minEnd)
            {
                var cap = i + 1 < cues.Count
                    ? cues[i + 1].Start - TimeSpan.FromSeconds(CueGapSeconds)
                    : TimeSpan.FromSeconds(timeline.TotalDuration);
                cues[i] = cue with { End = TimeSpan.FromTicks(Math.Max(cue.End.Ticks, Math.Min(minEnd.Ticks, cap.Ticks))) };
            }
        }

        return cues;
    }

    public static void WriteSrt(string path, IReadOnlyList<SubtitleCue> cues)
    {
        var text = new StringBuilder();
        for (var i = 0; i < cues.Count; i++)
        {
            text.Append((i + 1).ToString(CultureInfo.InvariantCulture)).Append("\r\n");
            text.Append(FormatSrtTime(cues[i].Start))
                .Append(" --> ")
                .Append(FormatSrtTime(cues[i].End))
                .Append("\r\n");
            text.Append(cues[i].Text).Append("\r\n\r\n");
        }

        // BOM on purpose: it maximizes player compatibility, and YouTube accepts it.
        File.WriteAllText(path, text.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    /// <summary>Styled ASS for optional burn-in, with body-local times.</summary>
    public static void WriteAss(string path, IReadOnlyList<SubtitleCue> cues, Timeline timeline)
    {
        var text = new StringBuilder();
        text.Append("[Script Info]\n");
        text.Append("PlayResX: 1920\nPlayResY: 1080\nWrapStyle: 0\n\n");
        text.Append("[V4+ Styles]\n");
        text.Append("Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding\n");
        text.Append("Style: News,IBM Plex Sans,46,&H00FFFFFF,&H00FFFFFF,&H00141A20,&H96000000,-1,0,0,0,100,100,0,0,1,2,1,2,240,240,140,1\n\n");
        text.Append("[Events]\n");
        text.Append("Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text\n");

        foreach (var cue in cues)
        {
            var start = timeline.ToBodyLocal(cue.Start.TotalSeconds);
            var end = timeline.ToBodyLocal(cue.End.TotalSeconds);
            if (end <= 0)
            {
                continue;
            }

            text.Append("Dialogue: 0,")
                .Append(FormatAssTime(Math.Max(0, start)))
                .Append(',')
                .Append(FormatAssTime(end))
                .Append(",News,,0,0,0,,")
                .Append(cue.Text.Replace("\r\n", "\\N", StringComparison.Ordinal))
                .Append('\n');
        }

        File.WriteAllText(path, text.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static List<string> SplitIntoChunks(string text)
    {
        if (text.Length <= MaxCueLength)
        {
            return [text];
        }

        var chunks = new List<string>();
        var remaining = text;
        while (remaining.Length > MaxCueLength)
        {
            var breakAt = FindClauseBreak(remaining);
            chunks.Add(remaining[..breakAt].Trim());
            remaining = remaining[breakAt..].Trim();
        }

        if (remaining.Length > 0)
        {
            chunks.Add(remaining);
        }

        return chunks;
    }

    private static int FindClauseBreak(string text)
    {
        string[] separators = ["; ", ", ", ": "];
        var best = -1;
        foreach (var separator in separators)
        {
            var index = text.LastIndexOf(separator, Math.Min(MaxCueLength, text.Length - 1), StringComparison.Ordinal);
            if (index > best && index >= 20)
            {
                best = index + separator.Length - 1;
            }
        }

        if (best > 0)
        {
            return best;
        }

        var space = text.LastIndexOf(' ', Math.Min(MaxCueLength, text.Length - 1));
        return space > 20 ? space : Math.Min(MaxCueLength, text.Length);
    }

    private static string Wrap(string chunk)
    {
        if (chunk.Length <= MaxLineLength)
        {
            return chunk;
        }

        var middle = chunk.Length / 2;
        var best = -1;
        for (var distance = 0; distance < middle; distance++)
        {
            foreach (var candidate in new[] { middle - distance, middle + distance })
            {
                if (candidate > 0 && candidate < chunk.Length && chunk[candidate] == ' ')
                {
                    best = candidate;
                    distance = middle;
                    break;
                }
            }
        }

        return best < 0 ? chunk : chunk[..best] + "\r\n" + chunk[(best + 1)..];
    }

    private static string FormatSrtTime(TimeSpan time) => string.Create(
        CultureInfo.InvariantCulture,
        $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00},{time.Milliseconds:000}");

    private static string FormatAssTime(double seconds)
    {
        var time = TimeSpan.FromSeconds(seconds);
        var centiseconds = (int)Math.Round(time.Milliseconds / 10.0);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}.{Math.Min(99, centiseconds):00}");
    }
}

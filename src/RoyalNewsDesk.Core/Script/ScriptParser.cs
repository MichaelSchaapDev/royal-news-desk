using System.Globalization;
using RoyalNewsDesk.Core.Models;

namespace RoyalNewsDesk.Core.Script;

/// <summary>
/// Turns an episode's segments into a normalized speech plan at produce time.
/// Everything unrecognized degrades to a warning; the only fatal outcome is a
/// script with no speakable text at all.
/// </summary>
public static class ScriptParser
{
    private const double DefaultPauseSeconds = 0.8;

    private static readonly string[] KnownDirectives = ["IMAGE", "PAUSE"];

    public static SpeechPlan Plan(Episode episode, string imagesDir)
    {
        var warnings = new List<PipelineWarning>();
        var segments = new List<PlannedSegment>();

        for (var segmentIndex = 0; segmentIndex < episode.Segments.Count; segmentIndex++)
        {
            var segment = episode.Segments[segmentIndex];
            var items = new List<SpeakItem>();
            var imageFile = ResolveImage(segment, imagesDir, warnings);
            var paragraphIndex = 0;
            var paragraphLines = new List<string>();
            var lines = segment.Body.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

            void CloseParagraph(int lineNumber)
            {
                if (paragraphLines.Count == 0)
                {
                    return;
                }

                var paragraph = TextNormalizer.NormalizeParagraph(string.Join(' ', paragraphLines));
                paragraphLines.Clear();
                if (paragraph.Length == 0)
                {
                    return;
                }

                foreach (var sentence in SentenceSplitter.Split(paragraph, lineNumber, warnings))
                {
                    items.Add(new SpeakSentence(
                        TextNormalizer.EnsureTerminalPunctuation(sentence),
                        paragraphIndex));
                }

                paragraphIndex++;
            }

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                var lineNumber = i + 1;

                if (line.Length == 0)
                {
                    CloseParagraph(lineNumber);
                    continue;
                }

                if (line.StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }

                if (line.StartsWith('[') && line.EndsWith(']'))
                {
                    CloseParagraph(lineNumber);
                    HandleDirective(line, lineNumber, items, warnings);
                    continue;
                }

                if (line.Contains('[', StringComparison.Ordinal) || line.Contains(']', StringComparison.Ordinal))
                {
                    warnings.Add(new PipelineWarning("W402", lineNumber, null));
                    line = StripInlineBrackets(line);
                    if (line.Length == 0)
                    {
                        continue;
                    }
                }

                paragraphLines.Add(line);
            }

            CloseParagraph(lines.Length);

            if (items.OfType<SpeakSentence>().Any())
            {
                segments.Add(new PlannedSegment(segments.Count, segment.Headline, imageFile, items));
            }
            else
            {
                warnings.Add(new PipelineWarning("W103", null, segment.Headline));
            }
        }

        if (!segments.SelectMany(s => s.Items).OfType<SpeakSentence>().Any())
        {
            throw new ScriptEmptyException();
        }

        var title = string.IsNullOrWhiteSpace(episode.Title)
            ? segments.Select(s => s.Headline).FirstOrDefault(h => h is not null) ?? "Royal News Desk"
            : episode.Title.Trim();

        return new SpeechPlan(title, segments, warnings);
    }

    private static void HandleDirective(
        string line,
        int lineNumber,
        List<SpeakItem> items,
        List<PipelineWarning> warnings)
    {
        var inner = line[1..^1].Trim();
        var name = inner;
        string? argument = null;
        var colon = inner.IndexOf(':', StringComparison.Ordinal);
        if (colon >= 0)
        {
            name = inner[..colon].Trim();
            argument = inner[(colon + 1)..].Trim();
        }

        if (name.Equals("PAUSE", StringComparison.OrdinalIgnoreCase))
        {
            items.Add(new SpeakPause(ParsePauseSeconds(argument, lineNumber, warnings)));
            return;
        }

        if (name.Equals("IMAGE", StringComparison.OrdinalIgnoreCase))
        {
            // Images attach via Segment.ImageFile (the editor buttons). A
            // stray [IMAGE] in the text is noted, never spoken.
            warnings.Add(new PipelineWarning("W202", lineNumber, argument));
            return;
        }

        var suggestion = KnownDirectives
            .FirstOrDefault(d => LevenshteinDistance(name.ToUpperInvariant(), d) <= 2);
        warnings.Add(new PipelineWarning("W401", lineNumber, suggestion is null ? name : name + "|" + suggestion));
    }

    private static double ParsePauseSeconds(string? argument, int lineNumber, List<PipelineWarning> warnings)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            return DefaultPauseSeconds;
        }

        // Mom is Dutch: accept both "1.5" and "1,5".
        var normalized = argument.Replace(',', '.');
        if (!double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
        {
            warnings.Add(new PipelineWarning("W302", lineNumber, argument));
            return DefaultPauseSeconds;
        }

        var clamped = Math.Clamp(seconds, 0.2, 5.0);
        if (Math.Abs(clamped - seconds) > 0.0001)
        {
            warnings.Add(new PipelineWarning("W301", lineNumber, argument));
        }

        return clamped;
    }

    private static string? ResolveImage(Segment segment, string imagesDir, List<PipelineWarning> warnings)
    {
        if (segment.ImageFile is null)
        {
            return null;
        }

        var path = Path.Combine(imagesDir, segment.ImageFile);
        if (File.Exists(path))
        {
            return segment.ImageFile;
        }

        warnings.Add(new PipelineWarning("W201", null, segment.ImageFile));
        return null;
    }

    private static string StripInlineBrackets(string line)
    {
        var result = new System.Text.StringBuilder(line.Length);
        var depth = 0;
        foreach (var c in line)
        {
            if (c == '[')
            {
                depth++;
            }
            else if (c == ']')
            {
                depth = Math.Max(0, depth - 1);
            }
            else if (depth == 0)
            {
                result.Append(c);
            }
        }

        return string.Join(' ', result.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries)).Trim();
    }

    private static int LevenshteinDistance(string a, string b)
    {
        var costs = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
        {
            costs[j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            var previous = costs[0];
            costs[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var current = costs[j];
                costs[j] = Math.Min(
                    Math.Min(costs[j] + 1, costs[j - 1] + 1),
                    previous + (a[i - 1] == b[j - 1] ? 0 : 1));
                previous = current;
            }
        }

        return costs[b.Length];
    }
}

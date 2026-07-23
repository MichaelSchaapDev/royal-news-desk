using System.Text;
using RoyalNewsDesk.Core.Models;

namespace RoyalNewsDesk.Core.Script;

/// <summary>Result of splitting a pasted script into editable segments.</summary>
public sealed record ImportedScript(
    string? Title,
    IReadOnlyList<ImportedSegment> Segments,
    IReadOnlyList<PipelineWarning> Warnings);

public sealed record ImportedSegment(string? Headline, string Body);

/// <summary>
/// Turns a full pasted script into segments for the editor. Light touch on
/// purpose: it splits on '#' headlines and keeps paragraph text (including
/// [PAUSE] and [IMAGE] lines) as the segment body; the produce-time parser
/// interprets those.
/// </summary>
public static class ScriptImporter
{
    public static ImportedScript Import(string pastedText)
    {
        var warnings = new List<PipelineWarning>();
        string? title = null;
        var segments = new List<ImportedSegment>();
        string? currentHeadline = null;
        var currentBody = new StringBuilder();
        var sawContent = false;

        void CloseSegment()
        {
            var body = currentBody.ToString().Trim();
            if (body.Length == 0 && currentHeadline is null)
            {
                return;
            }

            segments.Add(new ImportedSegment(currentHeadline, body));
            currentHeadline = null;
            currentBody.Clear();
        }

        var lines = pastedText.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd();
            var lineNumber = i + 1;

            if (line.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            if (line.StartsWith("TITLE:", StringComparison.OrdinalIgnoreCase) && !sawContent)
            {
                var value = line[6..].Trim();
                if (title is null)
                {
                    title = value;
                }
                else
                {
                    warnings.Add(new PipelineWarning("W101", lineNumber, value));
                }

                continue;
            }

            if (line.StartsWith('#'))
            {
                CloseSegment();
                var headline = line.TrimStart('#').Trim();
                if (headline.Length == 0)
                {
                    warnings.Add(new PipelineWarning("W102", lineNumber, null));
                    currentHeadline = null;
                }
                else
                {
                    currentHeadline = headline;
                }

                sawContent = true;
                continue;
            }

            if (line.Trim().Length > 0)
            {
                sawContent = true;
            }

            currentBody.AppendLine(line);
        }

        CloseSegment();

        // Segments with a headline but no text would render nothing; drop them.
        var kept = new List<ImportedSegment>();
        foreach (var segment in segments)
        {
            if (HasSpeakableText(segment.Body))
            {
                kept.Add(segment);
            }
            else
            {
                warnings.Add(new PipelineWarning("W103", null, segment.Headline));
            }
        }

        return new ImportedScript(title, kept, warnings);
    }

    private static bool HasSpeakableText(string body)
    {
        foreach (var raw in body.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                continue;
            }

            return true;
        }

        return false;
    }
}

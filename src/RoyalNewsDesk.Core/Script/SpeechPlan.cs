using RoyalNewsDesk.Core.Models;

namespace RoyalNewsDesk.Core.Script;

/// <summary>One thing to speak or a silence to hold, in order.</summary>
public abstract record SpeakItem;

/// <param name="ParagraphIndex">0-based paragraph within the segment; drives gap sizes.</param>
public sealed record SpeakSentence(string Text, int ParagraphIndex) : SpeakItem;

/// <summary>Extra silence requested with [PAUSE].</summary>
public sealed record SpeakPause(double Seconds) : SpeakItem;

public sealed record PlannedSegment(
    int Index,
    string? Headline,
    string? ImageFile,
    IReadOnlyList<SpeakItem> Items);

/// <summary>Everything the audio and video stages need, parsed and normalized once.</summary>
public sealed record SpeechPlan(
    string Title,
    IReadOnlyList<PlannedSegment> Segments,
    IReadOnlyList<PipelineWarning> Warnings)
{
    public IEnumerable<SpeakSentence> AllSentences =>
        Segments.SelectMany(s => s.Items).OfType<SpeakSentence>();
}

/// <summary>The script contains nothing to speak; producing is pointless.</summary>
public sealed class ScriptEmptyException : Exception
{
    public ScriptEmptyException()
        : base("The script has no speakable text (E100).")
    {
    }
}

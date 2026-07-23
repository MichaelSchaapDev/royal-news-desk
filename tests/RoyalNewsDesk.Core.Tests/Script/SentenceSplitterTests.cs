using RoyalNewsDesk.Core.Models;
using RoyalNewsDesk.Core.Script;

namespace RoyalNewsDesk.Core.Tests.Script;

public class SentenceSplitterTests
{
    private static List<string> Split(string text)
    {
        var warnings = new List<PipelineWarning>();
        return SentenceSplitter.Split(text, 1, warnings);
    }

    [Fact]
    public void SplitsPlainSentences()
    {
        var result = Split("The King visited Scotland. The Queen stayed home. All was well.");
        Assert.Equal(3, result.Count);
        Assert.Equal("The King visited Scotland.", result[0]);
    }

    [Fact]
    public void KeepsAbbreviationsTogether()
    {
        // Protection is deliberately conservative: "H.R.H. The" could be a real
        // boundary, but a missed split only makes one longer sentence, while a
        // wrong split breaks prosody mid-title.
        var result = Split("Dr. Smith spoke with H.R.H. The audience listened closely.");
        var sentence = Assert.Single(result);
        Assert.Contains("Dr. Smith", sentence, StringComparison.Ordinal);
        Assert.Contains("H.R.H. The audience", sentence, StringComparison.Ordinal);
    }

    [Fact]
    public void KeepsDecimalsTogether()
    {
        var result = Split("The crowd was 2.5 times larger than 2024. Nobody expected that.");
        Assert.Equal(2, result.Count);
        Assert.Contains("2.5", result[0], StringComparison.Ordinal);
    }

    [Fact]
    public void KeepsInitialsTogether()
    {
        var result = Split("Reporter J. Smith asked the question. The palace declined.");
        Assert.Equal(2, result.Count);
        Assert.Contains("J. Smith", result[0], StringComparison.Ordinal);
    }

    [Fact]
    public void HandlesQuestionsAndExclamations()
    {
        var result = Split("Did the ban exist? No! It never did.");
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void SwallowsClosingQuotes()
    {
        var result = Split("She said \"good night.\" The desk went quiet.");
        Assert.Equal(2, result.Count);
        Assert.EndsWith("good night.\"", result[0], StringComparison.Ordinal);
    }

    [Fact]
    public void ForceSplitsMonsterSentences()
    {
        var warnings = new List<PipelineWarning>();
        var longSentence = string.Join(", ", Enumerable.Repeat("the palace released another statement", 20)) + ".";
        var result = SentenceSplitter.Split(longSentence, 7, warnings);

        Assert.True(result.Count >= 2);
        Assert.Contains(warnings, w => w.Code == "W501" && w.LineNumber == 7);
    }
}

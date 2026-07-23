using RoyalNewsDesk.Core.Script;

namespace RoyalNewsDesk.Core.Tests.Script;

public class TextNormalizerTests
{
    [Fact]
    public void ReplacesSmartQuotes()
    {
        Assert.Equal("She said \"hello\" and 'bye'.", TextNormalizer.NormalizeParagraph("She said “hello” and ‘bye’."));
    }

    [Fact]
    public void TurnsDashesIntoCommaPauses()
    {
        Assert.Equal("The King, they say, agreed.", TextNormalizer.NormalizeParagraph("The King — they say — agreed."));
    }

    [Fact]
    public void ReplacesAmpersand()
    {
        Assert.Equal("Fact and fiction.", TextNormalizer.NormalizeParagraph("Fact & fiction."));
    }

    [Fact]
    public void StripsEmoji()
    {
        Assert.Equal("Great news.", TextNormalizer.NormalizeParagraph("Great news. \U0001F451\U0001F604"));
    }

    [Fact]
    public void CollapsesWhitespace()
    {
        Assert.Equal("One two three.", TextNormalizer.NormalizeParagraph("One   two\t three."));
    }

    [Theory]
    [InlineData("No punctuation", "No punctuation.")]
    [InlineData("Already fine.", "Already fine.")]
    [InlineData("Really?", "Really?")]
    [InlineData("He said \"stop.\"", "He said \"stop.\"")]
    public void EnsuresTerminalPunctuation(string input, string expected)
    {
        Assert.Equal(expected, TextNormalizer.EnsureTerminalPunctuation(input));
    }
}

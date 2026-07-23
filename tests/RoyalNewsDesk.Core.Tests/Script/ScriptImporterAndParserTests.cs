using RoyalNewsDesk.Core.Models;
using RoyalNewsDesk.Core.Script;

namespace RoyalNewsDesk.Core.Tests.Script;

public class ScriptImporterAndParserTests
{
    private const string SampleScript = """
        TITLE: Royal Rumour Check

        // an editorial note, never spoken

        Good evening, and welcome to the Royal News Desk.

        # Palace Denies Abdication Plan
        A viral post claimed the King signed papers. The truth is duller.

        [PAUSE: 1,5]

        The palace called the claim baseless.

        # Empty One

        # The Corgi Story
        The dog belongs to a baker. Her name is Biscuit.
        """;

    [Fact]
    public void ImporterSplitsSegmentsAndTitle()
    {
        var imported = ScriptImporter.Import(SampleScript);

        Assert.Equal("Royal Rumour Check", imported.Title);
        Assert.Equal(3, imported.Segments.Count);
        Assert.Null(imported.Segments[0].Headline);
        Assert.Equal("Palace Denies Abdication Plan", imported.Segments[1].Headline);
        Assert.Equal("The Corgi Story", imported.Segments[2].Headline);
        Assert.Contains(imported.Warnings, w => w.Code == "W103" && w.Detail == "Empty One");
        Assert.Contains("[PAUSE: 1,5]", imported.Segments[1].Body, StringComparison.Ordinal);
    }

    [Fact]
    public void ParserBuildsSpeechPlanWithPausesAndParagraphs()
    {
        using var temp = new TempDir();
        var imported = ScriptImporter.Import(SampleScript);
        var episode = ToEpisode(imported);

        var plan = ScriptParser.Plan(episode, temp.Path);

        Assert.Equal("Royal Rumour Check", plan.Title);
        Assert.Equal(3, plan.Segments.Count);

        var second = plan.Segments[1];
        var pause = Assert.Single(second.Items.OfType<SpeakPause>());
        Assert.Equal(1.5, pause.Seconds);

        var sentences = second.Items.OfType<SpeakSentence>().ToList();
        Assert.Equal(3, sentences.Count);
        Assert.Equal(0, sentences[0].ParagraphIndex);
        Assert.Equal(1, sentences[2].ParagraphIndex);
    }

    [Fact]
    public void UnknownDirectiveIsSkippedWithSuggestion()
    {
        using var temp = new TempDir();
        var episode = new Episode { Id = "e1", Title = "T" };
        episode.Segments.Add(new Segment
        {
            Id = "s1",
            Body = "Real text here.\n\n[IMGE: corgi.jpg]\n\nMore text follows.",
        });

        var plan = ScriptParser.Plan(episode, temp.Path);

        var warning = Assert.Single(plan.Warnings, w => w.Code == "W401");
        Assert.Contains("IMAGE", warning.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain(
            plan.AllSentences,
            s => s.Text.Contains("IMGE", StringComparison.Ordinal));
    }

    [Fact]
    public void InlineBracketsAreStrippedNotSpoken()
    {
        using var temp = new TempDir();
        var episode = new Episode { Id = "e1", Title = "T" };
        episode.Segments.Add(new Segment { Id = "s1", Body = "The King [dramatic pause] waved." });

        var plan = ScriptParser.Plan(episode, temp.Path);

        var sentence = Assert.Single(plan.AllSentences);
        Assert.Equal("The King waved.", sentence.Text);
        Assert.Contains(plan.Warnings, w => w.Code == "W402");
    }

    [Fact]
    public void MissingImageWarnsAndDrops()
    {
        using var temp = new TempDir();
        var episode = new Episode { Id = "e1", Title = "T" };
        episode.Segments.Add(new Segment { Id = "s1", Body = "Text.", ImageFile = "gone.jpg" });

        var plan = ScriptParser.Plan(episode, temp.Path);

        Assert.Null(plan.Segments[0].ImageFile);
        Assert.Contains(plan.Warnings, w => w.Code == "W201" && w.Detail == "gone.jpg");
    }

    [Fact]
    public void PresentImageIsKept()
    {
        using var temp = new TempDir();
        File.WriteAllText(Path.Combine(temp.Path, "seg.jpg"), "x");
        var episode = new Episode { Id = "e1", Title = "T" };
        episode.Segments.Add(new Segment { Id = "s1", Body = "Text.", ImageFile = "seg.jpg" });

        var plan = ScriptParser.Plan(episode, temp.Path);

        Assert.Equal("seg.jpg", plan.Segments[0].ImageFile);
    }

    [Fact]
    public void EmptyScriptThrows()
    {
        using var temp = new TempDir();
        var episode = new Episode { Id = "e1", Title = "T" };
        episode.Segments.Add(new Segment { Id = "s1", Body = "// only a comment" });

        Assert.Throws<ScriptEmptyException>(() => ScriptParser.Plan(episode, temp.Path));
    }

    private static Episode ToEpisode(ImportedScript imported)
    {
        var episode = new Episode { Id = "test", Title = imported.Title ?? "" };
        var index = 1;
        foreach (var segment in imported.Segments)
        {
            episode.Segments.Add(new Segment
            {
                Id = "seg-" + index.ToString("00", System.Globalization.CultureInfo.InvariantCulture),
                Headline = segment.Headline,
                Body = segment.Body,
            });
            index++;
        }

        return episode;
    }
}

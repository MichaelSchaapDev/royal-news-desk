using RoyalNewsDesk.Core.Models;
using RoyalNewsDesk.Core.Storage;

namespace RoyalNewsDesk.Core.Tests.Storage;

public class EpisodeStoreTests
{
    [Fact]
    public void SavesLoadsAndLists()
    {
        using var temp = new TempDir();
        var store = new JsonEpisodeStore(new AppPaths(temp.Path));

        var episode = store.CreateNew(new DateTime(2026, 7, 23, 14, 30, 0, DateTimeKind.Utc), "en_GB-cori-high");
        episode.Title = "Kroningsportret feiten";
        episode.Segments.Add(new Segment { Id = "seg-01", Headline = "Test", Body = "Hello." });
        store.Save(episode);

        var loaded = store.Load(episode.Id);
        Assert.NotNull(loaded);
        Assert.Equal("Kroningsportret feiten", loaded.Title);
        Assert.Single(loaded.Segments);
        Assert.Equal("en_GB-cori-high", loaded.VoiceId);

        var list = store.List();
        var summary = Assert.Single(list);
        Assert.Equal(episode.Id, summary.Id);
        Assert.False(summary.HasOutput);
    }

    [Fact]
    public void NewIdsAreUniqueAndPathSafe()
    {
        using var temp = new TempDir();
        var store = new JsonEpisodeStore(new AppPaths(temp.Path));
        var now = DateTime.UtcNow;

        var ids = Enumerable.Range(0, 50).Select(_ => store.CreateNew(now, "v").Id).ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.All(ids, id => Assert.DoesNotContain(
            Path.GetInvalidFileNameChars(),
            c => id.Contains(c, StringComparison.Ordinal)));
    }

    [Fact]
    public void LegacyEpisodeWithoutPresenterStyleLoadsAsAnimated()
    {
        using var temp = new TempDir();
        var store = new JsonEpisodeStore(new AppPaths(temp.Path));
        var paths = store.PathsFor("legacy-01");
        paths.EnsureCreated();
        File.WriteAllText(paths.EpisodeFile, """
            { "schemaVersion": 1, "id": "legacy-01", "title": "Old", "voiceId": "v", "segments": [], "tickerItems": [] }
            """);

        var loaded = store.Load("legacy-01");

        Assert.NotNull(loaded);
        Assert.Equal(Core.Presenters.PresenterStyle.Animated, loaded.PresenterStyle);
    }

    [Fact]
    public void PhotorealStyleRoundTrips()
    {
        using var temp = new TempDir();
        var store = new JsonEpisodeStore(new AppPaths(temp.Path));
        var episode = store.CreateNew(DateTime.UtcNow, "v");
        episode.PresenterStyle = Core.Presenters.PresenterStyle.Photoreal;
        store.Save(episode);

        var loaded = store.Load(episode.Id);
        Assert.NotNull(loaded);
        Assert.Equal(Core.Presenters.PresenterStyle.Photoreal, loaded.PresenterStyle);
        Assert.Contains("\"presenterStyle\": \"photoreal\"", File.ReadAllText(store.PathsFor(episode.Id).EpisodeFile), StringComparison.Ordinal);
    }

    [Fact]
    public void DeleteRemovesTheFolder()
    {
        using var temp = new TempDir();
        var store = new JsonEpisodeStore(new AppPaths(temp.Path));
        var episode = store.CreateNew(DateTime.UtcNow, "v");
        store.Save(episode);
        Assert.True(Directory.Exists(store.PathsFor(episode.Id).Root));

        store.Delete(episode.Id);

        Assert.False(Directory.Exists(store.PathsFor(episode.Id).Root));
        Assert.Empty(store.List());
    }
}

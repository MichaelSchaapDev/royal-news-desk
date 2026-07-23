using RoyalNewsDesk.Core.Models;
using RoyalNewsDesk.Core.Storage;

namespace RoyalNewsDesk.Core.Tests.Storage;

public class SettingsStoreTests
{
    [Fact]
    public void RoundTripsSettings()
    {
        using var temp = new TempDir();
        var store = new JsonSettingsStore(new AppPaths(temp.Path));

        var settings = new AppSettings
        {
            Language = "en",
            Theme = AppTheme.Dark,
            VoiceId = "en_GB-alba-medium",
            ReadingSpeed = 1.1,
            KeepWorkFiles = true,
        };
        settings.Branding.ChannelName = "Test Desk";
        store.Save(settings);

        var loaded = store.Load();
        Assert.Equal("en", loaded.Language);
        Assert.Equal(AppTheme.Dark, loaded.Theme);
        Assert.Equal("en_GB-alba-medium", loaded.VoiceId);
        Assert.Equal(1.1, loaded.ReadingSpeed);
        Assert.True(loaded.KeepWorkFiles);
        Assert.Equal("Test Desk", loaded.Branding.ChannelName);
    }

    [Fact]
    public void MissingFileYieldsDutchDefaults()
    {
        using var temp = new TempDir();
        var store = new JsonSettingsStore(new AppPaths(temp.Path));

        var loaded = store.Load();

        Assert.Equal("nl", loaded.Language);
        Assert.Equal(AppTheme.Light, loaded.Theme);
        Assert.False(string.IsNullOrWhiteSpace(loaded.OutputFolder));
    }

    [Fact]
    public void CorruptFileYieldsDefaults()
    {
        using var temp = new TempDir();
        var paths = new AppPaths(temp.Path);
        Directory.CreateDirectory(paths.DataRoot);
        File.WriteAllText(paths.SettingsFile, "{ this is not json");

        var store = new JsonSettingsStore(paths);
        var loaded = store.Load();

        Assert.Equal("nl", loaded.Language);
    }

    [Fact]
    public void UnknownPropertiesSurviveWithoutError()
    {
        using var temp = new TempDir();
        var paths = new AppPaths(temp.Path);
        Directory.CreateDirectory(paths.DataRoot);
        File.WriteAllText(paths.SettingsFile, """{ "language": "en", "someFutureSetting": 42 }""");

        var store = new JsonSettingsStore(paths);
        var loaded = store.Load();

        Assert.Equal("en", loaded.Language);
    }
}

using RoyalNewsDesk.Core.VoiceModels;

namespace RoyalNewsDesk.Core.Tests.VoiceModels;

public class VoiceCatalogTests
{
    [Fact]
    public void LoadsEmbeddedCatalog()
    {
        var catalog = VoiceCatalog.LoadEmbedded();

        Assert.Equal(3, catalog.Voices.Count);
        var cori = catalog.Find("en_GB-cori-high");
        Assert.NotNull(cori);
        Assert.Equal(2, cori.Files.Count);
        Assert.All(cori.Files, f =>
        {
            Assert.Equal(64, f.Sha256.Length);
            Assert.True(f.SizeBytes > 0);
            Assert.NotEmpty(f.Urls);
        });
        Assert.True(cori.TotalBytes > 100_000_000);
    }
}

using RoyalNewsDesk.Core.Presenters;

namespace RoyalNewsDesk.Core.Tests.Presenters;

public class PresenterCatalogTests
{
    [Fact]
    public void LoadsEmbeddedCatalog()
    {
        var catalog = PresenterCatalog.LoadEmbedded();

        Assert.Equal(2, catalog.Engines.Count);
        Assert.NotNull(catalog.Find("sadtalker-cpu"));
        var cuda = catalog.Find("sadtalker-cuda");
        Assert.NotNull(cuda);
        Assert.True(cuda.RequiresNvidiaGpu);

        Assert.All(catalog.Engines, engine =>
        {
            Assert.False(string.IsNullOrWhiteSpace(engine.Entrypoint));
            Assert.NotEmpty(engine.Files);
            Assert.All(engine.Files, file =>
            {
                Assert.Equal(64, file.Sha256.Length);
                Assert.All(file.Urls, url => Assert.StartsWith("https://", url, StringComparison.Ordinal));
                Assert.True(file.Extract);
            });
        });
    }
}

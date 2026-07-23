using System.Reflection;
using System.Text.Json;
using RoyalNewsDesk.Core.Storage;

namespace RoyalNewsDesk.Core.Presenters;

/// <summary>The photoreal engines the app can offer, from an embedded json file.</summary>
public sealed class PresenterCatalog
{
    public PresenterCatalog(IReadOnlyList<PresenterEngineInfo> engines)
    {
        Engines = engines;
    }

    public IReadOnlyList<PresenterEngineInfo> Engines { get; }

    public PresenterEngineInfo? Find(string engineId) => Engines.FirstOrDefault(e => e.Id == engineId);

    public static PresenterCatalog LoadEmbedded()
    {
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("RoyalNewsDesk.Core.Resources.presenters.json")
            ?? throw new InvalidOperationException("Embedded presenters.json is missing.");
        var doc = JsonSerializer.Deserialize<CatalogDoc>(stream, JsonDefaults.Options)
            ?? throw new InvalidOperationException("Embedded presenters.json is empty.");
        return new PresenterCatalog(doc.Engines);
    }

    private sealed class CatalogDoc
    {
        public List<PresenterEngineInfo> Engines { get; set; } = [];
    }
}

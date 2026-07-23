using System.Reflection;
using System.Text.Json;
using RoyalNewsDesk.Core.Storage;

namespace RoyalNewsDesk.Core.VoiceModels;

/// <summary>The list of voices the app can offer, from an embedded json file.</summary>
public sealed class VoiceCatalog
{
    public VoiceCatalog(IReadOnlyList<VoiceInfo> voices)
    {
        Voices = voices;
    }

    public IReadOnlyList<VoiceInfo> Voices { get; }

    public VoiceInfo? Find(string voiceId) => Voices.FirstOrDefault(v => v.Id == voiceId);

    public static VoiceCatalog LoadEmbedded()
    {
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("RoyalNewsDesk.Core.Resources.voices.json")
            ?? throw new InvalidOperationException("Embedded voices.json is missing.");
        var doc = JsonSerializer.Deserialize<CatalogDoc>(stream, JsonDefaults.Options)
            ?? throw new InvalidOperationException("Embedded voices.json is empty.");
        return new VoiceCatalog(doc.Voices);
    }

    private sealed class CatalogDoc
    {
        public List<VoiceInfo> Voices { get; set; } = [];
    }
}

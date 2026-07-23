using RoyalNewsDesk.Core.Models;

namespace RoyalNewsDesk.Core.Storage;

/// <summary>List row for the episodes page.</summary>
public sealed record EpisodeSummary(string Id, string Title, DateTime CreatedUtc, bool HasOutput);

public interface IEpisodeStore
{
    IReadOnlyList<EpisodeSummary> List();

    Episode? Load(string id);

    void Save(Episode episode);

    void Delete(string id);

    EpisodePaths PathsFor(string id);

    Episode CreateNew(DateTime utcNow, string voiceId);
}

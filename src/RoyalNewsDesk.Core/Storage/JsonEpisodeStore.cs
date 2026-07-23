using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using RoyalNewsDesk.Core.Models;

namespace RoyalNewsDesk.Core.Storage;

public sealed class JsonEpisodeStore(AppPaths paths) : IEpisodeStore
{
    private const string IdAlphabet = "abcdefghjkmnpqrstuvwxyz23456789";

    public IReadOnlyList<EpisodeSummary> List()
    {
        if (!Directory.Exists(paths.EpisodesRoot))
        {
            return [];
        }

        var summaries = new List<EpisodeSummary>();
        foreach (var dir in Directory.EnumerateDirectories(paths.EpisodesRoot))
        {
            var id = Path.GetFileName(dir);
            var episode = Load(id);
            if (episode is null)
            {
                continue;
            }

            var outDir = PathsFor(id).OutDir;
            var hasOutput = Directory.Exists(outDir)
                && Directory.EnumerateFiles(outDir, "*.mp4").Any();
            summaries.Add(new EpisodeSummary(episode.Id, episode.Title, episode.CreatedUtc, hasOutput));
        }

        return summaries.OrderByDescending(s => s.CreatedUtc).ToList();
    }

    public Episode? Load(string id)
    {
        var file = PathsFor(id).EpisodeFile;
        if (!File.Exists(file))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<Episode>(File.ReadAllText(file), JsonDefaults.Options);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    public void Save(Episode episode)
    {
        var episodePaths = PathsFor(episode.Id);
        episodePaths.EnsureCreated();
        var tempFile = episodePaths.EpisodeFile + ".tmp";
        File.WriteAllText(tempFile, JsonSerializer.Serialize(episode, JsonDefaults.Options));
        File.Move(tempFile, episodePaths.EpisodeFile, overwrite: true);
    }

    public void Delete(string id)
    {
        var root = PathsFor(id).Root;
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    public EpisodePaths PathsFor(string id) => new(Path.Combine(paths.EpisodesRoot, id));

    public Episode CreateNew(DateTime utcNow, string voiceId)
    {
        var id = utcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)
            + "-" + RandomSuffix(4);
        return new Episode
        {
            Id = id,
            CreatedUtc = utcNow,
            VoiceId = voiceId,
        };
    }

    private static string RandomSuffix(int length)
    {
        var bytes = new byte[length];
        RandomNumberGenerator.Fill(bytes);
        return string.Create(length, bytes, static (span, source) =>
        {
            for (var i = 0; i < span.Length; i++)
            {
                span[i] = IdAlphabet[source[i] % IdAlphabet.Length];
            }
        });
    }
}

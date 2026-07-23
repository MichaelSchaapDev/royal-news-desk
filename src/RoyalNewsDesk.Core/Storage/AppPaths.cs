namespace RoyalNewsDesk.Core.Storage;

/// <summary>
/// Single source of truth for where app data lives. Kept apart from the
/// Velopack install folder (which is replaced on every update) and from the
/// user-visible output folder.
/// </summary>
public sealed class AppPaths
{
    public AppPaths(string dataRoot)
    {
        DataRoot = dataRoot;
    }

    public static AppPaths CreateDefault() => new(
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RoyalNewsDeskStudio"));

    public string DataRoot { get; }

    public string SettingsFile => Path.Combine(DataRoot, "settings.json");

    public string ModelsRoot => Path.Combine(DataRoot, "models");

    public string EpisodesRoot => Path.Combine(DataRoot, "episodes");

    public string LogsRoot => Path.Combine(DataRoot, "logs");

    public string DefaultOutputFolder
    {
        get
        {
            var videos = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
            if (string.IsNullOrEmpty(videos))
            {
                videos = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Videos");
            }

            return Path.Combine(videos, "Royal News Desk");
        }
    }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(DataRoot);
        Directory.CreateDirectory(ModelsRoot);
        Directory.CreateDirectory(EpisodesRoot);
        Directory.CreateDirectory(LogsRoot);
    }
}

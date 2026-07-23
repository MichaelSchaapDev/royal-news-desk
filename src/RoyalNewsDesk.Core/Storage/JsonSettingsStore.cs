using System.Text.Json;
using RoyalNewsDesk.Core.Models;

namespace RoyalNewsDesk.Core.Storage;

public sealed class JsonSettingsStore(AppPaths paths) : ISettingsStore
{
    public AppSettings Load()
    {
        AppSettings settings;
        try
        {
            settings = File.Exists(paths.SettingsFile)
                ? JsonSerializer.Deserialize<AppSettings>(
                      File.ReadAllText(paths.SettingsFile),
                      JsonDefaults.Options) ?? new AppSettings()
                : new AppSettings();
        }
        catch (JsonException)
        {
            settings = new AppSettings();
        }
        catch (IOException)
        {
            settings = new AppSettings();
        }

        if (string.IsNullOrWhiteSpace(settings.OutputFolder))
        {
            settings.OutputFolder = paths.DefaultOutputFolder;
        }

        return settings;
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(paths.DataRoot);
        var tempFile = paths.SettingsFile + ".tmp";
        File.WriteAllText(tempFile, JsonSerializer.Serialize(settings, JsonDefaults.Options));
        File.Move(tempFile, paths.SettingsFile, overwrite: true);
    }
}

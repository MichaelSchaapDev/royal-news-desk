using RoyalNewsDesk.Core.Models;

namespace RoyalNewsDesk.Core.Storage;

public interface ISettingsStore
{
    /// <summary>Loads settings, falling back to defaults when the file is missing or unreadable.</summary>
    AppSettings Load();

    void Save(AppSettings settings);
}

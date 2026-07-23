namespace RoyalNewsDesk.App.Services;

/// <summary>Switches the main window between pages.</summary>
public interface INavigator
{
    void OpenEpisodes();

    void OpenSettings();

    void OpenAbout();

    void OpenEditor(string episodeId);
}

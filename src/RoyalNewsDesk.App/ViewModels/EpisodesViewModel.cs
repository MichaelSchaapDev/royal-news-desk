using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoyalNewsDesk.App.Resources;
using RoyalNewsDesk.App.Services;
using RoyalNewsDesk.Core.Storage;

namespace RoyalNewsDesk.App.ViewModels;

public sealed record EpisodeRow(string Id, string Title, string CreatedText, bool HasOutput, string? ThumbnailPath)
{
    public string StatusText => HasOutput ? Strings.Episodes_HasOutput : Strings.Episodes_NoOutput;

    public bool HasThumbnail => ThumbnailPath is not null;
}

public partial class EpisodesViewModel(
    IEpisodeStore store,
    ISettingsStore settingsStore,
    INavigator navigator) : ObservableObject
{
    public ObservableCollection<EpisodeRow> Episodes { get; } = [];

    [ObservableProperty]
    private bool _isEmpty;

    public void Refresh()
    {
        Episodes.Clear();
        foreach (var summary in store.List())
        {
            var title = string.IsNullOrWhiteSpace(summary.Title)
                ? Strings.Episodes_Untitled
                : summary.Title;
            var created = summary.CreatedUtc.ToLocalTime()
                .ToString("d MMMM yyyy", CultureInfo.CurrentCulture);
            var thumbnail = System.IO.Path.Combine(store.PathsFor(summary.Id).OutDir, "thumbnail.png");
            Episodes.Add(new EpisodeRow(
                summary.Id,
                title,
                created,
                summary.HasOutput,
                System.IO.File.Exists(thumbnail) ? thumbnail : null));
        }

        IsEmpty = Episodes.Count == 0;
    }

    [RelayCommand]
    private void NewEpisode()
    {
        var settings = settingsStore.Load();
        var episode = store.CreateNew(DateTime.UtcNow, settings.VoiceId);
        store.Save(episode);
        navigator.OpenEditor(episode.Id);
    }

    [RelayCommand]
    private void Open(EpisodeRow row) => navigator.OpenEditor(row.Id);

    [RelayCommand]
    private async Task DeleteAsync(EpisodeRow row)
    {
        var confirm = new Wpf.Ui.Controls.MessageBox
        {
            Title = Strings.Episodes_DeleteConfirmTitle,
            Content = Strings.Episodes_DeleteConfirmText,
            PrimaryButtonText = Strings.Common_Delete,
            PrimaryButtonAppearance = Wpf.Ui.Controls.ControlAppearance.Danger,
            CloseButtonText = Strings.Common_Cancel,
        };

        var result = await confirm.ShowDialogAsync();
        if (result == Wpf.Ui.Controls.MessageBoxResult.Primary)
        {
            store.Delete(row.Id);
            Refresh();
        }
    }
}

using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoyalNewsDesk.App.Services;
using RoyalNewsDesk.Core.Models;
using RoyalNewsDesk.Core.Storage;

namespace RoyalNewsDesk.App.ViewModels;

public partial class EditorViewModel(
    IEpisodeStore store,
    INavigator navigator,
    ISettingsStore settingsStore,
    Core.Presenters.IPresenterEngineManager presenterManager) : ObservableObject, ISavable
{
    private Episode? _episode;

    [ObservableProperty]
    private string _title = "";

    [ObservableProperty]
    private string _pasteText = "";

    [ObservableProperty]
    private bool _isPhotoreal;

    [ObservableProperty]
    private bool _photorealNotReady;

    public ObservableCollection<SegmentViewModel> Segments { get; } = [];

    public void Load(string episodeId)
    {
        _episode = store.Load(episodeId) ?? throw new InvalidOperationException("Episode not found: " + episodeId);
        Title = _episode.Title;
        IsPhotoreal = _episode.PresenterStyle == Core.Presenters.PresenterStyle.Photoreal;
        UpdatePhotorealReadiness();
        Segments.Clear();
        var imagesDir = store.PathsFor(episodeId).ImagesDir;
        foreach (var segment in _episode.Segments)
        {
            Segments.Add(new SegmentViewModel(segment, imagesDir));
        }

        Renumber();
    }

    private void Renumber()
    {
        for (var i = 0; i < Segments.Count; i++)
        {
            Segments[i].Ordinal = i + 1;
        }
    }

    public void Save()
    {
        if (_episode is null)
        {
            return;
        }

        _episode.Title = Title.Trim();
        _episode.PresenterStyle = IsPhotoreal
            ? Core.Presenters.PresenterStyle.Photoreal
            : Core.Presenters.PresenterStyle.Animated;
        _episode.Segments = Segments.Select(s => s.ToModel()).ToList();
        store.Save(_episode);
    }

    partial void OnIsPhotorealChanged(bool value) => UpdatePhotorealReadiness();

    private void UpdatePhotorealReadiness()
    {
        if (!IsPhotoreal)
        {
            PhotorealNotReady = false;
            return;
        }

        var settings = settingsStore.Load();
        PhotorealNotReady = !presenterManager.IsInstalled(settings.PhotorealEngineId)
            || settings.PhotorealPortraitPath is not { Length: > 0 } portrait
            || !File.Exists(portrait);
    }

    [RelayCommand]
    private void OpenPresenterSettings() => navigator.OpenSettings();

    [RelayCommand]
    private void AddSegment()
    {
        if (_episode is null)
        {
            return;
        }

        var next = Segments.Count + 1;
        var segment = new Segment { Id = "seg-" + next.ToString("00", System.Globalization.CultureInfo.InvariantCulture) };
        Segments.Add(new SegmentViewModel(segment, store.PathsFor(_episode.Id).ImagesDir));
        Renumber();
    }

    [RelayCommand]
    private void RemoveSegment(SegmentViewModel segment)
    {
        Segments.Remove(segment);
        Renumber();
    }

    [RelayCommand]
    private void SaveEdits() => Save();

    [RelayCommand]
    private async Task ImportAsync()
    {
        if (_episode is null || string.IsNullOrWhiteSpace(PasteText))
        {
            return;
        }

        if (Segments.Any(s => !string.IsNullOrWhiteSpace(s.Body)))
        {
            var confirm = new Wpf.Ui.Controls.MessageBox
            {
                Title = Resources.Strings.Editor_ImportConfirmTitle,
                Content = Resources.Strings.Editor_ImportConfirmText,
                PrimaryButtonText = Resources.Strings.Common_Continue,
                CloseButtonText = Resources.Strings.Common_Cancel,
            };
            if (await confirm.ShowDialogAsync() != Wpf.Ui.Controls.MessageBoxResult.Primary)
            {
                return;
            }
        }

        var imported = Core.Script.ScriptImporter.Import(PasteText);
        if (imported.Segments.Count == 0)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(imported.Title))
        {
            Title = imported.Title!;
        }

        Segments.Clear();
        var imagesDir = store.PathsFor(_episode.Id).ImagesDir;
        var index = 1;
        foreach (var segment in imported.Segments)
        {
            var model = new Segment
            {
                Id = "seg-" + index.ToString("00", System.Globalization.CultureInfo.InvariantCulture),
                Headline = segment.Headline,
                Body = segment.Body,
            };
            Segments.Add(new SegmentViewModel(model, imagesDir));
            index++;
        }

        Renumber();
        PasteText = "";
        Save();
    }

    [RelayCommand]
    private void LoadExample()
    {
        var samplePath = Path.Combine(AppContext.BaseDirectory, "assets", "sample-script.txt");
        if (File.Exists(samplePath))
        {
            PasteText = File.ReadAllText(samplePath);
        }
    }

    [RelayCommand]
    private void GoBack()
    {
        Save();
        navigator.OpenEpisodes();
    }

    [RelayCommand]
    private void Produce()
    {
        if (_episode is null)
        {
            return;
        }

        Save();
        navigator.OpenProduce(_episode.Id);
    }
}

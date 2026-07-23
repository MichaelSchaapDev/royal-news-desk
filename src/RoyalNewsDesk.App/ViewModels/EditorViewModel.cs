using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoyalNewsDesk.App.Services;
using RoyalNewsDesk.Core.Models;
using RoyalNewsDesk.Core.Storage;

namespace RoyalNewsDesk.App.ViewModels;

public partial class EditorViewModel(IEpisodeStore store, INavigator navigator) : ObservableObject, ISavable
{
    private Episode? _episode;

    [ObservableProperty]
    private string _title = "";

    [ObservableProperty]
    private string _pasteText = "";

    public ObservableCollection<SegmentViewModel> Segments { get; } = [];

    public bool CanProduce => false; // Wired up when the pipeline lands.

    public void Load(string episodeId)
    {
        _episode = store.Load(episodeId) ?? throw new InvalidOperationException("Episode not found: " + episodeId);
        Title = _episode.Title;
        Segments.Clear();
        var imagesDir = store.PathsFor(episodeId).ImagesDir;
        foreach (var segment in _episode.Segments)
        {
            Segments.Add(new SegmentViewModel(segment, imagesDir));
        }
    }

    public void Save()
    {
        if (_episode is null)
        {
            return;
        }

        _episode.Title = Title.Trim();
        _episode.Segments = Segments.Select(s => s.ToModel()).ToList();
        store.Save(_episode);
    }

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
    }

    [RelayCommand]
    private void RemoveSegment(SegmentViewModel segment) => Segments.Remove(segment);

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

    [RelayCommand(CanExecute = nameof(CanProduce))]
    private void Produce()
    {
        // Arrives with the pipeline milestone.
    }
}

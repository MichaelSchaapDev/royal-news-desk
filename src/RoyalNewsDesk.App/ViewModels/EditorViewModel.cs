using System.Collections.ObjectModel;
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

    public bool CanImport => false; // The script parser lands in a later milestone.

    [RelayCommand(CanExecute = nameof(CanImport))]
    private void Import()
    {
        // Arrives with the script parser milestone.
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

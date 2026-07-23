using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoyalNewsDesk.App.Resources;
using RoyalNewsDesk.App.Services;
using RoyalNewsDesk.Core.Models;
using RoyalNewsDesk.Core.Pipeline;
using RoyalNewsDesk.Core.Storage;

namespace RoyalNewsDesk.App.ViewModels;

public partial class StepRowViewModel(PipelineStepId step) : ObservableObject
{
    public PipelineStepId Step { get; } = step;

    [ObservableProperty]
    private StepState _state = StepState.Pending;

    [ObservableProperty]
    private string _text = StepProgressLocalizer.StepText(new StepProgress(step, StepState.Pending));

    [ObservableProperty]
    private double _fraction;

    [ObservableProperty]
    private bool _hasFraction;

    public bool IsPending => State == StepState.Pending;

    public bool IsRunning => State == StepState.Running;

    public bool IsSucceeded => State == StepState.Succeeded;

    public bool IsFailed => State is StepState.Failed or StepState.Canceled;

    partial void OnStateChanged(StepState value)
    {
        OnPropertyChanged(nameof(IsPending));
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(IsSucceeded));
        OnPropertyChanged(nameof(IsFailed));
    }
}

public partial class ProduceViewModel(
    EpisodePipeline pipeline,
    IEpisodeStore store,
    ISettingsStore settingsStore,
    INavigator navigator) : ObservableObject, ILeavable
{
    private string _episodeId = "";
    private CancellationTokenSource? _cts;

    public ObservableCollection<StepRowViewModel> Steps { get; } = [];

    public ObservableCollection<string> Warnings { get; } = [];

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private bool _isDone;

    [ObservableProperty]
    private bool _isFailed;

    [ObservableProperty]
    private string _failText = "";

    [ObservableProperty]
    private string _durationText = "";

    [ObservableProperty]
    private Uri? _videoUri;

    private string _videoPath = "";

    public void Start(string episodeId)
    {
        _episodeId = episodeId;
        _ = RunAsync();
    }

    private async Task RunAsync()
    {
        VideoUri = null;
        IsDone = false;
        IsFailed = false;
        Warnings.Clear();
        Steps.Clear();
        foreach (var step in Enum.GetValues<PipelineStepId>())
        {
            Steps.Add(new StepRowViewModel(step));
        }

        var episode = store.Load(_episodeId);
        if (episode is null)
        {
            navigator.OpenEpisodes();
            return;
        }

        var settings = settingsStore.Load();
        var options = ProduceOptions.From(settings);
        _cts = new CancellationTokenSource();
        IsRunning = true;

        var progress = new Progress<StepProgress>(OnStepProgress);
        try
        {
            var result = await Task.Run(
                () => pipeline.ProduceAsync(episode, settings, options, progress, _cts.Token));

            foreach (var warning in result.Warnings)
            {
                Warnings.Add(StepProgressLocalizer.WarningText(warning));
            }

            _videoPath = result.VideoPath;
            DurationText = string.Format(
                CultureInfo.CurrentCulture,
                Strings.Produce_DurationFormat,
                result.Duration.ToString(@"mm\:ss", CultureInfo.CurrentCulture));
            VideoUri = new Uri(result.VideoPath);
            IsDone = true;
        }
        catch (OperationCanceledException)
        {
            navigator.OpenEditor(_episodeId);
        }
        catch (PipelineException ex)
        {
            FailText = StepProgressLocalizer.ErrorText(ex.Code);
            IsFailed = true;
        }
        catch (Exception ex)
        {
            FailText = StepProgressLocalizer.ErrorText(PipelineErrorCode.Unknown) + " (" + ex.Message + ")";
            IsFailed = true;
        }
        finally
        {
            IsRunning = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void OnStepProgress(StepProgress progress)
    {
        var row = Steps.FirstOrDefault(s => s.Step == progress.Step);
        if (row is null)
        {
            return;
        }

        row.State = progress.State;
        row.Text = StepProgressLocalizer.StepText(progress);
        if (progress.Fraction is { } fraction)
        {
            row.Fraction = fraction;
            row.HasFraction = progress.State == StepState.Running;
        }
        else if (progress.State != StepState.Running)
        {
            row.HasFraction = false;
        }

        if (progress.State == StepState.Failed && progress.TechnicalDetail is { } detail)
        {
            Warnings.Add(detail);
        }
    }

    [RelayCommand]
    private void Cancel() => _cts?.Cancel();

    [RelayCommand]
    private void OpenFolder()
    {
        if (_videoPath.Length > 0 && File.Exists(_videoPath))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + _videoPath + "\"")
            {
                UseShellExecute = true,
            });
        }
    }

    [RelayCommand]
    private void Again()
    {
        if (!IsRunning)
        {
            _ = RunAsync();
        }
    }

    [RelayCommand]
    private void GoBack()
    {
        _cts?.Cancel();
        navigator.OpenEditor(_episodeId);
    }

    public void OnLeaving() => _cts?.Cancel();
}

using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoyalNewsDesk.App.Resources;
using RoyalNewsDesk.App.Services;
using RoyalNewsDesk.Core.Storage;
using RoyalNewsDesk.Core.Tools;
using RoyalNewsDesk.Core.VoiceModels;

namespace RoyalNewsDesk.App.ViewModels;

public partial class FirstRunViewModel(
    IVoiceModelManager voiceManager,
    ISettingsStore settingsStore,
    INavigator navigator,
    ToolHealthCheck toolHealthCheck) : ObservableObject
{
    private string _voiceId = "";

    [ObservableProperty]
    private string _voiceName = "";

    [ObservableProperty]
    private string _voiceSizeText = "";

    [ObservableProperty]
    private bool _isIdle = true;

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private bool _isDone;

    [ObservableProperty]
    private bool _isFailed;

    [ObservableProperty]
    private double _progressFraction;

    [ObservableProperty]
    private string _progressText = "";

    [ObservableProperty]
    private string _errorDetail = "";

    [ObservableProperty]
    private bool _hasToolProblems;

    public void Initialize()
    {
        var settings = settingsStore.Load();
        _voiceId = settings.VoiceId;
        var voice = voiceManager.Voices.FirstOrDefault(v => v.Id == _voiceId)
            ?? voiceManager.Voices[0];
        _voiceId = voice.Id;
        VoiceName = voice.DisplayName;
        VoiceSizeText = string.Format(
            CultureInfo.CurrentCulture,
            Strings.FirstRun_SizeFormat,
            (voice.TotalBytes / 1_000_000).ToString(CultureInfo.CurrentCulture));

        _ = CheckToolsAsync();
    }

    private async Task CheckToolsAsync()
    {
        var results = await Task.Run(() => toolHealthCheck.CheckAllAsync(CancellationToken.None));
        HasToolProblems = results.Any(r => !r.Ok);
    }

    [RelayCommand]
    private async Task DownloadAsync()
    {
        IsIdle = false;
        IsFailed = false;
        IsDownloading = true;
        var progress = new Progress<DownloadProgress>(p =>
        {
            ProgressFraction = p.Fraction;
            ProgressText = string.Format(
                CultureInfo.CurrentCulture,
                Strings.FirstRun_DownloadingFormat,
                (p.BytesReceived / 1_000_000).ToString(CultureInfo.CurrentCulture),
                (p.TotalBytes / 1_000_000).ToString(CultureInfo.CurrentCulture));
        });

        try
        {
            await Task.Run(() => voiceManager.DownloadAsync(_voiceId, progress, CancellationToken.None));
            IsDownloading = false;
            IsDone = true;
        }
        catch (VoiceDownloadException ex)
        {
            IsDownloading = false;
            IsFailed = true;
            ErrorDetail = ex.Message;
        }
    }

    [RelayCommand]
    private void Continue() => navigator.OpenEpisodes();
}

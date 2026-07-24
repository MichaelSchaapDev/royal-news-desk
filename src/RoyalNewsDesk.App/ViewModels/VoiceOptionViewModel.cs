using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoyalNewsDesk.App.Resources;
using RoyalNewsDesk.Core.VoiceModels;

namespace RoyalNewsDesk.App.ViewModels;

public partial class VoiceOptionViewModel : ObservableObject
{
    private readonly VoiceInfo _voice;
    private readonly IVoiceModelManager _manager;
    private readonly Action<string> _onSelected;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isInstalled;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private double _progressFraction;

    public VoiceOptionViewModel(VoiceInfo voice, IVoiceModelManager manager, bool isSelected, Action<string> onSelected)
    {
        _voice = voice;
        _manager = manager;
        _onSelected = onSelected;
        _isSelected = isSelected;
        _isInstalled = manager.IsInstalled(voice.Id);
    }

    public string Id => _voice.Id;

    public string DisplayName => _voice.DisplayName;

    public string SizeText => (_voice.TotalBytes / 1_000_000).ToString(CultureInfo.CurrentCulture) + " MB";

    public string StatusText => IsInstalled ? Strings.Settings_VoiceInstalled : Strings.Settings_VoiceNotInstalled;

    public bool CanDownload => !IsInstalled && !IsBusy;

    public bool CanDelete => IsInstalled && !IsBusy;

    /// <summary>Re-reads the installed state, e.g. after the storage folder moved.</summary>
    public void RefreshInstalled() => IsInstalled = _manager.IsInstalled(Id);

    partial void OnIsSelectedChanged(bool value)
    {
        if (value)
        {
            _onSelected(Id);
        }
    }

    partial void OnIsInstalledChanged(bool value)
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(CanDownload));
        OnPropertyChanged(nameof(CanDelete));
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanDownload));
        OnPropertyChanged(nameof(CanDelete));
    }

    [RelayCommand]
    private async Task DownloadAsync()
    {
        IsBusy = true;
        var progress = new Progress<DownloadProgress>(p => ProgressFraction = p.Fraction);
        try
        {
            await Task.Run(() => _manager.DownloadAsync(Id, progress, CancellationToken.None));
        }
        catch (VoiceDownloadException)
        {
            // The row simply stays "not downloaded"; first-run has the detailed flow.
        }
        finally
        {
            IsBusy = false;
            IsInstalled = _manager.IsInstalled(Id);
        }
    }

    [RelayCommand]
    private void Delete()
    {
        _manager.Delete(Id);
        IsInstalled = false;
    }
}

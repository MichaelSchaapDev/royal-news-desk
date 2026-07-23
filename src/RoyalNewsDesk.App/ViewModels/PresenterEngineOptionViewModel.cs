using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoyalNewsDesk.App.Resources;
using RoyalNewsDesk.Core.Presenters;

namespace RoyalNewsDesk.App.ViewModels;

public partial class PresenterEngineOptionViewModel : ObservableObject
{
    private readonly PresenterEngineInfo _engine;
    private readonly IPresenterEngineManager _manager;
    private readonly Action<string> _onSelected;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isInstalled;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private double _progressFraction;

    [ObservableProperty]
    private string _busyText = "";

    public PresenterEngineOptionViewModel(
        PresenterEngineInfo engine,
        IPresenterEngineManager manager,
        bool isSelected,
        Action<string> onSelected)
    {
        _engine = engine;
        _manager = manager;
        _onSelected = onSelected;
        _isSelected = isSelected;
        _isInstalled = manager.IsInstalled(engine.Id);
    }

    public string Id => _engine.Id;

    public string DisplayName => _engine.DisplayName;

    public string SizeText => (_engine.TotalBytes / 1_000_000_000.0).ToString("0.0", CultureInfo.CurrentCulture) + " GB";

    public string StatusText => IsInstalled ? Strings.Settings_VoiceInstalled : Strings.Settings_VoiceNotInstalled;

    public bool NeedsGpu => _engine.RequiresNvidiaGpu;

    public bool CanDownload => !IsInstalled && !IsBusy;

    public bool CanDelete => IsInstalled && !IsBusy;

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
        var progress = new Progress<PresenterInstallProgress>(p =>
        {
            ProgressFraction = p.Fraction;
            BusyText = p.Phase == PresenterInstallPhase.Extracting ? Strings.Settings_EngineExtracting : "";
        });
        try
        {
            await Task.Run(() => _manager.DownloadAsync(Id, progress, CancellationToken.None));
        }
        catch (PresenterInstallException)
        {
            // The row simply stays "not downloaded".
        }
        finally
        {
            IsBusy = false;
            BusyText = "";
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

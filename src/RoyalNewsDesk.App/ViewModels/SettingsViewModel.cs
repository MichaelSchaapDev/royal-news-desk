using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using RoyalNewsDesk.App.Services;
using RoyalNewsDesk.Core.Models;
using RoyalNewsDesk.Core.Storage;
using Wpf.Ui.Appearance;

namespace RoyalNewsDesk.App.ViewModels;

public partial class SettingsViewModel : ObservableObject, ISavable
{
    private static readonly Regex HexColor = new("^#[0-9a-fA-F]{6}$", RegexOptions.Compiled);

    private readonly ISettingsStore _store;
    private readonly AppSettings _settings;
    private readonly string _initialLanguage;

    [ObservableProperty]
    private string _language;

    [ObservableProperty]
    private bool _isDarkTheme;

    [ObservableProperty]
    private double _readingSpeed;

    [ObservableProperty]
    private string _channelName;

    [ObservableProperty]
    private string _tagline;

    [ObservableProperty]
    private string _primaryColor;

    [ObservableProperty]
    private string _accentColor;

    [ObservableProperty]
    private string _outputFolder;

    [ObservableProperty]
    private bool _keepWorkFiles;

    [ObservableProperty]
    private bool _burnInSubtitles;

    [ObservableProperty]
    private bool _studioAmbience;

    [ObservableProperty]
    private bool _higherQuality;

    [ObservableProperty]
    private bool _showRestartHint;

    private readonly AppPaths _paths;

    public SettingsViewModel(
        ISettingsStore store,
        Core.VoiceModels.IVoiceModelManager voiceManager,
        Core.Presenters.IPresenterEngineManager presenterManager,
        AppPaths paths)
    {
        _store = store;
        _paths = paths;
        _settings = store.Load();
        _initialLanguage = _settings.Language;
        _aiStorageText = paths.AiRoot;

        Voices = voiceManager.Voices
            .Select(v => new VoiceOptionViewModel(
                v,
                voiceManager,
                isSelected: v.Id == _settings.VoiceId,
                onSelected: id =>
                {
                    _settings.VoiceId = id;
                    Save();
                }))
            .ToList();

        PresenterEngines = presenterManager.Engines
            .Select(e => new PresenterEngineOptionViewModel(
                e,
                presenterManager,
                isSelected: e.Id == _settings.PhotorealEngineId,
                onSelected: id =>
                {
                    _settings.PhotorealEngineId = id;
                    Save();
                }))
            .ToList();
        _portraitPath = _settings.PhotorealPortraitPath;

        _language = _settings.Language;
        _isDarkTheme = _settings.Theme == AppTheme.Dark;
        _readingSpeed = _settings.ReadingSpeed;
        _channelName = _settings.Branding.ChannelName;
        _tagline = _settings.Branding.Tagline;
        _primaryColor = _settings.Branding.PrimaryColor;
        _accentColor = _settings.Branding.AccentColor;
        _outputFolder = _settings.OutputFolder;
        _keepWorkFiles = _settings.KeepWorkFiles;
        _burnInSubtitles = _settings.BurnInSubtitles;
        _studioAmbience = _settings.StudioAmbience;
        _higherQuality = _settings.HigherQuality;
    }

    public IReadOnlyList<VoiceOptionViewModel> Voices { get; }

    public IReadOnlyList<PresenterEngineOptionViewModel> PresenterEngines { get; }

    [ObservableProperty]
    private string? _portraitPath;

    public bool HasPortrait => !string.IsNullOrWhiteSpace(PortraitPath) && File.Exists(PortraitPath);

    partial void OnPortraitPathChanged(string? value) => OnPropertyChanged(nameof(HasPortrait));

    [RelayCommand]
    private void BrowsePortrait()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Images|*.png;*.jpg;*.jpeg",
            CheckFileExists = true,
        };
        if (dialog.ShowDialog() == true)
        {
            PortraitPath = dialog.FileName;
            Save();
        }
    }

    [RelayCommand]
    private void RemovePortrait()
    {
        PortraitPath = null;
        Save();
    }

    public void Save()
    {
        _settings.Language = Language;
        _settings.Theme = IsDarkTheme ? AppTheme.Dark : AppTheme.Light;
        _settings.ReadingSpeed = Math.Clamp(ReadingSpeed, 0.8, 1.3);
        _settings.Branding.ChannelName = string.IsNullOrWhiteSpace(ChannelName) ? "Royal News Desk" : ChannelName.Trim();
        _settings.Branding.Tagline = Tagline.Trim();
        if (HexColor.IsMatch(PrimaryColor.Trim()))
        {
            _settings.Branding.PrimaryColor = PrimaryColor.Trim();
        }

        if (HexColor.IsMatch(AccentColor.Trim()))
        {
            _settings.Branding.AccentColor = AccentColor.Trim();
        }

        if (!string.IsNullOrWhiteSpace(OutputFolder))
        {
            _settings.OutputFolder = OutputFolder.Trim();
        }

        _settings.KeepWorkFiles = KeepWorkFiles;
        _settings.BurnInSubtitles = BurnInSubtitles;
        _settings.StudioAmbience = StudioAmbience;
        _settings.HigherQuality = HigherQuality;
        _settings.PhotorealPortraitPath = string.IsNullOrWhiteSpace(PortraitPath) ? null : PortraitPath;
        _store.Save(_settings);
    }

    partial void OnIsDarkThemeChanged(bool value)
    {
        App.ApplyTheme(value);
        Save();
    }

    partial void OnLanguageChanged(string value)
    {
        ShowRestartHint = value != _initialLanguage;
        Save();
    }

    [RelayCommand]
    private void BrowseOutputFolder()
    {
        var dialog = new OpenFolderDialog();
        if (dialog.ShowDialog() == true)
        {
            OutputFolder = dialog.FolderName;
            Save();
        }
    }

    [ObservableProperty]
    private string _aiStorageText;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanChangeAiStorage))]
    private bool _isMovingAi;

    [ObservableProperty]
    private double _aiMoveFraction;

    [ObservableProperty]
    private string? _aiStorageError;

    public bool CanChangeAiStorage => !IsMovingAi;

    public bool AiStorageIsCustom => !string.IsNullOrWhiteSpace(_settings.AiStorageFolder);

    [RelayCommand]
    private async Task BrowseAiStorageAsync()
    {
        var dialog = new OpenFolderDialog();
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        await ApplyAiStorageAsync(dialog.FolderName).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ResetAiStorageAsync()
    {
        if (AiStorageIsCustom)
        {
            await ApplyAiStorageAsync(_paths.DataRoot).ConfigureAwait(true);
        }
    }

    private async Task ApplyAiStorageAsync(string chosen)
    {
        AiStorageError = null;
        var target = Path.GetFullPath(chosen);
        var current = Path.GetFullPath(_paths.AiRoot);
        if (string.Equals(target, current, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (AiStorageMover.IsSameOrNested(current, target))
        {
            AiStorageError = Resources.Strings.Settings_StorageNested;
            return;
        }

        var bytes = AiStorageMover.MeasureBytes(current);
        if (bytes > 0)
        {
            if (AiStorageMover.FreeBytesAt(target) < bytes + 500_000_000L)
            {
                AiStorageError = Resources.Strings.Settings_StorageNoSpace;
                return;
            }

            var gb = (bytes / 1_000_000_000.0).ToString("0.0", System.Globalization.CultureInfo.CurrentCulture);
            var confirm = new Wpf.Ui.Controls.MessageBox
            {
                Title = Resources.Strings.Settings_StorageMoveTitle,
                Content = string.Format(
                    System.Globalization.CultureInfo.CurrentCulture,
                    Resources.Strings.Settings_StorageMoveText,
                    gb),
                PrimaryButtonText = Resources.Strings.Common_Continue,
                CloseButtonText = Resources.Strings.Common_Cancel,
            };
            if (await confirm.ShowDialogAsync() != Wpf.Ui.Controls.MessageBoxResult.Primary)
            {
                return;
            }

            IsMovingAi = true;
            AiMoveFraction = 0;
            var progress = new Progress<double>(f => AiMoveFraction = f);
            try
            {
                await Task.Run(() => AiStorageMover.MoveAsync(current, target, progress, CancellationToken.None))
                    .ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                AiStorageError = string.Format(
                    System.Globalization.CultureInfo.CurrentCulture,
                    Resources.Strings.Settings_StorageMoveFailed,
                    ex.Message);
                return;
            }
            finally
            {
                IsMovingAi = false;
            }
        }

        var isDefault = string.Equals(target, Path.GetFullPath(_paths.DataRoot), StringComparison.OrdinalIgnoreCase);
        _settings.AiStorageFolder = isDefault ? null : target;
        _paths.AiRootOverride = _settings.AiStorageFolder;
        _paths.EnsureCreated();

        // A portrait chosen from inside the AI folder moved along with it.
        if (PortraitPath is { Length: > 0 } portrait)
        {
            var old = Path.TrimEndingDirectorySeparator(current);
            var full = Path.GetFullPath(portrait);
            if (full.StartsWith(old + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                PortraitPath = Path.Combine(target, Path.GetRelativePath(old, full));
            }
        }

        Save();
        AiStorageText = _paths.AiRoot;
        OnPropertyChanged(nameof(AiStorageIsCustom));
        foreach (var voice in Voices)
        {
            voice.RefreshInstalled();
        }

        foreach (var engine in PresenterEngines)
        {
            engine.RefreshInstalled();
        }
    }

    [RelayCommand]
    private void RestartNow()
    {
        Save();
        if (Environment.ProcessPath is { } exe)
        {
            Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
        }

        Application.Current.Shutdown();
    }
}

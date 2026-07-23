using System.Diagnostics;
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

    public SettingsViewModel(ISettingsStore store)
    {
        _store = store;
        _settings = store.Load();
        _initialLanguage = _settings.Language;

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
        _store.Save(_settings);
    }

    partial void OnIsDarkThemeChanged(bool value)
    {
        ApplicationThemeManager.Apply(value ? ApplicationTheme.Dark : ApplicationTheme.Light);
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

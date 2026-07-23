using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using RoyalNewsDesk.App.Resources;
using RoyalNewsDesk.App.Services;
using Wpf.Ui.Controls;

namespace RoyalNewsDesk.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject, INavigator
{
    public const string EpisodesKey = "episodes";
    public const string SettingsKey = "settings";
    public const string AboutKey = "about";

    private readonly IServiceProvider _services;
    private bool _syncingSelection;

    [ObservableProperty]
    private object? _currentPage;

    [ObservableProperty]
    private NavItem? _selectedNavItem;

    public MainWindowViewModel(IServiceProvider services)
    {
        _services = services;
        NavItems =
        [
            new NavItem(EpisodesKey, Strings.Nav_Episodes, SymbolRegular.Home24),
            new NavItem(SettingsKey, Strings.Nav_Settings, SymbolRegular.Settings24),
            new NavItem(AboutKey, Strings.Nav_About, SymbolRegular.Info24),
        ];
    }

    public IReadOnlyList<NavItem> NavItems { get; }

    public void OpenEpisodes()
    {
        var vm = _services.GetRequiredService<EpisodesViewModel>();
        vm.Refresh();
        ShowPage(vm, EpisodesKey);
    }

    public void OpenSettings()
    {
        ShowPage(_services.GetRequiredService<SettingsViewModel>(), SettingsKey);
    }

    public void OpenAbout()
    {
        ShowPage(_services.GetRequiredService<AboutViewModel>(), AboutKey);
    }

    public void OpenEditor(string episodeId)
    {
        var vm = _services.GetRequiredService<EditorViewModel>();
        vm.Load(episodeId);
        ShowPage(vm, navKey: null);
    }

    partial void OnSelectedNavItemChanged(NavItem? value)
    {
        if (_syncingSelection || value is null)
        {
            return;
        }

        switch (value.Key)
        {
            case EpisodesKey:
                OpenEpisodes();
                break;
            case SettingsKey:
                OpenSettings();
                break;
            case AboutKey:
                OpenAbout();
                break;
        }
    }

    private void ShowPage(object page, string? navKey)
    {
        if (CurrentPage is ISavable savable)
        {
            savable.Save();
        }

        CurrentPage = page;

        _syncingSelection = true;
        try
        {
            SelectedNavItem = navKey is null ? null : NavItems.FirstOrDefault(n => n.Key == navKey);
        }
        finally
        {
            _syncingSelection = false;
        }
    }
}

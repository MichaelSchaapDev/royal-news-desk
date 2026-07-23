using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RoyalNewsDesk.App.Services;
using RoyalNewsDesk.App.ViewModels;
using RoyalNewsDesk.Core.Models;
using RoyalNewsDesk.Core.Processes;
using RoyalNewsDesk.Core.Storage;
using RoyalNewsDesk.Core.Tools;
using RoyalNewsDesk.Core.VoiceModels;
using Serilog;
using System.Net.Http;
using Wpf.Ui.Appearance;
using AppStrings = RoyalNewsDesk.App.Resources.Strings;

namespace RoyalNewsDesk.App;

public partial class App : Application
{
    private ServiceProvider? _services;
    private AppPaths? _paths;
    private Microsoft.Extensions.Logging.ILogger? _log;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _paths = AppPaths.CreateDefault();
        _paths.EnsureCreated();

        var serilog = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                Path.Combine(_paths.LogsRoot, "app-.log"),
                formatProvider: CultureInfo.InvariantCulture,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .CreateLogger();

        var settingsStore = new JsonSettingsStore(_paths);
        var settings = settingsStore.Load();

        var uiCulture = new CultureInfo(settings.Language == "en" ? "en" : "nl");
        CultureInfo.DefaultThreadCurrentUICulture = uiCulture;
        Thread.CurrentThread.CurrentUICulture = uiCulture;

        ApplicationThemeManager.Apply(
            settings.Theme == AppTheme.Dark ? ApplicationTheme.Dark : ApplicationTheme.Light);

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddSerilog(serilog, dispose: true));
        services.AddSingleton(_paths);
        services.AddSingleton<ISettingsStore>(settingsStore);
        services.AddSingleton<IEpisodeStore, JsonEpisodeStore>();
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton<IToolLocator, InstalledToolLocator>();
        services.AddSingleton<ToolHealthCheck>();
        services.AddSingleton(VoiceCatalog.LoadEmbedded());
        services.AddSingleton(new HttpClient { Timeout = TimeSpan.FromMinutes(30) });
        services.AddSingleton<IVoiceModelManager, VoiceModelManager>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<INavigator>(sp => sp.GetRequiredService<MainWindowViewModel>());
        services.AddTransient<EpisodesViewModel>();
        services.AddTransient<EditorViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<AboutViewModel>();
        services.AddTransient<FirstRunViewModel>();
        _services = services.BuildServiceProvider();

        _log = _services.GetRequiredService<ILoggerFactory>().CreateLogger("App");
        _log.LogInformation("Starting Royal News Desk Studio, language {Language}", settings.Language);

        SetupExceptionHandling();

        var mainVm = _services.GetRequiredService<MainWindowViewModel>();
        var window = new MainWindow(mainVm);
        MainWindow = window;
        window.Show();

        var voiceManager = _services.GetRequiredService<IVoiceModelManager>();
        if (voiceManager.IsInstalled(settings.VoiceId))
        {
            mainVm.OpenEpisodes();
        }
        else
        {
            mainVm.OpenFirstRun();
        }

        MaybeRunScreenshotMode(window, mainVm);
    }

    /// <summary>
    /// Dev helper: --screenshot &lt;path.png&gt; renders the window with WPF's own
    /// compositor and exits. Used for automated UI checks and doc screenshots,
    /// where OS-level capture is unreliable on hybrid-GPU machines.
    /// </summary>
    private void MaybeRunScreenshotMode(System.Windows.Window window, MainWindowViewModel mainVm)
    {
        var args = Environment.GetCommandLineArgs();
        var index = Array.IndexOf(args, "--screenshot");
        if (index < 0 || index + 1 >= args.Length)
        {
            return;
        }

        var targetPath = args[index + 1];
        var pageIndex = Array.IndexOf(args, "--page");
        var page = pageIndex >= 0 && pageIndex + 1 < args.Length ? args[pageIndex + 1] : "episodes";
        window.Dispatcher.InvokeAsync(async () =>
        {
            switch (page)
            {
                case "settings":
                    mainVm.OpenSettings();
                    break;
                case "about":
                    mainVm.OpenAbout();
                    break;
            }

            await Task.Delay(1500);
            var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(window);
            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(
                (int)(window.ActualWidth * dpi.DpiScaleX),
                (int)(window.ActualHeight * dpi.DpiScaleY),
                dpi.PixelsPerInchX,
                dpi.PixelsPerInchY,
                System.Windows.Media.PixelFormats.Pbgra32);
            bitmap.Render(window);
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            using (var stream = File.Create(targetPath))
            {
                encoder.Save(stream);
            }

            Shutdown();
        });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }

    private void SetupExceptionHandling()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            _log?.LogError(args.Exception, "Unobserved task exception");
            args.SetObserved();
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            _log?.LogCritical(args.ExceptionObject as Exception, "Unhandled domain exception");
        };
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _log?.LogError(e.Exception, "Unhandled UI exception");
        var body = string.Format(
            CultureInfo.CurrentCulture,
            AppStrings.Error_Body,
            _paths?.LogsRoot ?? "");
        MessageBox.Show(body, AppStrings.Error_Title, MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}

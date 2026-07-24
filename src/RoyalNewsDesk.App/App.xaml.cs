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

        _paths.AiRootOverride = settings.AiStorageFolder;
        _paths.EnsureCreated();

        // Dev overrides for --screenshot runs; never persisted.
        var language = ReadArg("--lang") ?? settings.Language;
        var darkTheme = ReadArg("--theme") is { } t
            ? t == "dark"
            : settings.Theme == AppTheme.Dark;

        var uiCulture = new CultureInfo(language == "en" ? "en" : "nl");
        CultureInfo.DefaultThreadCurrentUICulture = uiCulture;
        Thread.CurrentThread.CurrentUICulture = uiCulture;

        var fontsDir = Path.Combine(AppContext.BaseDirectory, "assets", "fonts");
        var fontsUri = new Uri(fontsDir + Path.DirectorySeparatorChar);
        Resources["DisplayFontFamily"] = new System.Windows.Media.FontFamily(fontsUri, "./#IBM Plex Serif, Georgia, serif");
        Resources["SansFontFamily"] = new System.Windows.Media.FontFamily(fontsUri, "./#IBM Plex Sans, Segoe UI, sans-serif");

        ApplyTheme(darkTheme);

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
        services.AddSingleton<RoyalNewsDesk.Core.Tts.ITtsEngine, RoyalNewsDesk.Core.Tts.PiperTtsEngine>();
        services.AddSingleton<RoyalNewsDesk.Core.LipSync.ILipSyncEngine, RoyalNewsDesk.Core.LipSync.RhubarbLipSyncEngine>();
        services.AddSingleton(RoyalNewsDesk.Core.Presenters.PresenterCatalog.LoadEmbedded());
        services.AddSingleton<RoyalNewsDesk.Core.Presenters.IPresenterEngineManager,
            RoyalNewsDesk.Core.Presenters.PresenterEngineManager>();
        services.AddSingleton(sp => new RoyalNewsDesk.Core.Pipeline.EpisodePipeline(
            sp.GetRequiredService<IProcessRunner>(),
            sp.GetRequiredService<IToolLocator>(),
            sp.GetRequiredService<IVoiceModelManager>(),
            sp.GetRequiredService<RoyalNewsDesk.Core.Presenters.IPresenterEngineManager>(),
            sp.GetRequiredService<IEpisodeStore>(),
            sp.GetRequiredService<RoyalNewsDesk.Core.Tts.ITtsEngine>(),
            sp.GetRequiredService<RoyalNewsDesk.Core.LipSync.ILipSyncEngine>(),
            new RoyalNewsDesk.Core.Presenters.AnimatedPresenterEngine(
                Path.Combine(AppContext.BaseDirectory, "assets")),
            new RoyalNewsDesk.Core.Presenters.SadTalkerPresenterEngine(
                sp.GetRequiredService<IProcessRunner>(),
                sp.GetRequiredService<IToolLocator>(),
                sp.GetRequiredService<RoyalNewsDesk.Core.Presenters.IPresenterEngineManager>()),
            Path.Combine(AppContext.BaseDirectory, "assets")));
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<INavigator>(sp => sp.GetRequiredService<MainWindowViewModel>());
        services.AddTransient<EpisodesViewModel>();
        services.AddTransient<EditorViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<AboutViewModel>();
        services.AddTransient<FirstRunViewModel>();
        services.AddTransient<ProduceViewModel>();
        services.AddSingleton<UpdateService>();
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

        var updates = _services.GetRequiredService<UpdateService>();
        _ = updates.CheckAndStageAsync(() =>
            window.Dispatcher.InvokeAsync(() => mainVm.UpdateReady = true));
    }

    private static string? ReadArg(string name)
    {
        var args = Environment.GetCommandLineArgs();
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    /// <summary>
    /// Applies the Fluent theme plus the pieces it does not know about:
    /// the royal accent color and the theme-tuned brand brushes.
    /// Also called when the theme toggle in Settings flips.
    /// </summary>
    public static void ApplyTheme(bool dark)
    {
        var theme = dark ? ApplicationTheme.Dark : ApplicationTheme.Light;
        ApplicationThemeManager.Apply(theme);
        // The automatic dark palette drains 30-65 points of saturation from
        // the base color, which turns royal blue into gray lavender. Hand the
        // dark theme an explicit ramp instead; light derives fine on its own.
        if (dark)
        {
            ApplicationAccentColorManager.Apply(
                System.Windows.Media.Color.FromRgb(0x4E, 0x67, 0xBD),
                System.Windows.Media.Color.FromRgb(0x7C, 0x93, 0xE0),
                System.Windows.Media.Color.FromRgb(0x54, 0x70, 0xC6),
                System.Windows.Media.Color.FromRgb(0x43, 0x5C, 0xA8));
        }
        else
        {
            ApplicationAccentColorManager.Apply(
                System.Windows.Media.Color.FromRgb(0x24, 0x40, 0x7C),
                theme);
        }

        var dictionaries = Current.Resources.MergedDictionaries;
        var brandUri = new Uri(
            dark ? "Resources/BrandDark.xaml" : "Resources/BrandLight.xaml",
            UriKind.Relative);
        var existing = dictionaries.FirstOrDefault(d =>
            d.Source is { OriginalString: var s } && s.Contains("Brand", StringComparison.Ordinal));
        if (existing is not null)
        {
            dictionaries.Remove(existing);
        }

        dictionaries.Add(new ResourceDictionary { Source = brandUri });
    }

    /// <summary>
    /// Dev helper: --screenshot &lt;path.png&gt; renders the window with WPF's own
    /// compositor and exits. Used for automated UI checks and doc screenshots,
    /// where OS-level capture is unreliable on hybrid-GPU machines.
    /// Combine with --page, --theme light|dark and --lang en|nl.
    /// </summary>
    private void MaybeRunScreenshotMode(System.Windows.Window window, MainWindowViewModel mainVm)
    {
        var targetPath = ReadArg("--screenshot");
        if (targetPath is null)
        {
            return;
        }

        var page = ReadArg("--page") ?? "episodes";
        var delay = int.TryParse(ReadArg("--shot-delay"), out var ms) ? ms : 1500;
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
                case "firstrun":
                    mainVm.OpenFirstRun();
                    break;
                case "editor":
                case "produce":
                    var first = _services!.GetRequiredService<IEpisodeStore>().List().FirstOrDefault();
                    if (first is not null)
                    {
                        if (page == "produce")
                        {
                            mainVm.OpenProduce(first.Id);
                        }
                        else
                        {
                            mainVm.OpenEditor(first.Id);
                        }
                    }

                    break;
            }

            await Task.Delay(delay);
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

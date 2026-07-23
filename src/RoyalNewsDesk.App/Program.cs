using Velopack;

namespace RoyalNewsDesk.App;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Velopack must run before anything else: it handles install, update,
        // and uninstall hooks, and may exit the process.
        VelopackApp.Build().Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}

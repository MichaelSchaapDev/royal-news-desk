using RoyalNewsDesk.App.ViewModels;

namespace RoyalNewsDesk.App;

public partial class MainWindow
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}

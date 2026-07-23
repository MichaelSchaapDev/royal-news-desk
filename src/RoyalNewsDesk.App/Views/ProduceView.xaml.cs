using System.Windows;
using System.Windows.Controls;

namespace RoyalNewsDesk.App.Views;

public partial class ProduceView : UserControl
{
    public ProduceView()
    {
        InitializeComponent();
    }

    private void OnPlayClick(object sender, RoutedEventArgs e) => Player.Play();

    private void OnPauseClick(object sender, RoutedEventArgs e) => Player.Pause();
}

using Avalonia.Controls;
using ViperKit.UI.ViewModels;

namespace ViperKit.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }
}

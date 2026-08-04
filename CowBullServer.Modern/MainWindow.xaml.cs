using System.Windows;
using CowBullServer.Modern.ViewModels;

namespace CowBullServer.Modern;

public partial class MainWindow : Window
{
    public MainWindow(ServerViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();
        DataContext = viewModel;
    }
}

using System.Windows;
using CowBullClient.Modern.ViewModels;

namespace CowBullClient.Modern;

public partial class MainWindow : Window
{
    public MainWindow(ClientViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();
        DataContext = viewModel;
    }
}

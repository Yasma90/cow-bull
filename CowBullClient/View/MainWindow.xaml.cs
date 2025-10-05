using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CowBullClient.ViewModel;

namespace CowBullClient.View
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            DataContext = new VModelPlay();
            InitializeComponent();
        }

        //private void TboxNUmber_OnTextChanged(object sender, TextChangedEventArgs e)
        //{
        //    if (tboxNUmber.Text != "")
        //    {
        //        btnSend.IsEnabled = true;
        //    }
        //}

        //private void TboxNUmber_OnTextInput(object sender, TextCompositionEventArgs e)
        //{
        //    if (tboxNUmber.Text != "")
        //    {
        //        btnSend.IsEnabled = true;
        //    }
        //}
    }
}

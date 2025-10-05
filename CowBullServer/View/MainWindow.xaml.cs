using System;
using System.Windows;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CowBullServer.Model;
using CowBullServer.ViewModel;

namespace CowBullServer.View
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private ServerSocket _server;
        public MainWindow()
        {
            //_server = new ServerSocket();
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

        private void Click_Config(object sender, RoutedEventArgs e)
        {
            var dlg = new Configuration {Owner = this};
            Shadow.Visibility = Visibility.Visible;
            dlg.ShowDialog();
            Shadow.Visibility = Visibility.Collapsed;
        }
    }
}

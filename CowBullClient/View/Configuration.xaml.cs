using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace CowBullClient.View
{
    /// <summary>
    /// Interaction logic for Configuration.xaml
    /// </summary>
    public partial class Configuration : UserControl
    {
        public Configuration()
        {
            InitializeComponent();
        }

        private void LoginForm_KeyDown(object sender, KeyEventArgs e)
        {
            //If user pressed to enter in login form, connect to server
            if (e.Key == Key.Enter)
            {
                //ConnectToServer();
            }
        }

        private void txtNick_TextChanged(object sender, TextChangedEventArgs e)
        {
            //lblCurrentUserNick.Content = txtNick.Text;
        }
    }
}

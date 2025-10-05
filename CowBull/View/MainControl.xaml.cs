using System;
using System.Windows.Controls;

namespace CowBull.View
{
    /// <summary>
    /// Interaction logic for MainControl.xaml
    /// </summary>
    public partial class MainControl : UserControl
    {
        public MainControl()
        {
            Uri uri = new Uri("/Resources/fondo.png");
            InitializeComponent();
        }
    }
}

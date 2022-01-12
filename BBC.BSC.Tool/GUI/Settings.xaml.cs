using System.Windows.Controls;

namespace BBC.BSC.Tool.GUI
{
    /// <summary>
    /// Interaction logic for Settings.xaml
    /// </summary>
    public partial class Settings
    {
        public Settings()
        {
            InitializeComponent();
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

            Properties.Settings.Default.ere = ((TextBox)sender).Text;
            Properties.Settings.Default.Save();

        }
    }
}

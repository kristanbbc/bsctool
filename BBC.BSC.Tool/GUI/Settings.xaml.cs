using System.Configuration;
using System.Windows.Controls;

namespace BBC.BSC.Tool.GUI
{
    /// <summary>
    /// Interaction logic for Settings.xaml
    /// </summary>
    public partial class Settings
    {
        private bool _isLoaded;

        public Settings()
        {
            InitializeComponent();
            Loaded += (s, e) => _isLoaded = true;
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isLoaded)
            {
                return;
            }

            Properties.Settings.Default.ere = ((TextBox)sender).Text;
            TrySaveSettings();
        }

        private void AdPageSize_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isLoaded)
            {
                return;
            }

            if (int.TryParse(((TextBox)sender).Text, out int pageSize) && pageSize > 0)
            {
                Properties.Settings.Default.AdPageSize = pageSize;
                TrySaveSettings();
            }
        }

        private void Domain_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isLoaded)
            {
                return;
            }

            Properties.Settings.Default.Domain = ((TextBox)sender).Text.Trim();
            TrySaveSettings();
        }

        private static void TrySaveSettings()
        {
            try
            {
                Properties.Settings.Default.Save();
            }
            catch (ConfigurationErrorsException)
            {
                // user.config can be temporarily replaced/updated by another process/session;
                // avoid crashing and let the next settings change retry save.
            }
        }
    }
}

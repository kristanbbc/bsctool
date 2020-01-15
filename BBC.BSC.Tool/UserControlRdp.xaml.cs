using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;


namespace BBC.BSC.Tool
{
    /// <summary>
    /// Interaction logic for UserControlRdp.xaml
    /// </summary>
    public partial class UserControlRdp : UserControl
    {
        public UserControlRdp()
        {
            InitializeComponent();
        }
        private AxMSTSCLib.AxMsTscAxNotSafeForScripting rdp;

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.Integration.WindowsFormsHost host = new System.Windows.Forms.Integration.WindowsFormsHost();
            rdp = new AxMSTSCLib.AxMsTscAxNotSafeForScripting();

            

        }
    }
}

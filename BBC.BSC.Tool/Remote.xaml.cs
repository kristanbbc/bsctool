using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace BBC.BSC.Tool
{
    /// <summary>
    /// Interaction logic for Remote.xaml
    /// </summary>
    public partial class Remote : Window
    {
        public Remote()
        {
            InitializeComponent();
        }

        private List<AxMSTSCLib.AxMsRdpClient6> rdps = new List<AxMSTSCLib.AxMsRdpClient6>();

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.Integration.WindowsFormsHost host = new System.Windows.Forms.Integration.WindowsFormsHost();

            rdps.Add(new AxMSTSCLib.AxMsRdpClient6());
            rdps.Add(new AxMSTSCLib.AxMsRdpClient6());

            rdpHost.Child = rdps[0];
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            AxMSTSCLib.AxMsRdpClient6 rdp = rdps[0];
            rdpHost.Child = rdp;
            if (rdp.Connected == 1)
            {

            }
            else
            {
                rdp.Server = "bbcws3001";
                rdp.Width = int.Parse(rdpHost.ActualWidth.ToString());
                rdp.Height = int.Parse(rdpHost.ActualHeight.ToString());
                rdp.DesktopWidth = rdp.Width;
                rdp.DesktopHeight = rdp.Height;



                rdp.Connect();
            }
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Window w = (Window)sender;
            w.Title = string.Format($"{rdpHost.ActualWidth} x {rdpHost.ActualHeight}");
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            AxMSTSCLib.AxMsRdpClient6 rdp = rdps[1];
            rdpHost.Child = rdp;
            if (rdp.Connected == 1)
            {

            }
            else
            {
                

                rdp.Server = "3gbv1mfdra6v11";
                rdp.Width = int.Parse(rdpHost.ActualWidth.ToString());
                rdp.Height = int.Parse(rdpHost.ActualHeight.ToString());
                rdp.DesktopWidth = rdp.Width;
                rdp.DesktopHeight = rdp.Height;



                rdp.Connect();
            }
        }
    }
}

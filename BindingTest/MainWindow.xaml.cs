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

namespace BindingTest
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            dataGrid.ItemsSource = _testObjects;

            _testObjects.Add(new TestObject("1", "2"));
            _testObjects.Add(new TestObject("3", "4"));
            _testObjects.Add(new TestObject("5", "6"));
        }
        

        private List<TestObject> _testObjects = new List<TestObject>();

        private class TestObject
        {
                public string A
            {
                get;
                set;
            }
                public string B
            {
                get;
                set;
            }
            public TestObject(string a, string b)
            {
                this.A = a;
                this.B = b;
            }
        }


        private void Button_Click(object sender, RoutedEventArgs e)
        {

            _testObjects.Add(new TestObject("7", "8"));
            _testObjects.Add(new TestObject("9", "10"));
            _testObjects.Add(new TestObject("11", "12"));
            _testObjects.Add(new TestObject("13", "14"));
            _testObjects.Add(new TestObject("15", "16"));

            dataGrid.Items.Refresh();
        }
    }
}

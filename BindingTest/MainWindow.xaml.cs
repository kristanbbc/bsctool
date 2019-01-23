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
            dataGrid.ItemsSource = testObjects;

            testObjects.Add(new TestObject("1", "2"));
            testObjects.Add(new TestObject("3", "4"));
            testObjects.Add(new TestObject("5", "6"));
        }
        

        private List<TestObject> testObjects = new List<TestObject>();

        private class TestObject
        {
                public string a
            {
                get;
                set;
            }
                public string b
            {
                get;
                set;
            }
            public TestObject(string a, string b)
            {
                this.a = a;
                this.b = b;
            }
        }


        private void Button_Click(object sender, RoutedEventArgs e)
        {

            testObjects.Add(new TestObject("7", "8"));
            testObjects.Add(new TestObject("9", "10"));
            testObjects.Add(new TestObject("11", "12"));
            testObjects.Add(new TestObject("13", "14"));
            testObjects.Add(new TestObject("15", "16"));

            dataGrid.Items.Refresh();
        }
    }
}

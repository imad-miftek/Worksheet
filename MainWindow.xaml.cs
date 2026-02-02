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

namespace Worksheet
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            ToolbarControl.PlotButtonClicked += Toolbar_PlotButtonClicked;
        }

        private void Toolbar_PlotButtonClicked(object? sender, System.EventArgs e)
        {
            WorksheetGridControl.AddPlot();
        }
    }
}
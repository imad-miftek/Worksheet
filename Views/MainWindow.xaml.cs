using System.Windows;

namespace Worksheet.Views
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
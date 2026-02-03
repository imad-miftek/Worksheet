using System.Windows;
using Worksheet.Models;

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
            ToolbarControl.HistogramPlotButtonClicked += Toolbar_HistogramPlotButtonClicked;
            ToolbarControl.PseudocolorPlotButtonClicked += Toolbar_PseudocolorPlotButtonClicked;
            ToolbarControl.SpectralRibbonPlotButtonClicked += Toolbar_SpectralRibbonPlotButtonClicked;
        }

        private void Toolbar_HistogramPlotButtonClicked(object? sender, System.EventArgs e)
        {
            WorksheetGridControl.AddPlot(PlotType.Histogram, AxisScaleType.Linear);
        }

        private void Toolbar_PseudocolorPlotButtonClicked(object? sender, System.EventArgs e)
        {
            WorksheetGridControl.AddPlot(PlotType.Pseudocolor);
        }

        private void Toolbar_SpectralRibbonPlotButtonClicked(object? sender, System.EventArgs e)
        {
            WorksheetGridControl.AddPlot(PlotType.SpectralRibbon);
        }
    }
}
using System.Windows;
using Worksheet.Models;
using System;
using System.Windows.Threading;

namespace Worksheet.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer _gateStatsTimer = new(DispatcherPriority.Background);

        public MainWindow()
        {
            InitializeComponent();
            ToolbarControl.HistogramPlotButtonClicked += Toolbar_HistogramPlotButtonClicked;
            ToolbarControl.LoadHistogramConfigButtonClicked += Toolbar_LoadHistogramConfigButtonClicked;
            ToolbarControl.ClearMemoryButtonClicked += Toolbar_ClearMemoryButtonClicked;
            ToolbarControl.PseudocolorPlotButtonClicked += Toolbar_PseudocolorPlotButtonClicked;
            ToolbarControl.SpectralRibbonPlotButtonClicked += Toolbar_SpectralRibbonPlotButtonClicked;
            ToolbarControl.OscilloscopePlotButtonClicked += Toolbar_OscilloscopePlotButtonClicked;
            SidebarControl.StartStreamingClicked += Sidebar_StartStreamingClicked;
            SidebarControl.StopStreamingClicked += Sidebar_StopStreamingClicked;
            SidebarControl.RollingWindowChanged += Sidebar_RollingWindowChanged;
            SidebarControl.SetStreamingState(WorksheetGridControl.IsStreamingEnabled);
            SidebarControl.SetRollingWindowValue(WorksheetGridControl.WindowCapacity);

            _gateStatsTimer.Interval = TimeSpan.FromMilliseconds(250);
            _gateStatsTimer.Tick += (_, __) =>
            {
                try
                {
                    SidebarControl.SetGateStatsRows(WorksheetGridControl.GetGateStatsRows());
                    SidebarControl.SetProcessingStatus(WorksheetGridControl.GetProcessingStatusSnapshot());
                }
                catch
                {
                }
            };
            _gateStatsTimer.Start();
            SidebarControl.SetProcessingStatus(WorksheetGridControl.GetProcessingStatusSnapshot());

            Closed += (_, __) => _gateStatsTimer.Stop();
        }

        private void Toolbar_HistogramPlotButtonClicked(object? sender, System.EventArgs e)
        {
            WorksheetGridControl.AddPlot(PlotType.Histogram);
        }

        private void Toolbar_LoadHistogramConfigButtonClicked(object? sender, System.EventArgs e)
        {
            WorksheetGridControl.LoadLoafConfig();
        }

        private void Toolbar_ClearMemoryButtonClicked(object? sender, System.EventArgs e)
        {
            WorksheetGridControl.ClearMemory();
        }

        private void Toolbar_PseudocolorPlotButtonClicked(object? sender, System.EventArgs e)
        {
            WorksheetGridControl.AddPlot(PlotType.Pseudocolor);
        }

        private void Toolbar_SpectralRibbonPlotButtonClicked(object? sender, System.EventArgs e)
        {
            WorksheetGridControl.AddPlot(PlotType.SpectralRibbon);
        }

        private void Toolbar_OscilloscopePlotButtonClicked(object? sender, System.EventArgs e)
        {
            WorksheetGridControl.AddPlot(PlotType.Oscilloscope);
        }

        private void Sidebar_StartStreamingClicked(object? sender, System.EventArgs e)
        {
            WorksheetGridControl.SetStreamingEnabled(true);
            SidebarControl.SetStreamingState(WorksheetGridControl.IsStreamingEnabled);
        }

        private void Sidebar_StopStreamingClicked(object? sender, System.EventArgs e)
        {
            WorksheetGridControl.SetStreamingEnabled(false);
            SidebarControl.SetStreamingState(WorksheetGridControl.IsStreamingEnabled);
        }

        private void Sidebar_RollingWindowChanged(int windowCapacity)
        {
            WorksheetGridControl.SetWindowCapacity(windowCapacity);
            SidebarControl.SetRollingWindowValue(WorksheetGridControl.WindowCapacity);
        }
    }
}

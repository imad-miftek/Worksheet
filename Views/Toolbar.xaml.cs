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

namespace Worksheet.Views
{
    /// <summary>
    /// Interaction logic for Toolbar.xaml
    /// </summary>
    public partial class Toolbar : UserControl
    {
        public event EventHandler? HistogramPlotButtonClicked;
        public event EventHandler? LoadHistogramConfigButtonClicked;
        public event EventHandler? ClearMemoryButtonClicked;
        public event EventHandler? PseudocolorPlotButtonClicked;
        public event EventHandler? SpectralRibbonPlotButtonClicked;
        public event EventHandler? OscilloscopePlotButtonClicked;

        public Toolbar()
        {
            InitializeComponent();
        }

        private void HistogramPlotButton_Click(object sender, RoutedEventArgs e)
        {
            HistogramPlotButtonClicked?.Invoke(this, EventArgs.Empty);
        }

        private void LoadHistogramConfigButton_Click(object sender, RoutedEventArgs e)
        {
            LoadHistogramConfigButtonClicked?.Invoke(this, EventArgs.Empty);
        }

        private void ClearMemoryButton_Click(object sender, RoutedEventArgs e)
        {
            ClearMemoryButtonClicked?.Invoke(this, EventArgs.Empty);
        }

        private void PseudocolorPlotButton_Click(object sender, RoutedEventArgs e)
        {
            PseudocolorPlotButtonClicked?.Invoke(this, EventArgs.Empty);
        }

        private void SpectralRibbonPlotButton_Click(object sender, RoutedEventArgs e)
        {
            SpectralRibbonPlotButtonClicked?.Invoke(this, EventArgs.Empty);
        }

        private void OscilloscopePlotButton_Click(object sender, RoutedEventArgs e)
        {
            OscilloscopePlotButtonClicked?.Invoke(this, EventArgs.Empty);
        }
    }
}

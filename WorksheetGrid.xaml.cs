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
using ScottPlot.MultiplotLayouts;
using ScottPlot.WPF;

namespace Worksheet
{
    /// <summary>
    /// Interaction logic for WorksheetGrid.xaml
    /// </summary>
    public partial class WorksheetGrid : UserControl
    {
        private int plotsAdded = 0;
        private int columns = 3;

        public WorksheetGrid()
        {
            InitializeComponent();
        }

        public void AddPlot()
        {
            var plot = new WpfPlot();
            double[] xs = { 1, 2, 3, 4, 5 };
            double[] ys = { 1, 4, 9, 16, 25 };
            plot.Plot.Add.Scatter(xs, ys);
            plot.Height = 200;
            plot.Width = 200;
            plot.Plot.FigureBackground.Color = ScottPlot.Color.FromARGB(0);

            WorksheetGridContainer.Children.Add(plot);
        }
    }
}

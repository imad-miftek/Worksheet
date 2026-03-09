using System.Windows;
using Worksheet.Models;

namespace Worksheet.Views.PlotViews.Dialogs
{
    public partial class SpectralRibbonPropertiesDialog : Window
    {
        public AxisScaleType SelectedYAxisScale { get; private set; }

        public SpectralRibbonPropertiesDialog(AxisScaleType currentScale)
        {
            InitializeComponent();

            YAxisScaleComboBox.ItemsSource = new[] { AxisScaleType.Linear, AxisScaleType.Logarithmic };
            YAxisScaleComboBox.SelectedItem = currentScale;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (YAxisScaleComboBox.SelectedItem is AxisScaleType yScale)
            {
                SelectedYAxisScale = yScale;
                DialogResult = true;
            }
            else
            {
                DialogResult = false;
            }
        }
    }
}

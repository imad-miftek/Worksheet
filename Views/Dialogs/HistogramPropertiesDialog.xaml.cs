using System.Linq;
using System.Windows;
using Worksheet.Models;

namespace Worksheet.Views.Dialogs
{
    public partial class HistogramPropertiesDialog : Window
    {
        public AxisScaleType SelectedAxisScale { get; private set; }

        public HistogramPropertiesDialog(AxisScaleType currentScale)
        {
            InitializeComponent();

            AxisScaleComboBox.ItemsSource = new[] { AxisScaleType.Linear, AxisScaleType.Logarithmic };
            AxisScaleComboBox.SelectedItem = currentScale;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (AxisScaleComboBox.SelectedItem is AxisScaleType selected)
            {
                SelectedAxisScale = selected;
                DialogResult = true;
            }
            else
            {
                DialogResult = false;
            }
        }
    }
}

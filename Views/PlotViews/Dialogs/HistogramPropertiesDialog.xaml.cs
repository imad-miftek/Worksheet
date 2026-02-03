using System.Windows;
using Worksheet.Models;

namespace Worksheet.Views.PlotViews.Dialogs
{
    public partial class HistogramPropertiesDialog : Window
    {
        public AxisScaleType SelectedAxisScale { get; private set; }
        public int SelectedChannelIndex { get; private set; }

        public HistogramPropertiesDialog(AxisScaleType currentScale, System.Collections.Generic.IReadOnlyList<string> channelNames, int currentChannelIndex)
        {
            InitializeComponent();

            AxisScaleComboBox.ItemsSource = new[] { AxisScaleType.Linear, AxisScaleType.Logarithmic };
            AxisScaleComboBox.SelectedItem = currentScale;

            ChannelComboBox.ItemsSource = channelNames;
            if (channelNames.Count > 0)
            {
                if (currentChannelIndex < 0)
                    currentChannelIndex = 0;
                if (currentChannelIndex >= channelNames.Count)
                    currentChannelIndex = channelNames.Count - 1;
                ChannelComboBox.SelectedIndex = currentChannelIndex;
            }
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (AxisScaleComboBox.SelectedItem is AxisScaleType selected && ChannelComboBox.SelectedIndex >= 0)
            {
                SelectedAxisScale = selected;
                SelectedChannelIndex = ChannelComboBox.SelectedIndex;
                DialogResult = true;
            }
            else
            {
                DialogResult = false;
            }
        }
    }
}

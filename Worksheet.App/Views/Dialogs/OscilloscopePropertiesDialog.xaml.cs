using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Worksheet.Views.PlotViews.Dialogs
{
    public partial class OscilloscopePropertiesDialog : Window
    {
        private readonly List<CheckBox> _channelCheckBoxes = new();

        public OscilloscopePropertiesDialog(int channelCount, IReadOnlyList<int> selectedChannelIndices)
        {
            InitializeComponent();

            if (channelCount <= 0)
                channelCount = 1;

            var selected = new HashSet<int>(selectedChannelIndices ?? []);
            if (selected.Count == 0)
                selected.Add(0);

            for (int i = 0; i < channelCount; i++)
            {
                var checkBox = new CheckBox
                {
                    Content = $"Channel {i}",
                    Tag = i,
                    IsChecked = selected.Contains(i),
                    Margin = new Thickness(0, i == 0 ? 0 : 4, 0, 0)
                };
                _channelCheckBoxes.Add(checkBox);
                ChannelsPanel.Children.Add(checkBox);
            }
        }

        public int[] SelectedChannelIndices { get; private set; } = [0];

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            var selected = _channelCheckBoxes
                .Where(checkBox => checkBox.IsChecked == true)
                .Select(checkBox => (int)checkBox.Tag)
                .ToArray();

            if (selected.Length == 0)
            {
                MessageBox.Show(this, "Select at least one channel.", "Oscilloscope Properties", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SelectedChannelIndices = selected;
            DialogResult = true;
        }
    }
}

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
    /// Interaction logic for Sidebar.xaml
    /// </summary>
    public partial class Sidebar : UserControl
    {
        public event EventHandler? StartStreamingClicked;
        public event EventHandler? StopStreamingClicked;

        public Sidebar()
        {
            InitializeComponent();
        }

        private void StartStreamingButton_Click(object sender, RoutedEventArgs e)
        {
            StartStreamingClicked?.Invoke(this, EventArgs.Empty);
        }

        private void StopStreamingButton_Click(object sender, RoutedEventArgs e)
        {
            StopStreamingClicked?.Invoke(this, EventArgs.Empty);
        }

        public void SetStreamingState(bool isStreamingEnabled)
        {
            StartStreamingButton.IsEnabled = !isStreamingEnabled;
            StopStreamingButton.IsEnabled = isStreamingEnabled;
            StreamingStatusText.Text = isStreamingEnabled ? "Status: Running" : "Status: Stopped";
        }
    }
}

using System;
using System.Configuration;
using System.Data;
using System.IO;
using System.Windows;
using Worksheet.Services;

namespace Worksheet
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Load channel settings from JSON file
            LoadChannelConfiguration();
        }

        private void LoadChannelConfiguration()
        {
            // Load from application base directory (channels.json is copied to output by .csproj)
            var path = Path.Combine(AppContext.BaseDirectory, "channels.json");

            if (!File.Exists(path))
            {
                Console.WriteLine($"Warning: Channel configuration file not found at: {path}");
                Console.WriteLine("Using empty channel list.");
                return;
            }

            try
            {
                FeatureSelectionStrategy.LoadChannelSettings(path);
                var channelCount = FeatureSelectionStrategy.ChannelNames.Count;
                var allChannelCount = FeatureSelectionStrategy.AllChannelNames.Count;
                Console.WriteLine($"Loaded channel configuration from: {path}");
                Console.WriteLine($"Total channels: {allChannelCount}, Filtered (numeric) channels: {channelCount}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load channel configuration from {path}: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
    }

}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Worksheet.Models;

namespace Worksheet.Services
{
    public class ChannelSettings
    {
        private static readonly string[] Disconnected = { "nc", "disconnected", "none", "null" };

        public List<ChannelInfo> Channels { get; private set; } = new List<ChannelInfo>();
        public List<ChannelInfo> AllChannels { get; private set; } = new List<ChannelInfo>();
        public int ChannelCount => AllChannels.Count;

        public bool LoadFromJsonFile(string jsonFilePath)
        {
            if (!File.Exists(jsonFilePath))
            {
                Console.WriteLine($"Configuration file not found: {jsonFilePath}");
                return false;
            }

            try
            {
                return LoadFromJson(jsonFilePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load channel configuration: {ex.Message}");
                return false;
            }
        }

        private bool LoadFromJson(string jsonFilePath)
        {
            var jsonText = File.ReadAllText(jsonFilePath);
            var jsonDoc = JsonDocument.Parse(jsonText);

            if (!jsonDoc.RootElement.TryGetProperty("channels", out var channelsElement))
            {
                Console.WriteLine("JSON file does not contain 'channels' property");
                return false;
            }

            // Parse channels in order (don't sort - keep JSON order to match DataSource)
            int id = 0;
            foreach (var property in channelsElement.EnumerateObject())
            {
                var key = property.Name;
                var value = property.Value.GetString();

                if (!string.IsNullOrEmpty(value))
                {
                    var channelInfo = new ChannelInfo(id, key, value, value);
                    AllChannels.Add(channelInfo);

                    // Only add connected channels
                    if (!Disconnected.Contains(value.ToLower()))
                    {
                        Channels.Add(channelInfo);
                    }

                    id++;
                }
            }

            return true;
        }

        // Keep the old method for backward compatibility, redirects to JSON
        public bool LoadFromIniFile(string iniFilePath)
        {
            return LoadFromJsonFile(iniFilePath);
        }

        public List<string> GetAdcChannels()
        {
            return Channels.Select(c => c.Wavelength).ToList();
        }

        public List<int> GetAdcIndices()
        {
            return Channels.Select(c => c.Id).ToList();
        }

        public List<string> GetAdcChannelsFiltered()
        {
            return Channels
                .Where(c => IsNumericWavelength(c.Wavelength))
                .Select(c => c.Wavelength)
                .ToList();
        }

        public List<int> GetAdcIndicesFiltered()
        {
            return Channels
                .Where(c => IsNumericWavelength(c.Wavelength))
                .Select(c => c.Id)
                .ToList();
        }

        private bool IsNumericWavelength(string wavelength)
        {
            try
            {
                // Handle wavelengths with 'nm' suffix
                var numericPart = wavelength.EndsWith("nm", StringComparison.OrdinalIgnoreCase)
                    ? wavelength.Substring(0, wavelength.Length - 2)
                    : wavelength;

                return double.TryParse(numericPart, out _);
            }
            catch
            {
                return false;
            }
        }

        private class NaturalChannelComparer : IComparer<string>
        {
            public int Compare(string? x, string? y)
            {
                x ??= string.Empty;
                y ??= string.Empty;

                var xParts = GetSortKey(x);
                var yParts = GetSortKey(y);

                int prefixCompare = string.Compare(xParts.prefix, yParts.prefix, StringComparison.Ordinal);
                if (prefixCompare != 0)
                    return prefixCompare;

                return xParts.number.CompareTo(yParts.number);
            }

            private (string prefix, int number) GetSortKey(string text)
            {
                var match = Regex.Match(text, @"\.(\d+)$");
                if (match.Success)
                {
                    var prefix = text.Substring(0, match.Index);
                    var number = int.Parse(match.Groups[1].Value);
                    return (prefix, number);
                }
                return (text, 0);
            }
        }
    }
}

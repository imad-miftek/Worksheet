using System.Collections.Generic;
using Worksheet.Models;

namespace Worksheet.Services
{
    public class FeatureSelectionStrategy
    {
        private readonly Dictionary<PlotType, (string[] x, string[] y)> _features = new()
        {
            { PlotType.Histogram, (new[] { "intensity", "signal" }, new[] { "frequency" }) },
            { PlotType.Pseudocolor, (new[] { "x", "x2" }, new[] { "y", "y2" }) },
            { PlotType.SpectralRibbon, (new[] { "sample" }, new[] { "intensity", "power" }) }
        };

        public IReadOnlyList<string> GetXFeatures(PlotType plotType)
        {
            return _features.TryGetValue(plotType, out var f) ? f.x : new string[0];
        }

        public IReadOnlyList<string> GetYFeatures(PlotType plotType)
        {
            return _features.TryGetValue(plotType, out var f) ? f.y : new string[0];
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Worksheet.Models;

namespace Worksheet.Services
{
    public class FeatureSelectionStrategy
    {
        private static readonly string[] HistogramChannelNames =
        {
            "FSC-A", "FSC-H", "FSC-W", "SSC-A", "SSC-H", "SSC-W",
            "FITC-A", "FITC-H", "PE-A", "PE-H", "PerCP-A", "PerCP-Cy5.5-A",
            "PE-Cy7-A", "APC-A", "APC-H7-A", "APC-Cy7-A", "BV421-A", "BV510-A",
            "BV605-A", "BV650-A", "BV711-A", "BV785-A", "BB515-A", "BB700-A",
            "BB750-A", "Alexa488-A", "Alexa532-A", "Alexa555-A", "Alexa594-A",
            "Alexa647-A", "Alexa700-A", "Alexa750-A", "Pacific Blue-A",
            "Pacific Green-A", "Pacific Orange-A", "eFluor450-A", "eFluor506-A",
            "eFluor660-A", "eFluor710-A", "eFluor780-A", "PE-Texas Red-A",
            "PE-CF594-A", "PE-Cy5-A", "PE-Cy5.5-A", "PE-Cy5.5-H",
            "APC-R700-A", "APC-Fire750-A", "APC-Fire810-A", "Cy5-A",
            "Cy5.5-A", "Cy7-A", "DAPI-A", "7-AAD-A", "PI-A",
            "Zombie Aqua-A", "Zombie Green-A", "Zombie Red-A", "Zombie NIR-A",
            "LiveDead Violet-A", "LiveDead Aqua-A"
        };

        public IReadOnlyList<string> GetXFeatureNames(PlotType plotType)
        {
            return plotType == PlotType.Histogram
                ? HistogramChannelNames
                : Array.Empty<string>();
        }

        public IReadOnlyList<int> GetXFeatureIndices(PlotType plotType)
        {
            return plotType == PlotType.Histogram
                ? Enumerable.Range(0, HistogramChannelNames.Length).ToArray()
                : Array.Empty<int>();
        }
    }
}

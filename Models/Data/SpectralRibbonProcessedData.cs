using System;

namespace Worksheet.Models.Data
{
    using Worksheet.Models;

    public class SpectralRibbonProcessedData : ProcessedPlotData
    {
        public SpectralRibbonProcessedData(Guid plotId, double[][] channels)
            : base(plotId, PlotType.SpectralRibbon)
        {
            Channels = channels;
        }

        public double[][] Channels { get; }
    }
}

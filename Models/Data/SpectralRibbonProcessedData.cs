using System;
using System.Collections.Generic;

namespace Worksheet.Models.Data
{
    using Worksheet.Models;

    public class SpectralRibbonProcessedData : ProcessedPlotData
    {
        public SpectralRibbonProcessedData(Guid plotId, double[,] data, IReadOnlyList<string> channelNames)
            : base(plotId, PlotType.SpectralRibbon)
        {
            Data = data;
            ChannelNames = channelNames;
        }

        public double[,] Data { get; }

        public IReadOnlyList<string> ChannelNames { get; }
    }
}

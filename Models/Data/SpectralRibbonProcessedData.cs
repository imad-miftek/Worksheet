using System;
using System.Collections.Generic;

namespace Worksheet.Models.Data
{
    using Worksheet.Models;

    public class SpectralRibbonProcessedData : ProcessedPlotData
    {
        public SpectralRibbonProcessedData(Guid plotId, double[,] data, IReadOnlyList<string> channelNames, bool isEmpty)
            : base(plotId, PlotType.SpectralRibbon)
        {
            Data = data;
            ChannelNames = channelNames;
            IsEmpty = isEmpty;
        }

        public double[,] Data { get; }

        public IReadOnlyList<string> ChannelNames { get; }

        public bool IsEmpty { get; }
    }
}

using System;

namespace Worksheet.Models.Data
{
    using Worksheet.Models;

    public class HistogramProcessedData : ProcessedPlotData
    {
        public HistogramProcessedData(Guid plotId, double[] positions, double[] counts, HistogramBinning binning)
            : base(plotId, PlotType.Histogram)
        {
            Positions = positions;
            Counts = counts;
            Binning = binning;
        }

        public double[] Positions { get; }
        public double[] Counts { get; }
        public HistogramBinning Binning { get; }
    }
}

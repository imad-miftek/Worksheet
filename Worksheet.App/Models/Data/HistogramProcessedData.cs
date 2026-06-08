using System;

namespace Worksheet.Models.Data
{
    using Worksheet.Models;

    public class HistogramProcessedData : ProcessedPlotData
    {
        public HistogramProcessedData(Guid plotId, double[] positions, double[] counts, int binCount, AxisScaleType scaleType)
            : base(plotId, PlotType.Histogram)
        {
            Positions = positions;
            Counts = counts;
            BinCount = binCount;
            ScaleType = scaleType;
        }

        public double[] Positions { get; }
        public double[] Counts { get; }
        public int BinCount { get; }
        public AxisScaleType ScaleType { get; }
    }
}

using System;

namespace Worksheet.Models.Data
{
    using Worksheet.Models;

    public class HeatmapProcessedData : ProcessedPlotData
    {
        public HeatmapProcessedData(Guid plotId, double[,] data)
            : base(plotId, PlotType.Pseudocolor)
        {
            Data = data;
        }

        public double[,] Data { get; }
    }
}

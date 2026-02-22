using System;

namespace Worksheet.Models.Data
{
    using Worksheet.Models;

    public class HeatmapProcessedData : ProcessedPlotData
    {
        public HeatmapProcessedData(Guid plotId, double[,] data, bool isEmpty)
            : base(plotId, PlotType.Pseudocolor)
        {
            Data = data;
            IsEmpty = isEmpty;
        }

        public double[,] Data { get; }
        public bool IsEmpty { get; }
    }
}

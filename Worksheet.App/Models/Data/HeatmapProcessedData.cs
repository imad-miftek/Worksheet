using System;

namespace Worksheet.Models.Data
{
    using Worksheet.Models;

    public class HeatmapProcessedData : ProcessedPlotData
    {
        public HeatmapProcessedData(Guid plotId, double[,] data, byte[] pixelBuffer, int bins, bool isEmpty)
            : base(plotId, PlotType.Pseudocolor)
        {
            Data = data;
            PixelBuffer = pixelBuffer;
            Bins = bins;
            IsEmpty = isEmpty;
        }

        public double[,] Data { get; }
        public byte[] PixelBuffer { get; }
        public int Bins { get; }
        public bool IsEmpty { get; }
    }
}

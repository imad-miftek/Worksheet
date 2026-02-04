using System;

namespace Worksheet.Models.Data
{
    using Worksheet.Models;

    public class OscilloscopeProcessedData : ProcessedPlotData
    {
        public OscilloscopeProcessedData(Guid plotId, double[][] signals)
            : base(plotId, PlotType.Oscilloscope)
        {
            Signals = signals;
        }

        public double[][] Signals { get; }
    }
}

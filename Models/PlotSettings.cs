using System;

namespace Worksheet.Models
{
    public class PlotSettings
    {
        public Guid Id { get; } = Guid.NewGuid();
        public PlotType PlotType { get; set; }
        public int BinCount { get; set; } = 256;
        public string XFeature { get; set; } = string.Empty;
        public string YFeature { get; set; } = string.Empty;
        public AxisScaleType XAxisScaleType { get; set; } = AxisScaleType.Linear;
        public AxisScaleType YAxisScaleType { get; set; } = AxisScaleType.Linear;
    }
}

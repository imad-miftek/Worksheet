using System;

namespace Worksheet.Models
{
    public class PlotSettings
    {
        public Guid Id { get; } = Guid.NewGuid();
        public PlotType PlotType { get; set; }
        public int BinCount { get; set; } = 256;
        public int XFeature { get; set; }
        public int YFeature { get; set; }
        public AxisScaleType XAxisScaleType { get; set; } = AxisScaleType.Linear;
        public AxisScaleType YAxisScaleType { get; set; } = AxisScaleType.Linear;
        public double MinValue { get; set; } = 0;
        public double MaxValue { get; set; } = 100_000_000;
        public int OscilloscopeChannelCount { get; set; } = 4;
        public int[] OscilloscopeChannelIndices { get; set; } = [0];

        public int GetBinCount()
        {
            return BinCount > 0 ? BinCount : 256;
        }

        public double DataValueToBinPosition(double value, AxisScaleType scaleType)
        {
            double min = MinValue;
            double max = MaxValue;

            if (scaleType == AxisScaleType.Logarithmic)
            {
                if (min < 1)
                    min = 1;

                if (max <= min)
                    max = min * 10;

                if (value < min) value = min;
                if (value > max) value = max;

                double minLog = Math.Log10(min);
                double maxLog = Math.Log10(max);
                double log = Math.Log10(value);
                double t = (log - minLog) / (maxLog - minLog);
                return t * GetBinCount();
            }

            if (max <= min)
                max = min + 1;

            if (value < min) value = min;
            if (value > max) value = max;

            double frac = (value - min) / (max - min);
            return frac * GetBinCount();
        }
    }
}

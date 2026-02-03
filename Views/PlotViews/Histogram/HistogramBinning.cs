using System;
using Worksheet.Models;

namespace Worksheet.Views.PlotViews.Histogram
{
    public class HistogramBinning
    {
        public int BinCount { get; }
        public AxisScaleType ScaleType { get; }
        public double MinValue { get; }
        public double MaxValue { get; }

        private readonly double _minLog;
        private readonly double _maxLog;

        public HistogramBinning(int binCount, AxisScaleType scaleType)
        {
            BinCount = binCount;
            ScaleType = scaleType;

            if (scaleType == AxisScaleType.Logarithmic)
            {
                MinValue = 1;
                MaxValue = 100_000_000;
                _minLog = Math.Log10(MinValue);
                _maxLog = Math.Log10(MaxValue);
            }
            else
            {
                MinValue = 0;
                MaxValue = 100_000_000;
                _minLog = 0;
                _maxLog = 0;
            }
        }

        public double[] CreateCounts(double[] values)
        {
            var counts = new double[BinCount];

            foreach (var raw in values)
            {
                var pos = DataValueToBinPosition(raw);
                var index = (int)Math.Floor(pos);

                if (index < 0)
                    index = 0;
                else if (index >= BinCount)
                    index = BinCount - 1;

                counts[index]++;
            }

            return counts;
        }

        public double[] CreateBinPositions()
        {
            var positions = new double[BinCount];
            for (int i = 0; i < BinCount; i++)
                positions[i] = i + 0.5;
            return positions;
        }

        public double DataValueToBinPosition(double value)
        {
            if (ScaleType == AxisScaleType.Logarithmic)
            {
                if (value < MinValue) value = MinValue;
                if (value > MaxValue) value = MaxValue;

                var log = Math.Log10(value);
                var t = (log - _minLog) / (_maxLog - _minLog);
                return t * BinCount;
            }

            if (value < MinValue) value = MinValue;
            if (value > MaxValue) value = MaxValue;

            var frac = (value - MinValue) / (MaxValue - MinValue);
            return frac * BinCount;
        }

        public double BinPositionToDataValue(double position)
        {
            var t = position / BinCount;

            if (ScaleType == AxisScaleType.Logarithmic)
            {
                var log = _minLog + t * (_maxLog - _minLog);
                return Math.Pow(10, log);
            }

            return MinValue + t * (MaxValue - MinValue);
        }
    }
}

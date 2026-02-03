using System;

namespace Worksheet.Services
{
    public class DataSource
    {
        private readonly double[] _histogramValues;
        private readonly double[][] _spectralChannels;
        private readonly double[,] _heatmapData;

        public DataSource()
        {
            _histogramValues = BuildHistogramValues();
            _spectralChannels = BuildSpectralChannels(60, 240);
            _heatmapData = BuildHeatmapData(60, 60);
        }

        public double[] GetHistogramValues(string feature)
        {
            return _histogramValues;
        }

        public double[][] GetSpectralChannels(string feature)
        {
            return _spectralChannels;
        }

        public double[,] GetHeatmapData(string xFeature, string yFeature)
        {
            return _heatmapData;
        }

        private static double[] BuildHistogramValues()
        {
            var random = new Random(3);
            int sampleCount = 60000;
            double mean = 150;
            double stdDev = 35;

            var values = new double[sampleCount];
            for (int i = 0; i < sampleCount; i += 2)
            {
                double u1 = random.NextDouble();
                double u2 = random.NextDouble();
                double z0 = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
                double z1 = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);

                values[i] = mean + stdDev * z0;
                if (i + 1 < sampleCount)
                    values[i + 1] = mean + stdDev * z1;
            }

            return values;
        }

        private static double[][] BuildSpectralChannels(int channelCount, int pointCount)
        {
            var random = new Random(4);
            var channels = new double[channelCount][];

            for (int i = 0; i < channelCount; i++)
            {
                double baseFreq = 0.015 + i * 0.0015;
                double phase = i * 0.2;
                double amplitude = 0.8 + (i % 5) * 0.12;

                var values = new double[pointCount];
                for (int j = 0; j < pointCount; j++)
                {
                    double x = j;
                    double noise = (random.NextDouble() - 0.5) * 0.2;
                    values[j] = amplitude * Math.Sin(baseFreq * x + phase) + noise + i * 0.015;
                }

                channels[i] = values;
            }

            return channels;
        }

        private static double[,] BuildHeatmapData(int width, int height)
        {
            var data = new double[width, height];
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    double v1 = Math.Sin(x * 0.12) * Math.Cos(y * 0.18);
                    double v2 = Math.Sin((x + y) * 0.07);
                    data[x, y] = v1 + v2;
                }
            }

            return data;
        }
    }
}

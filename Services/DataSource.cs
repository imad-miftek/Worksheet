using System;

namespace Worksheet.Services
{
    public class DataSource
    {
        private readonly double[][] _channels;

        public DataSource()
        {
            _channels = BuildChannelValues(60);
        }

        public double[] Get(int featureIndex)
        {
            if (_channels.Length == 0)
                return Array.Empty<double>();

            if (featureIndex < 0)
                featureIndex = 0;
            else if (featureIndex >= _channels.Length)
                featureIndex = _channels.Length - 1;

            return _channels[featureIndex];
        }

        private static double[][] BuildChannelValues(int channelCount)
        {
            var channels = new double[channelCount][];

            for (int i = 0; i < channelCount; i++)
            {
                var random = new Random(100 + i);
                int sampleCount = 18000 + (i * 900);
                double mean = 80 + (i * 2.3);
                double stdDev = 18 + (i % 6) * 3.5;

                var values = new double[sampleCount];
                for (int j = 0; j < sampleCount; j += 2)
                {
                    double u1 = random.NextDouble();
                    double u2 = random.NextDouble();
                    double z0 = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
                    double z1 = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);

                    values[j] = mean + stdDev * z0;
                    if (j + 1 < sampleCount)
                        values[j + 1] = mean + stdDev * z1;
                }

                channels[i] = values;
            }

            return channels;
        }
    }
}

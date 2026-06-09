using System;

namespace Worksheet.Services
{
    public sealed class Event
    {
        public Event(double[] parameters)
        {
            Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        }

        public double[] Parameters { get; }

        public int SignalCount => Parameters.Length;

        public double GetSignalValue(int signalIndex)
        {
            if ((uint)signalIndex >= (uint)Parameters.Length)
                throw new ArgumentOutOfRangeException(nameof(signalIndex));

            return Parameters[signalIndex];
        }
    }
}

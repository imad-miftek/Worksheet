using System;

namespace Worksheet.Services
{
    public sealed class Event : IEventSignalValues
    {
        private readonly double[] _values;

        public Event(double[] values)
        {
            _values = values ?? throw new ArgumentNullException(nameof(values));
        }

        public int SignalCount => _values.Length;

        public double GetSignalValue(int signalIndex)
        {
            if ((uint)signalIndex >= (uint)_values.Length)
                throw new ArgumentOutOfRangeException(nameof(signalIndex));

            return _values[signalIndex];
        }
    }
}

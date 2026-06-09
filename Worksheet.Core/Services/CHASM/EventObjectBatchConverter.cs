using System;
using System.Collections.Generic;
using System.Threading.Channels;
using System.Threading.Tasks;
using Worksheet.Models;

namespace Worksheet.Services
{
    public sealed class EventObjectBatchConverter<TEvent>
    {
        public const int DefaultParallelCellThreshold = 250_000;

        private readonly SignalLayout _signalLayout;
        private readonly Func<TEvent, int, double> _readSignalValue;

        public EventObjectBatchConverter(
            SignalLayout signalLayout,
            int maxBatchSize = 1000,
            int parallelCellThreshold = DefaultParallelCellThreshold)
            : this(
                signalLayout,
                static (ev, signalIndex) =>
                {
                    if (ev is not IEventSignalValues signalValues)
                        throw new InvalidOperationException($"{typeof(TEvent).Name} must implement {nameof(IEventSignalValues)} when no value reader is provided.");

                    return signalValues.GetSignalValue(signalIndex);
                },
                maxBatchSize,
                parallelCellThreshold)
        {
        }

        public EventObjectBatchConverter(
            SignalLayout signalLayout,
            Func<TEvent, int, double> readSignalValue,
            int maxBatchSize = 1000,
            int parallelCellThreshold = DefaultParallelCellThreshold)
        {
            if (readSignalValue == null)
                throw new ArgumentNullException(nameof(readSignalValue));
            if (maxBatchSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxBatchSize));
            if (parallelCellThreshold < 0)
                throw new ArgumentOutOfRangeException(nameof(parallelCellThreshold));

            _signalLayout = signalLayout;
            _readSignalValue = readSignalValue;
            MaxBatchSize = maxBatchSize;
            ParallelCellThreshold = parallelCellThreshold;
        }

        public int SignalCount => _signalLayout.SignalCount;

        public int MaxBatchSize { get; }

        public int ParallelCellThreshold { get; }

        public IReadOnlyList<ColumnMajorEventBatch> Convert(IReadOnlyList<TEvent> events)
        {
            if (events == null)
                throw new ArgumentNullException(nameof(events));
            if (events.Count == 0)
                return Array.Empty<ColumnMajorEventBatch>();

            var batches = new List<ColumnMajorEventBatch>((events.Count + MaxBatchSize - 1) / MaxBatchSize);
            for (int offset = 0; offset < events.Count; offset += MaxBatchSize)
            {
                int count = Math.Min(MaxBatchSize, events.Count - offset);
                batches.Add(ConvertChunk(events, offset, count));
            }

            return batches;
        }

        public int TryWriteTo(ChannelWriter<IEventBatch> writer, IReadOnlyList<TEvent> events)
        {
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));

            int written = 0;
            foreach (var batch in Convert(events))
            {
                if (!writer.TryWrite(batch))
                    break;

                written++;
            }

            return written;
        }

        private ColumnMajorEventBatch ConvertChunk(IReadOnlyList<TEvent> events, int offset, int count)
        {
            var values = new double[checked(_signalLayout.SignalCount * count)];
            int cellCount = values.Length;

            if (ParallelCellThreshold > 0 && cellCount >= ParallelCellThreshold)
                FillColumnMajorParallel(events, offset, count, values);
            else
                FillColumnMajor(events, offset, count, values);

            return new ColumnMajorEventBatch(count, values, _signalLayout);
        }

        private void FillColumnMajor(IReadOnlyList<TEvent> events, int offset, int count, double[] values)
        {
            for (int signal = 0; signal < _signalLayout.SignalCount; signal++)
            {
                int signalOffset = signal * count;
                for (int e = 0; e < count; e++)
                    values[signalOffset + e] = _readSignalValue(events[offset + e], signal);
            }
        }

        private void FillColumnMajorParallel(IReadOnlyList<TEvent> events, int offset, int count, double[] values)
        {
            Parallel.For(0, _signalLayout.SignalCount, signal =>
            {
                int signalOffset = signal * count;
                for (int e = 0; e < count; e++)
                    values[signalOffset + e] = _readSignalValue(events[offset + e], signal);
            });
        }
    }
}

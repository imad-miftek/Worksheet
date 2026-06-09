using System;
using System.Collections.Generic;
using System.Threading.Channels;
using Worksheet.Models;

namespace Worksheet.Services
{
    public sealed class EventProducer : IProducer, IEventIngestionPort
    {
        private readonly Channel<IEventBatch> _channel;
        private readonly EventBatchConverter _converter;
        private readonly SignalLayout _signalLayout;
        private volatile bool _running;

        public EventProducer(
            ChasmOptions? options = null,
            int maxBatchSize = 1000,
            int parallelCellThreshold = EventBatchConverter.DefaultParallelCellThreshold)
            : this(
                options?.SignalLayout ?? ChasmOptions.Default.SignalLayout,
                options?.ChannelCapacityBatches ?? ChasmOptions.Default.ChannelCapacityBatches,
                maxBatchSize,
                parallelCellThreshold)
        {
        }

        public EventProducer(
            SignalLayout signalLayout,
            int channelCapacityBatches,
            int maxBatchSize = 1000,
            int parallelCellThreshold = EventBatchConverter.DefaultParallelCellThreshold)
        {
            if (channelCapacityBatches <= 0)
                throw new ArgumentOutOfRangeException(nameof(channelCapacityBatches));

            _signalLayout = signalLayout;

            var bounded = new BoundedChannelOptions(channelCapacityBatches)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false,
            };

            _channel = Channel.CreateBounded<IEventBatch>(bounded);
            _converter = new EventBatchConverter(
                signalLayout,
                maxBatchSize,
                parallelCellThreshold);
        }

        public ChannelReader<IEventBatch> Reader => _channel.Reader;

        public void Start()
        {
            _running = true;
        }

        public void Stop()
        {
            _running = false;

            while (_channel.Reader.TryRead(out _)) { }
        }

        public int Publish(IReadOnlyList<Event> events)
        {
            return PublishEvents(events);
        }

        public int PublishEvents(IReadOnlyList<Event> events)
        {
            if (events == null)
                throw new ArgumentNullException(nameof(events));
            if (!_running || events.Count == 0)
                return 0;

            int written = 0;
            foreach (var batch in _converter.Convert(events))
            {
                if (_channel.Writer.TryWrite(batch))
                    written++;
            }

            return written;
        }

        public int PublishColumnMajor(double[] values, int eventCount)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));
            if (!_running || eventCount == 0)
                return 0;

            var batch = new ColumnMajorEventBatch(eventCount, values, _signalLayout);
            return _channel.Writer.TryWrite(batch) ? 1 : 0;
        }
    }
}

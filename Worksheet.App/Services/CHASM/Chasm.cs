using System;
using System.Threading;
using System.Threading.Tasks;

namespace Worksheet.Services
{
    public sealed class Chasm : IDisposable
    {
        private readonly IProducer _producer;
        private readonly IConsumer _consumer;
        private readonly IChasmDataSource _dataSource;

        private CancellationTokenSource? _consumerCts;
        private Task? _consumerTask;

        public Chasm(IProducer producer, IConsumer consumer, IChasmDataSource dataSource)
        {
            _producer = producer;
            _consumer = consumer;
            _dataSource = dataSource;
        }

        public bool IsStreamingEnabled { get; private set; }

        public long DataVersion => _dataSource.DataVersion;

        public void StartStreaming()
        {
            if (IsStreamingEnabled)
                return;

            IsStreamingEnabled = true;
            _dataSource.SetStreamingEnabled(true);

            _consumerCts = new CancellationTokenSource();
            _consumerTask = Task.Run(() => _consumer.RunAsync(_consumerCts.Token));

            _producer.Start();
        }

        public void StopStreaming()
        {
            if (!IsStreamingEnabled)
                return;

            IsStreamingEnabled = false;
            _dataSource.SetStreamingEnabled(false);

            _producer.Stop();
            _consumerCts?.Cancel();

            // Drain any queued batches so restart doesn't replay stale data.
            while (_producer.Reader.TryRead(out _)) { }
        }

        public void ClearMemory() => _dataSource.ClearMemory();

        public void Dispose()
        {
            StopStreaming();
        }
    }
}


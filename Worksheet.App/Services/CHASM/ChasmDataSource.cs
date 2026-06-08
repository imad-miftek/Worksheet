using System;

namespace Worksheet.Services
{
    // Adapter over Viewport/DataSource buffer.
    public sealed class ChasmDataSource : IChasmDataSource
    {
        private readonly DataSource _dataSource;

        public ChasmDataSource(DataSource dataSource)
        {
            _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        }

        public void Append(EventBatch batch)
        {
            if (batch.Count <= 0)
                return;

            _dataSource.AppendBatch(batch.Channels, batch.Count);
        }

        public void ClearMemory() => _dataSource.ClearMemory();

        public long DataVersion => _dataSource.DataVersion;

        public bool IsStreamingEnabled => _dataSource.IsStreamingEnabled;

        public void SetStreamingEnabled(bool enabled) => _dataSource.SetStreamingEnabled(enabled);

        public ChannelWindowSnapshot GetSnapshot(int featureIndex) => _dataSource.GetSnapshot(featureIndex);

        public MultiChannelWindowSnapshot GetSnapshot(params int[] featureIndices) => _dataSource.GetSnapshot(featureIndices);
    }
}


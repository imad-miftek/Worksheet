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

        public double[] Get(int featureIndex) => _dataSource.Get(featureIndex);

        public int GetVisibleLength(int featureIndex) => _dataSource.GetVisibleLength(featureIndex);

        public void GetVisible(int featureIndex, out double[] values, out int visibleLength) =>
            _dataSource.GetVisible(featureIndex, out values, out visibleLength);
    }
}


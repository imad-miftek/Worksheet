namespace Worksheet.Services
{
    public interface IChasmDataSource : IChannelDataBuffer
    {
        void Append(IEventBatch batch);
        void ClearMemory();
        long DataVersion { get; }

        // Optional passthroughs (handy for UI)
        bool IsStreamingEnabled { get; }
        void SetStreamingEnabled(bool enabled);
    }
}


namespace Worksheet.Services
{
    public interface IChannelDataBuffer
    {
        ChannelWindowSnapshot GetSnapshot(int featureIndex);
        MultiChannelWindowSnapshot GetSnapshot(params int[] featureIndices);
    }
}


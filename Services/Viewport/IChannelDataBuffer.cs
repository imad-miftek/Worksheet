namespace Worksheet.Services
{
    public interface IChannelDataBuffer
    {
        ChannelWindowSnapshot GetSnapshot(int featureIndex);
    }
}


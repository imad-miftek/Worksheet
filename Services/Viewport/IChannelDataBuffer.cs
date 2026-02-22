namespace Worksheet.Services
{
    public interface IChannelDataBuffer
    {
        double[] Get(int featureIndex);
        int GetVisibleLength(int featureIndex);
    }
}


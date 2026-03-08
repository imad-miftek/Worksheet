namespace Worksheet.Services
{
    public interface IChannelDataBuffer
    {
        double[] Get(int featureIndex);
        int GetVisibleLength(int featureIndex);
        void GetVisible(int featureIndex, out double[] values, out int visibleLength);
    }
}


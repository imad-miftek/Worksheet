namespace Worksheet.Services
{
    public interface IEventSignalValues
    {
        int SignalCount { get; }

        double GetSignalValue(int signalIndex);
    }
}

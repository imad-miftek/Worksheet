namespace Worksheet.Services
{
    public interface IOscilloscopeBuffer
    {
        int Count { get; }
        long Version { get; }
        bool TryGetLatest(out AnalogCapture? capture);
    }
}

namespace Worksheet.Services
{
    public interface IAnalogCaptureSink
    {
        void Publish(AnalogCapture capture);
    }
}

using System.Threading.Channels;

namespace Worksheet.Services
{
    public interface IProducer
    {
        ChannelReader<EventBatch> Reader { get; }
        void Start();
        void Stop();
    }
}


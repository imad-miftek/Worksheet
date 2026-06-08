using System.Threading.Channels;

namespace Worksheet.Services
{
    public interface IProducer
    {
        ChannelReader<IEventBatch> Reader { get; }
        void Start();
        void Stop();
    }
}


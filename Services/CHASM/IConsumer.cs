using System.Threading;
using System.Threading.Tasks;

namespace Worksheet.Services
{
    public interface IConsumer
    {
        Task RunAsync(CancellationToken token);
    }
}


using System;

namespace Worksheet.Services
{
    public abstract class UpdateEngine : IDisposable
    {
        public abstract bool IsRunning { get; }
        public abstract void Start();
        public abstract void Stop();
        public abstract void Dispose();
    }
}

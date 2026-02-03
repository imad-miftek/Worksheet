using System;
using System.Threading;

namespace Worksheet.Services
{
    public abstract class PollingEngine : UpdateEngine
    {
        private readonly TimeSpan _interval;
        private Timer? _timer;

        protected PollingEngine(TimeSpan interval)
        {
            _interval = interval;
        }

        public override bool IsRunning => _timer != null;

        public override void Start()
        {
            if (_timer != null)
                return;

            _timer = new Timer(_ => Tick(), null, _interval, _interval);
        }

        public override void Stop()
        {
            _timer?.Dispose();
            _timer = null;
        }

        public override void Dispose()
        {
            Stop();
        }

        protected abstract void Tick();
    }
}

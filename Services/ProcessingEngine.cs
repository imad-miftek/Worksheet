using System;

namespace Worksheet.Services
{
    public class ProcessingEngine : PollingEngine
    {
        private readonly DataStore _dataStore;
        private readonly DataProcessor _dataProcessor;

        public ProcessingEngine(DataStore dataStore, DataProcessor dataProcessor, TimeSpan interval)
            : base(interval)
        {
            _dataStore = dataStore;
            _dataProcessor = dataProcessor;
        }

        protected override void Tick()
        {
            var settings = _dataStore.GetAllSettings();
            foreach (var plotSettings in settings)
            {
                var processed = _dataProcessor.Process(plotSettings);
                _dataStore.SetProcessedData(processed);
            }
        }
    }
}

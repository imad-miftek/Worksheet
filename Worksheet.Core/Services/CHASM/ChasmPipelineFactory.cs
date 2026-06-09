using System;

namespace Worksheet.Services
{
    public static class ChasmPipelineFactory
    {
        public static ChasmPipeline CreateMock(DataSource dataSource, ChasmOptions? options = null)
        {
            if (dataSource == null)
                throw new ArgumentNullException(nameof(dataSource));

            var chasmOptions = options ?? ChasmOptions.Default;
            var chasmDataSource = new ChasmDataSource(dataSource);
            var producer = new MockProducer(chasmOptions);
            var consumer = new ChasmConsumer(producer.Reader, chasmDataSource);
            var chasm = new Chasm(producer, consumer, chasmDataSource);

            return new ChasmPipeline(chasm, chasmDataSource, producer, IngestionPort: null);
        }

        public static ChasmPipeline CreateEventIngress(DataSource dataSource, ChasmOptions? options = null)
        {
            if (dataSource == null)
                throw new ArgumentNullException(nameof(dataSource));

            var chasmOptions = options ?? ChasmOptions.Default;
            var chasmDataSource = new ChasmDataSource(dataSource);
            var producer = new EventProducer(chasmOptions);
            var consumer = new ChasmConsumer(producer.Reader, chasmDataSource);
            var chasm = new Chasm(producer, consumer, chasmDataSource);

            return new ChasmPipeline(chasm, chasmDataSource, producer, producer);
        }
    }
}

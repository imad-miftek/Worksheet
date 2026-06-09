namespace Worksheet.Services
{
public sealed record ChasmPipeline(
    Chasm Chasm,
    ChasmDataSource ChasmDataSource,
    IProducer Producer,
    IEventIngestionPort? IngestionPort);
}

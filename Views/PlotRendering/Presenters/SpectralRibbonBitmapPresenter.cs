using Worksheet.Models.Data;
using Worksheet.Views.PlotRendering.Surfaces;

namespace Worksheet.Views.PlotRendering.Presenters
{
    public sealed class SpectralRibbonBitmapPresenter
    {
        public void Present(SpectralRibbonProcessedData spectralData, DynamicBitmapSurfaceHost surfaceHost)
        {
            if (spectralData.IsEmpty)
            {
                surfaceHost.Clear();
                return;
            }

            surfaceHost.PresentBitmap(spectralData.PixelBuffer, spectralData.ChannelCount, spectralData.Bins);
        }
    }
}

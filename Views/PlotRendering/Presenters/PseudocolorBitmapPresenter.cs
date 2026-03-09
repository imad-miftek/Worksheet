using Worksheet.Models.Data;
using Worksheet.Views.PlotRendering.Surfaces;

namespace Worksheet.Views.PlotRendering.Presenters
{
    public sealed class PseudocolorBitmapPresenter
    {
        public void Present(HeatmapProcessedData heatmapData, DynamicBitmapSurfaceHost surfaceHost)
        {
            if (heatmapData.IsEmpty)
            {
                surfaceHost.Clear();
                return;
            }

            surfaceHost.PresentBitmap(heatmapData.PixelBuffer, heatmapData.Bins, heatmapData.Bins);
        }
    }
}

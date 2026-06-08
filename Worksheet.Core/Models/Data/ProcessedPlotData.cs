using System;

namespace Worksheet.Models.Data
{
    using Worksheet.Models;

    public abstract class ProcessedPlotData
    {
        protected ProcessedPlotData(Guid plotId, PlotType plotType)
        {
            PlotId = plotId;
            PlotType = plotType;
        }

        public Guid PlotId { get; }
        public PlotType PlotType { get; }
    }
}

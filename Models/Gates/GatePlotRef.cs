using System;

namespace Worksheet.Models.Gates
{
    public readonly record struct GatePlotRef(Guid PlotId, PlotType? PlotType = null);
}


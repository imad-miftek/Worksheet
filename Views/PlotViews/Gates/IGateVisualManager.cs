using System;
using Worksheet.Models;

namespace Worksheet.Views.PlotViews.Gates
{
    public interface IGateVisualManager
    {
        void Attach(PlotItem plotItem, Func<int> getBinCount);
        void BeginAddRectangleGate(PlotItem plotItem);
    }

    public interface IGateVisualManagerFactory
    {
        IGateVisualManager Create();
    }

    public sealed class GateVisualManagerFactory : IGateVisualManagerFactory
    {
        public IGateVisualManager Create() => new GateVisualManager();
    }
}

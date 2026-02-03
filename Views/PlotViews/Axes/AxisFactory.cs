using System;
using System.Collections.Generic;
using ScottPlot.WPF;
using Worksheet.Models;
using Worksheet.Views.PlotViews.Histogram;

namespace Worksheet.Views.PlotViews.Axes
{
    public class AxisFactory
    {
        private readonly Dictionary<AxisScaleType, AxisItem> _items;

        public AxisFactory()
            : this(new LinearAxisItem(), new LogarithmicAxisItem())
        {
        }

        public AxisFactory(LinearAxisItem linearAxisItem, LogarithmicAxisItem logarithmicAxisItem)
        {
            _items = new Dictionary<AxisScaleType, AxisItem>
            {
                { linearAxisItem.ScaleType, linearAxisItem },
                { logarithmicAxisItem.ScaleType, logarithmicAxisItem }
            };
        }

        public AxisItem Get(AxisScaleType scaleType)
        {
            if (_items.TryGetValue(scaleType, out var item))
                return item;

            throw new ArgumentOutOfRangeException(nameof(scaleType), scaleType, "Unsupported axis scale type.");
        }

        public void Apply(AxisScaleType scaleType, WpfPlot plot, HistogramBinning binning)
        {
            var item = Get(scaleType);
            item.Apply(plot, binning, AxisOrientation.Bottom);
            item.Apply(plot, binning, AxisOrientation.Left);
        }
    }
}

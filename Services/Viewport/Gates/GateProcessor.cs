using System;
using System.Collections.Generic;
using Worksheet.Models;
using Worksheet.Models.Gates;

namespace Worksheet.Services.Viewport.Gates
{
    public sealed class GateProcessor
    {
        private readonly IChannelDataBuffer _buffer;

        public GateProcessor(IChannelDataBuffer buffer)
        {
            _buffer = buffer;
        }

        public GateResult Process(GateSettings gate, PlotSettings plotSettings, long dataVersion, GateProcessorOptions? options = null)
        {
            options ??= new GateProcessorOptions();

            try
            {
                return plotSettings.PlotType switch
                {
                    PlotType.Histogram => ProcessHistogram(gate, plotSettings, dataVersion, options),
                    PlotType.Pseudocolor => ProcessPseudocolor(gate, plotSettings, dataVersion, options),
                    _ => EmptyResult(gate, plotSettings, dataVersion)
                };
            }
            catch (Exception ex)
            {
                AppLog.Exception(ex, $"GateProcessor.Process gateId={gate.GateId} plotId={plotSettings.Id} plotType={plotSettings.PlotType}");
                return EmptyResult(gate, plotSettings, dataVersion);
            }
        }

        private GateResult ProcessHistogram(GateSettings gate, PlotSettings settings, long dataVersion, GateProcessorOptions options)
        {
            // For now we only support rectangle gates for 1D histograms, using X bounds.
            if (gate.GateType != GateType.Rectangle || gate.Geometry.Type != GateType.Rectangle)
                return EmptyResult(gate, settings, dataVersion);

            int bins = settings.GetBinCount();
            var binGeo = gate.Geometry.ToBinGeometry(bins);

            var mask = BuildRectangleMask1D(bins, binGeo.XMin, binGeo.XMax);

            var values = _buffer.Get(settings.XFeature);
            int total = _buffer.GetVisibleLength(settings.XFeature);
            if (total <= 0)
                return EmptyResultWithTotal(gate, settings, dataVersion, total);

            var (scale, offset, isLog, effMin, effMax) = BuildBinTransform(settings, settings.XAxisScaleType);

            int passed = 0;
            double sum = 0;
            double sumsq = 0;
            List<int>? indices = options.IncludeEventIndices ? new List<int>() : null;

            for (int i = 0; i < total; i++)
            {
                double v = values[i];
                if (!double.IsFinite(v))
                    continue;

                int xBin = ToBin(v, scale, offset, isLog, bins, effMin, effMax);
                if (!mask[xBin])
                    continue;

                passed++;
                sum += v;
                sumsq += v * v;
                indices?.Add(i);
            }

            var stats = Build1DStats(passed, total, sum, sumsq);

            return new GateResult
            {
                GateId = gate.GateId,
                PlotId = settings.Id,
                DataVersion = dataVersion,
                PassedCount = passed,
                TotalCount = total,
                Stats = stats,
                EventIndices = indices?.ToArray(),
            };
        }

        private GateResult ProcessPseudocolor(GateSettings gate, PlotSettings settings, long dataVersion, GateProcessorOptions options)
        {
            int bins = settings.GetBinCount();
            var binGeo = gate.Geometry.ToBinGeometry(bins);
            var mask2d = BuildMask2D(bins, binGeo);

            var xValues = _buffer.Get(settings.XFeature);
            var yValues = _buffer.Get(settings.YFeature);
            int xCount = _buffer.GetVisibleLength(settings.XFeature);
            int yCount = _buffer.GetVisibleLength(settings.YFeature);
            int total = Math.Min(xCount, yCount);
            if (total <= 0)
                return EmptyResultWithTotal(gate, settings, dataVersion, total);

            var (xScale, xOffset, xIsLog, xEffMin, xEffMax) = BuildBinTransform(settings, settings.XAxisScaleType);
            var (yScale, yOffset, yIsLog, yEffMin, yEffMax) = BuildBinTransform(settings, settings.YAxisScaleType);

            int passed = 0;
            double sumX = 0;
            double sumsqX = 0;
            double sumY = 0;
            double sumsqY = 0;
            List<int>? indices = options.IncludeEventIndices ? new List<int>() : null;

            for (int i = 0; i < total; i++)
            {
                double xv = xValues[i];
                double yv = yValues[i];
                if (!double.IsFinite(xv) || !double.IsFinite(yv))
                    continue;

                int xBin = ToBin(xv, xScale, xOffset, xIsLog, bins, xEffMin, xEffMax);
                int yBin = ToBin(yv, yScale, yOffset, yIsLog, bins, yEffMin, yEffMax);

                if (!mask2d[yBin, xBin])
                    continue;

                passed++;
                sumX += xv;
                sumsqX += xv * xv;
                sumY += yv;
                sumsqY += yv * yv;
                indices?.Add(i);
            }

            var stats = Build2DStats(passed, total, sumX, sumsqX, sumY, sumsqY);

            return new GateResult
            {
                GateId = gate.GateId,
                PlotId = settings.Id,
                DataVersion = dataVersion,
                PassedCount = passed,
                TotalCount = total,
                Stats = stats,
                EventIndices = indices?.ToArray(),
            };
        }

        private static GateStatistics Build1DStats(int passed, int total, double sum, double sumsq)
        {
            if (passed <= 0 || total <= 0)
            {
                return new GateStatistics
                {
                    Percent = 0,
                    Total = 0,
                    X = new GateAxisStatistics(0, 0, 0, 0),
                };
            }

            double mean = sum / passed;
            double var = sumsq / passed - mean * mean;
            if (var < 0) var = 0;
            double std = Math.Sqrt(var);
            double cv = mean > 0 ? (std / mean) * 100 : 0;
            double percent = passed * 100.0 / total;

            return new GateStatistics
            {
                Percent = percent,
                Total = passed,
                X = new GateAxisStatistics(mean, std, var, cv),
            };
        }

        private static GateStatistics Build2DStats(int passed, int total, double sumX, double sumsqX, double sumY, double sumsqY)
        {
            if (passed <= 0 || total <= 0)
            {
                return new GateStatistics
                {
                    Percent = 0,
                    Total = 0,
                    X = new GateAxisStatistics(0, 0, 0, 0),
                    Y = new GateAxisStatistics(0, 0, 0, 0),
                };
            }

            double meanX = sumX / passed;
            double varX = sumsqX / passed - meanX * meanX;
            if (varX < 0) varX = 0;
            double stdX = Math.Sqrt(varX);
            double cvX = meanX > 0 ? (stdX / meanX) * 100 : 0;

            double meanY = sumY / passed;
            double varY = sumsqY / passed - meanY * meanY;
            if (varY < 0) varY = 0;
            double stdY = Math.Sqrt(varY);
            double cvY = meanY > 0 ? (stdY / meanY) * 100 : 0;

            double percent = passed * 100.0 / total;

            return new GateStatistics
            {
                Percent = percent,
                Total = passed,
                X = new GateAxisStatistics(meanX, stdX, varX, cvX),
                Y = new GateAxisStatistics(meanY, stdY, varY, cvY),
            };
        }

        private static GateResult EmptyResult(GateSettings gate, PlotSettings settings, long dataVersion) =>
            new()
            {
                GateId = gate.GateId,
                PlotId = settings.Id,
                DataVersion = dataVersion,
                PassedCount = 0,
                TotalCount = 0,
                Stats = new GateStatistics
                {
                    Percent = 0,
                    Total = 0,
                }
            };

        private static GateResult EmptyResultWithTotal(GateSettings gate, PlotSettings settings, long dataVersion, int total) =>
            new()
            {
                GateId = gate.GateId,
                PlotId = settings.Id,
                DataVersion = dataVersion,
                PassedCount = 0,
                TotalCount = total,
                Stats = new GateStatistics
                {
                    Percent = 0,
                    Total = 0,
                    X = new GateAxisStatistics(0, 0, 0, 0),
                    Y = settings.PlotType == PlotType.Pseudocolor ? new GateAxisStatistics(0, 0, 0, 0) : null,
                }
            };

        private static bool[] BuildRectangleMask1D(int bins, double xMin, double xMax)
        {
            var mask = new bool[bins];

            int start = (int)Math.Ceiling(xMin - 0.5);
            int end = (int)Math.Floor(xMax - 0.5);
            start = Math.Clamp(start, 0, bins - 1);
            end = Math.Clamp(end, 0, bins - 1);
            if (start > end)
                return mask;

            for (int x = start; x <= end; x++)
                mask[x] = true;

            return mask;
        }

        private static bool[,] BuildMask2D(int bins, GateBinGeometry geo)
        {
            var mask = new bool[bins, bins]; // [y, x]

            switch (geo.Type)
            {
                case GateType.Rectangle:
                    FillRectangle(mask, bins, geo.XMin, geo.XMax, geo.YMin, geo.YMax);
                    break;
                case GateType.Ellipse:
                    FillEllipse(mask, bins, geo.CenterX, geo.CenterY, geo.RadiusX, geo.RadiusY, geo.AngleDeg);
                    break;
                case GateType.Polygon:
                    FillPolygon(mask, bins, geo.PolygonPoints);
                    break;
            }

            return mask;
        }

        private static void FillRectangle(bool[,] mask, int bins, double xMin, double xMax, double yMin, double yMax)
        {
            int xStart = (int)Math.Ceiling(xMin - 0.5);
            int xEnd = (int)Math.Floor(xMax - 0.5);
            int yStart = (int)Math.Ceiling(yMin - 0.5);
            int yEnd = (int)Math.Floor(yMax - 0.5);

            xStart = Math.Clamp(xStart, 0, bins - 1);
            xEnd = Math.Clamp(xEnd, 0, bins - 1);
            yStart = Math.Clamp(yStart, 0, bins - 1);
            yEnd = Math.Clamp(yEnd, 0, bins - 1);

            if (xStart > xEnd || yStart > yEnd)
                return;

            for (int y = yStart; y <= yEnd; y++)
                for (int x = xStart; x <= xEnd; x++)
                    mask[y, x] = true;
        }

        private static void FillEllipse(bool[,] mask, int bins, double cx, double cy, double rx, double ry, double angleDeg)
        {
            if (rx <= 0 || ry <= 0)
                return;

            double angleRad = angleDeg * Math.PI / 180.0;
            double cos = Math.Cos(-angleRad);
            double sin = Math.Sin(-angleRad);
            double invRx2 = 1.0 / (rx * rx);
            double invRy2 = 1.0 / (ry * ry);

            for (int y = 0; y < bins; y++)
            {
                double py = y + 0.5;
                for (int x = 0; x < bins; x++)
                {
                    double px = x + 0.5;

                    double dx = px - cx;
                    double dy = py - cy;
                    double xRot = dx * cos - dy * sin;
                    double yRot = dx * sin + dy * cos;

                    double v = xRot * xRot * invRx2 + yRot * yRot * invRy2;
                    if (v <= 1.0)
                        mask[y, x] = true;
                }
            }
        }

        private static void FillPolygon(bool[,] mask, int bins, IReadOnlyList<GateBinPoint>? points)
        {
            if (points == null || points.Count < 3)
                return;

            // Ray casting at cell centers (x+0.5, y+0.5)
            for (int y = 0; y < bins; y++)
            {
                double py = y + 0.5;
                for (int x = 0; x < bins; x++)
                {
                    double px = x + 0.5;
                    if (PointInPolygon(px, py, points))
                        mask[y, x] = true;
                }
            }
        }

        private static bool PointInPolygon(double x, double y, IReadOnlyList<GateBinPoint> poly)
        {
            bool inside = false;
            int j = poly.Count - 1;
            for (int i = 0; i < poly.Count; i++)
            {
                double xi = poly[i].X;
                double yi = poly[i].Y;
                double xj = poly[j].X;
                double yj = poly[j].Y;

                bool intersect = ((yi > y) != (yj > y)) &&
                                 (x < (xj - xi) * (y - yi) / (yj - yi + 1e-12) + xi);
                if (intersect)
                    inside = !inside;
                j = i;
            }
            return inside;
        }

        // Keep bin mapping consistent with PlotProcessor.
        private static (double scale, double offset, bool isLog, double effMin, double effMax) BuildBinTransform(
            PlotSettings settings, AxisScaleType scaleType)
        {
            double min = settings.MinValue;
            double max = settings.MaxValue;
            int bins = settings.GetBinCount();

            if (scaleType == AxisScaleType.Logarithmic)
            {
                if (min < 1) min = 1;
                if (max <= min) max = min * 10;
                double minLog = Math.Log10(min);
                double maxLog = Math.Log10(max);
                double scale = bins / (maxLog - minLog);
                double offset = -minLog * scale;
                return (scale, offset, true, min, max);
            }
            else
            {
                if (max <= min) max = min + 1;
                double scale = bins / (max - min);
                double offset = -min * scale;
                return (scale, offset, false, min, max);
            }
        }

        private static int ToBin(double value, double scale, double offset, bool isLog,
            int bins, double effMin, double effMax)
        {
            if (value < effMin) value = effMin;
            else if (value > effMax) value = effMax;

            double pos = isLog ? Math.Log10(value) * scale + offset : value * scale + offset;
            return Math.Clamp((int)pos, 0, bins - 1);
        }
    }
}


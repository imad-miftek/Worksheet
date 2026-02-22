using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using ScottPlot.WPF;
using Worksheet.Models;
using Worksheet.Models.Data;
using Worksheet.Services;
using Worksheet.Views.PlotViews.Axes;
using Worksheet.Views.PlotViews.ContextMenus;

namespace Worksheet.Views.PlotViews
{
    public class PseudocolorPlotView : PlotView
    {
        private const double MinGateSizeBins = 1;
        private const double HandleSizeDip = 6;

        private ScottPlot.Plottables.Heatmap? _heatmap;
        private readonly ScottPlot.IColormap _colormap = CreateColormap();
        private PlotConfigSnapshot? _lastAppliedConfig;
        private double[,]? _emptyIntensities;
        private int _emptyBins;

        private Canvas? _gateLayer;
        private Rectangle? _previewRect;
        private bool _isDrawingGate;
        private bool _isDrawDragActive;
        private Point _dragStartDip;

        private Canvas? _handlesLayer;
        private Rectangle? _handleTL;
        private Rectangle? _handleTR;
        private Rectangle? _handleBL;
        private Rectangle? _handleBR;

        private readonly List<GateRect> _gates = new();
        private int _selectedGateIndex = -1;
        private GateInteractionMode _interactionMode = GateInteractionMode.None;
        private ScottPlot.Coordinates _mouseStartCoord;
        private GateRect _gateStartRect;
        private PlotItem? _interactionPlotItem;
        private bool _gateInteractionsAttached;

        private enum GateInteractionMode
        {
            None,
            Move,
            ResizeTL,
            ResizeTR,
            ResizeBL,
            ResizeBR,
        }

        private readonly record struct GateRect(double XMin, double XMax, double YMin, double YMax, ScottPlot.Plottables.Polygon Plottable)
        {
            public bool Contains(ScottPlot.Coordinates c) =>
                c.X >= XMin && c.X <= XMax && c.Y >= YMin && c.Y <= YMax;
        }

        public PseudocolorPlotView(PseudocolorPlotContextMenu contextMenu, PlotSettings settings)
            : base(contextMenu, settings)
        {
        }

        public override PlotType PlotType => PlotType.Pseudocolor;

        public override void Configure(WpfPlot plot)
        {
            ApplyAxisTicks(plot, resetLimits: true);
            ApplyAxisLabels(plot);
            _lastAppliedConfig = PlotConfigSnapshot.From(Settings);
        }

        public override void Render(WpfPlot plot, ProcessedPlotData data)
        {
            if (data is not HeatmapProcessedData heatmapData)
                return;

            RenderOnce(plot, () =>
            {
                ApplyConfigIfChanged(plot);
                EnsureHeatmap(plot, heatmapData.Data);

                if (_heatmap == null)
                    return;

                if (heatmapData.IsEmpty)
                {
                    // Hide when empty (no events). Do not rely on NaNs for emptiness because all-NaN crashes Update().
                    _heatmap.Opacity = 0;
                    return;
                }

                _heatmap.Opacity = 1;
                _heatmap.Intensities = heatmapData.Data;
                _heatmap.Update();
            });
        }

        public override void Clear(WpfPlot plot)
        {
            RenderOnce(plot, () =>
            {
                int bins = Settings.GetBinCount();
                var empty = GetEmptyIntensities(bins);

                // If the heatmap plottable was never created (or was removed), create a blank one now.
                if (_heatmap != null)
                {
                    bool stillInPlot = plot.Plot.GetPlottables<ScottPlot.Plottables.Heatmap>().Contains(_heatmap);
                    if (!stillInPlot)
                        _heatmap = null;
                }

                if (_heatmap == null)
                    EnsureHeatmap(plot, empty);

                if (_heatmap == null)
                    return;

                _heatmap.Opacity = 0;
                _heatmap.NaNCellColor = ScottPlot.Colors.Transparent;
                _heatmap.Extent = new ScottPlot.CoordinateRect(0, bins, 0, bins);
                _heatmap.Intensities = empty;
                _heatmap.Update();
            });
        }

        private void EnsureHeatmap(WpfPlot plot, double[,] initialData)
        {
            if (_heatmap != null)
                return;

            _heatmap = plot.Plot.Add.Heatmap(initialData);
            _heatmap.Extent = new ScottPlot.CoordinateRect(0, Settings.GetBinCount(), 0, Settings.GetBinCount());
            _heatmap.Colormap = _colormap;
            _heatmap.NaNCellColor = ScottPlot.Colors.Transparent;
            _heatmap.Opacity = 1;

            // Ensure gates always appear above the heatmap (even if heatmap is created later).
            plot.Plot.MoveToBottom(_heatmap);
        }

        public void AttachGateInteractions(PlotItem plotItem)
        {
            if (_gateInteractionsAttached)
                return;

            if (plotItem?.PlotContainer?.DragLayer == null || plotItem.PlotContainer?.Overlay == null)
                return;

            _gateInteractionsAttached = true;
            _interactionPlotItem = plotItem;

            EnsureHandlesLayer(plotItem);

            var dragLayer = plotItem.PlotContainer.DragLayer;
            dragLayer.PreviewMouseLeftButtonDown += DragLayer_PreviewMouseLeftButtonDown;
            dragLayer.PreviewMouseMove += DragLayer_PreviewMouseMove;
            dragLayer.PreviewMouseLeftButtonUp += DragLayer_PreviewMouseLeftButtonUp;

            plotItem.Plot.Plot.RenderManager.RenderFinished += (_, __) =>
            {
                try
                {
                    UpdateHandlePositions(plotItem);
                }
                catch
                {
                }
            };
        }

        internal void BeginAddGateRectangle(PlotItem plotItem)
        {
            if (plotItem?.Plot == null)
                return;

            if (_isDrawingGate)
                return;

            try
            {
                // Ensure ScottPlot has a recent render for pixel<->coordinate conversion.
                plotItem.Plot.Refresh();

                EnsureGateLayer(plotItem);
                if (_gateLayer == null || _previewRect == null)
                    return;

                _isDrawingGate = true;
                _isDrawDragActive = false;
                _gateLayer.IsHitTestVisible = true;
                _gateLayer.Cursor = Cursors.Cross;
                _previewRect.Visibility = Visibility.Visible;
                _previewRect.Width = 0;
                _previewRect.Height = 0;

                _gateLayer.MouseLeftButtonDown += GateLayer_MouseLeftButtonDown;
                _gateLayer.MouseMove += GateLayer_MouseMove;
                _gateLayer.MouseLeftButtonUp += GateLayer_MouseLeftButtonUp;
            }
            catch (Exception ex)
            {
                AppLog.Exception(ex, "PseudocolorPlotView.BeginAddGateRectangle");
                _isDrawingGate = false;
                if (_gateLayer != null)
                {
                    _gateLayer.IsHitTestVisible = false;
                    _gateLayer.Cursor = null;
                }
            }

            void GateLayer_MouseLeftButtonDown(object? sender, MouseButtonEventArgs e)
            {
                if (!_isDrawingGate || _gateLayer == null || _previewRect == null)
                    return;

                _dragStartDip = e.GetPosition(_gateLayer);
                _isDrawDragActive = true;
                _gateLayer.CaptureMouse();
                UpdatePreview(_dragStartDip, _dragStartDip);
                e.Handled = true;
            }

            void GateLayer_MouseMove(object? sender, MouseEventArgs e)
            {
                if (!_isDrawingGate || _gateLayer == null || _previewRect == null)
                    return;

                if (!_gateLayer.IsMouseCaptured)
                    return;

                var pos = e.GetPosition(_gateLayer);
                UpdatePreview(_dragStartDip, pos);
                e.Handled = true;
            }

            void GateLayer_MouseLeftButtonUp(object? sender, MouseButtonEventArgs e)
            {
                if (!_isDrawingGate || _gateLayer == null || _previewRect == null)
                    return;

                // Ignore synthetic/stray mouse-up events (e.g., context menu close)
                // until a real drag starts with mouse capture.
                if (!_isDrawDragActive || !_gateLayer.IsMouseCaptured)
                    return;

                var end = e.GetPosition(_gateLayer);
                _gateLayer.ReleaseMouseCapture();

                try
                {
                    FinalizeRectangleGate(plotItem, _dragStartDip, end);
                }
                catch (Exception ex)
                {
                    AppLog.Exception(ex, "PseudocolorPlotView.FinalizeRectangleGate");
                }
                finally
                {
                    ExitDrawMode();
                }

                e.Handled = true;
            }

            void ExitDrawMode()
            {
                if (_gateLayer == null || _previewRect == null)
                    return;

                _isDrawingGate = false;
                _isDrawDragActive = false;
                _previewRect.Visibility = Visibility.Collapsed;
                _gateLayer.IsHitTestVisible = false;
                _gateLayer.Cursor = null;

                _gateLayer.MouseLeftButtonDown -= GateLayer_MouseLeftButtonDown;
                _gateLayer.MouseMove -= GateLayer_MouseMove;
                _gateLayer.MouseLeftButtonUp -= GateLayer_MouseLeftButtonUp;
            }

            void UpdatePreview(Point start, Point end)
            {
                if (_previewRect == null)
                    return;

                double x = Math.Min(start.X, end.X);
                double y = Math.Min(start.Y, end.Y);
                double w = Math.Abs(end.X - start.X);
                double h = Math.Abs(end.Y - start.Y);

                Canvas.SetLeft(_previewRect, x);
                Canvas.SetTop(_previewRect, y);
                _previewRect.Width = w;
                _previewRect.Height = h;
            }
        }

        private void EnsureGateLayer(PlotItem plotItem)
        {
            if (_gateLayer != null && _previewRect != null)
                return;

            var overlay = plotItem.PlotContainer?.Overlay;
            var host = plotItem.PlotContainer?.Host;
            if (overlay == null || host == null)
                return;

            _gateLayer = new Canvas
            {
                Width = host.ActualWidth > 0 ? host.ActualWidth : host.Width,
                Height = host.ActualHeight > 0 ? host.ActualHeight : host.Height,
                Background = Brushes.Transparent,
                IsHitTestVisible = false,
            };

            Panel.SetZIndex(_gateLayer, 5); // above dragLayer, below thumbs (thumbs use 10)
            overlay.Children.Add(_gateLayer);

            host.SizeChanged += (_, __) =>
            {
                if (_gateLayer == null)
                    return;
                _gateLayer.Width = host.ActualWidth;
                _gateLayer.Height = host.ActualHeight;
            };

            _previewRect = new Rectangle
            {
                Stroke = Brushes.Black,
                StrokeThickness = 1,
                Fill = Brushes.Transparent,
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false,
            };

            _gateLayer.Children.Add(_previewRect);
        }

        private void FinalizeRectangleGate(PlotItem plotItem, Point startDip, Point endDip)
        {
            double wDip = Math.Abs(endDip.X - startDip.X);
            double hDip = Math.Abs(endDip.Y - startDip.Y);
            if (wDip < 4 || hDip < 4)
                return;

            var plot = plotItem.Plot;
            var dpi = VisualTreeHelper.GetDpi(plot);

            double x1px = Math.Min(startDip.X, endDip.X) * dpi.DpiScaleX;
            double y1px = Math.Min(startDip.Y, endDip.Y) * dpi.DpiScaleY;
            double x2px = Math.Max(startDip.X, endDip.X) * dpi.DpiScaleX;
            double y2px = Math.Max(startDip.Y, endDip.Y) * dpi.DpiScaleY;

            var axes = plot.Plot.Axes;
            var c1 = plot.Plot.GetCoordinates((float)x1px, (float)y1px, axes.Bottom, axes.Left);
            var c2 = plot.Plot.GetCoordinates((float)x2px, (float)y2px, axes.Bottom, axes.Left);

            double xMin = Math.Min(c1.X, c2.X);
            double xMax = Math.Max(c1.X, c2.X);
            double yMin = Math.Min(c1.Y, c2.Y);
            double yMax = Math.Max(c1.Y, c2.Y);

            int bins = Settings.GetBinCount();
            xMin = Math.Clamp(xMin, 0, bins);
            xMax = Math.Clamp(xMax, 0, bins);
            yMin = Math.Clamp(yMin, 0, bins);
            yMax = Math.Clamp(yMax, 0, bins);

            var coords = new[]
            {
                new ScottPlot.Coordinates(xMin, yMin),
                new ScottPlot.Coordinates(xMax, yMin),
                new ScottPlot.Coordinates(xMax, yMax),
                new ScottPlot.Coordinates(xMin, yMax),
            };

            var gate = CreateGatePolygon(plot, coords);
            var gateRect = new GateRect(xMin, xMax, yMin, yMax, gate);
            _gates.Add(gateRect);
            plot.Plot.MoveToTop(gate);

            SelectGate(_gates.Count - 1, plotItem);
            plot.Refresh();
        }

        private void EnsureHandlesLayer(PlotItem plotItem)
        {
            if (_handlesLayer != null && _handleTL != null && _handleTR != null && _handleBL != null && _handleBR != null)
                return;

            var overlay = plotItem.PlotContainer?.Overlay;
            var host = plotItem.PlotContainer?.Host;
            if (overlay == null || host == null)
                return;

            _handlesLayer = new Canvas
            {
                Width = host.ActualWidth > 0 ? host.ActualWidth : host.Width,
                Height = host.ActualHeight > 0 ? host.ActualHeight : host.Height,
                Background = null,
                IsHitTestVisible = true,
            };

            Panel.SetZIndex(_handlesLayer, 9);
            overlay.Children.Add(_handlesLayer);

            host.SizeChanged += (_, __) =>
            {
                if (_handlesLayer == null)
                    return;
                _handlesLayer.Width = host.ActualWidth;
                _handlesLayer.Height = host.ActualHeight;
            };

            _handleTL = MakeHandle(Cursors.SizeNWSE);
            _handleTR = MakeHandle(Cursors.SizeNESW);
            _handleBL = MakeHandle(Cursors.SizeNESW);
            _handleBR = MakeHandle(Cursors.SizeNWSE);

            _handleTL.MouseLeftButtonDown += (_, e) => StartResize(plotItem, GateInteractionMode.ResizeTL, e);
            _handleTR.MouseLeftButtonDown += (_, e) => StartResize(plotItem, GateInteractionMode.ResizeTR, e);
            _handleBL.MouseLeftButtonDown += (_, e) => StartResize(plotItem, GateInteractionMode.ResizeBL, e);
            _handleBR.MouseLeftButtonDown += (_, e) => StartResize(plotItem, GateInteractionMode.ResizeBR, e);

            _handlesLayer.Children.Add(_handleTL);
            _handlesLayer.Children.Add(_handleTR);
            _handlesLayer.Children.Add(_handleBL);
            _handlesLayer.Children.Add(_handleBR);

            HideHandles();
        }

        private static Rectangle MakeHandle(Cursor cursor) =>
            new()
            {
                Width = HandleSizeDip,
                Height = HandleSizeDip,
                Fill = Brushes.Black,
                Stroke = Brushes.Black,
                StrokeThickness = 1,
                Visibility = Visibility.Collapsed,
                Cursor = cursor,
                IsHitTestVisible = true,
            };

        private void DragLayer_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_gates.Count == 0)
                return;

            if (_interactionPlotItem == null)
                return;

            try
            {
                var dragLayer = _interactionPlotItem.PlotContainer.DragLayer;
                var coord = MouseToCoord(_interactionPlotItem.Plot, e.GetPosition(dragLayer));
                int hitIndex = HitTestGate(coord);
                if (hitIndex < 0)
                {
                    DeselectGate(_interactionPlotItem);
                    return; // let worksheet drag work
                }

                SelectGate(hitIndex, _interactionPlotItem);
                BeginMove(_interactionPlotItem, coord);
                e.Handled = true;
                dragLayer.CaptureMouse();
            }
            catch (Exception ex)
            {
                AppLog.Exception(ex, "PseudocolorPlotView.DragLayer_PreviewMouseLeftButtonDown");
            }
        }

        private void DragLayer_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_interactionMode == GateInteractionMode.None || _interactionPlotItem == null)
                return;

            var dragLayer = _interactionPlotItem.PlotContainer.DragLayer;
            if (!dragLayer.IsMouseCaptured)
                return;

            try
            {
                var coord = MouseToCoord(_interactionPlotItem.Plot, e.GetPosition(dragLayer));
                if (_interactionMode == GateInteractionMode.Move)
                    ApplyMove(_interactionPlotItem, coord);
                else
                    ApplyResize(_interactionPlotItem, coord);

                e.Handled = true;
            }
            catch (Exception ex)
            {
                AppLog.Exception(ex, "PseudocolorPlotView.DragLayer_PreviewMouseMove");
            }
        }

        private void DragLayer_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_interactionPlotItem == null)
                return;

            var dragLayer = _interactionPlotItem.PlotContainer.DragLayer;
            if (!dragLayer.IsMouseCaptured)
                return;

            dragLayer.ReleaseMouseCapture();
            _interactionMode = GateInteractionMode.None;
            e.Handled = true;
        }

        private void StartResize(PlotItem plotItem, GateInteractionMode mode, MouseButtonEventArgs e)
        {
            if (_selectedGateIndex < 0 || _selectedGateIndex >= _gates.Count)
                return;

            try
            {
                _interactionPlotItem = plotItem;
                _interactionMode = mode;
                var dragLayer = plotItem.PlotContainer.DragLayer;
                var coord = MouseToCoord(plotItem.Plot, e.GetPosition(dragLayer));
                _mouseStartCoord = coord;
                _gateStartRect = _gates[_selectedGateIndex];

                e.Handled = true;
                dragLayer.CaptureMouse();
            }
            catch (Exception ex)
            {
                AppLog.Exception(ex, "PseudocolorPlotView.StartResize");
            }
        }

        private void BeginMove(PlotItem plotItem, ScottPlot.Coordinates mouseCoord)
        {
            _interactionPlotItem = plotItem;
            _interactionMode = GateInteractionMode.Move;
            _mouseStartCoord = mouseCoord;
            _gateStartRect = _gates[_selectedGateIndex];
        }

        private int HitTestGate(ScottPlot.Coordinates c)
        {
            for (int i = _gates.Count - 1; i >= 0; i--)
                if (_gates[i].Contains(c))
                    return i;
            return -1;
        }

        private void SelectGate(int index, PlotItem plotItem)
        {
            _selectedGateIndex = index;
            _interactionPlotItem = plotItem;
            ShowHandles();
            UpdateHandlePositions(plotItem);
        }

        private void DeselectGate(PlotItem plotItem)
        {
            _selectedGateIndex = -1;
            _interactionMode = GateInteractionMode.None;
            HideHandles();
            plotItem.Plot.Refresh();
        }

        private void ShowHandles()
        {
            if (_handleTL == null || _handleTR == null || _handleBL == null || _handleBR == null)
                return;
            _handleTL.Visibility = Visibility.Visible;
            _handleTR.Visibility = Visibility.Visible;
            _handleBL.Visibility = Visibility.Visible;
            _handleBR.Visibility = Visibility.Visible;
        }

        private void HideHandles()
        {
            if (_handleTL == null || _handleTR == null || _handleBL == null || _handleBR == null)
                return;
            _handleTL.Visibility = Visibility.Collapsed;
            _handleTR.Visibility = Visibility.Collapsed;
            _handleBL.Visibility = Visibility.Collapsed;
            _handleBR.Visibility = Visibility.Collapsed;
        }

        private void UpdateHandlePositions(PlotItem plotItem)
        {
            if (_selectedGateIndex < 0 || _selectedGateIndex >= _gates.Count)
                return;

            if (_handleTL == null || _handleTR == null || _handleBL == null || _handleBR == null)
                return;

            var gate = _gates[_selectedGateIndex];

            PlaceHandle(plotItem, _handleTL, gate.XMin, gate.YMax);
            PlaceHandle(plotItem, _handleTR, gate.XMax, gate.YMax);
            PlaceHandle(plotItem, _handleBL, gate.XMin, gate.YMin);
            PlaceHandle(plotItem, _handleBR, gate.XMax, gate.YMin);
        }

        private static void PlaceHandle(PlotItem plotItem, Rectangle handle, double x, double y)
        {
            var axes = plotItem.Plot.Plot.Axes;
            var px = plotItem.Plot.Plot.GetPixel(new ScottPlot.Coordinates(x, y), axes.Bottom, axes.Left);

            var dpi = VisualTreeHelper.GetDpi(plotItem.Plot);
            double dipX = px.X / dpi.DpiScaleX;
            double dipY = px.Y / dpi.DpiScaleY;

            Canvas.SetLeft(handle, dipX - HandleSizeDip / 2);
            Canvas.SetTop(handle, dipY - HandleSizeDip / 2);
        }

        private static ScottPlot.Coordinates MouseToCoord(WpfPlot plot, Point mouseDip)
        {
            var dpi = VisualTreeHelper.GetDpi(plot);
            float pxX = (float)(mouseDip.X * dpi.DpiScaleX);
            float pxY = (float)(mouseDip.Y * dpi.DpiScaleY);
            var axes = plot.Plot.Axes;
            return plot.Plot.GetCoordinates(pxX, pxY, axes.Bottom, axes.Left);
        }

        private void ApplyMove(PlotItem plotItem, ScottPlot.Coordinates current)
        {
            if (_selectedGateIndex < 0 || _selectedGateIndex >= _gates.Count)
                return;

            double dx = current.X - _mouseStartCoord.X;
            double dy = current.Y - _mouseStartCoord.Y;

            var r = _gateStartRect;
            double xMin = r.XMin + dx;
            double xMax = r.XMax + dx;
            double yMin = r.YMin + dy;
            double yMax = r.YMax + dy;

            ClampRect(ref xMin, ref xMax, ref yMin, ref yMax);
            ReplaceGate(plotItem, _selectedGateIndex, xMin, xMax, yMin, yMax);
        }

        private void ApplyResize(PlotItem plotItem, ScottPlot.Coordinates current)
        {
            if (_selectedGateIndex < 0 || _selectedGateIndex >= _gates.Count)
                return;

            var r = _gateStartRect;
            double xMin = r.XMin;
            double xMax = r.XMax;
            double yMin = r.YMin;
            double yMax = r.YMax;

            switch (_interactionMode)
            {
                case GateInteractionMode.ResizeTL:
                    xMin = current.X;
                    yMax = current.Y;
                    break;
                case GateInteractionMode.ResizeTR:
                    xMax = current.X;
                    yMax = current.Y;
                    break;
                case GateInteractionMode.ResizeBL:
                    xMin = current.X;
                    yMin = current.Y;
                    break;
                case GateInteractionMode.ResizeBR:
                    xMax = current.X;
                    yMin = current.Y;
                    break;
                default:
                    return;
            }

            NormalizeRect(ref xMin, ref xMax, ref yMin, ref yMax);
            ClampRect(ref xMin, ref xMax, ref yMin, ref yMax);
            ReplaceGate(plotItem, _selectedGateIndex, xMin, xMax, yMin, yMax);
        }

        private void ReplaceGate(PlotItem plotItem, int index, double xMin, double xMax, double yMin, double yMax)
        {
            var old = _gates[index];
            try
            {
                plotItem.Plot.Plot.Remove(old.Plottable);
            }
            catch
            {
            }

            var coords = new[]
            {
                new ScottPlot.Coordinates(xMin, yMin),
                new ScottPlot.Coordinates(xMax, yMin),
                new ScottPlot.Coordinates(xMax, yMax),
                new ScottPlot.Coordinates(xMin, yMax),
            };

            var gate = CreateGatePolygon(plotItem.Plot, coords);
            _gates[index] = new GateRect(xMin, xMax, yMin, yMax, gate);
            plotItem.Plot.Plot.MoveToTop(gate);

            UpdateHandlePositions(plotItem);
            plotItem.Plot.Refresh();
        }

        private static void NormalizeRect(ref double xMin, ref double xMax, ref double yMin, ref double yMax)
        {
            if (xMin > xMax)
                (xMin, xMax) = (xMax, xMin);
            if (yMin > yMax)
                (yMin, yMax) = (yMax, yMin);
        }

        private void ClampRect(ref double xMin, ref double xMax, ref double yMin, ref double yMax)
        {
            int bins = Settings.GetBinCount();

            NormalizeRect(ref xMin, ref xMax, ref yMin, ref yMax);

            xMin = Math.Clamp(xMin, 0, bins);
            xMax = Math.Clamp(xMax, 0, bins);
            yMin = Math.Clamp(yMin, 0, bins);
            yMax = Math.Clamp(yMax, 0, bins);

            if (xMax - xMin < MinGateSizeBins)
                xMax = Math.Clamp(xMin + MinGateSizeBins, 0, bins);
            if (yMax - yMin < MinGateSizeBins)
                yMax = Math.Clamp(yMin + MinGateSizeBins, 0, bins);
        }

        private static ScottPlot.Plottables.Polygon CreateGatePolygon(WpfPlot plot, ScottPlot.Coordinates[] coords)
        {
            ScottPlot.Plottables.Polygon gate;
            try
            {
                gate = plot.Plot.Add.Polygon(coords);
            }
            catch
            {
                gate = new ScottPlot.Plottables.Polygon(coords);
                plot.Plot.PlottableList.Add(gate);
            }

            gate.LineWidth = 2;
            gate.LineColor = ScottPlot.Colors.Black;
            gate.FillColor = ScottPlot.Colors.Transparent;
            return gate;
        }

        private double[,] GetEmptyIntensities(int bins)
        {
            if (_emptyIntensities != null && _emptyBins == bins)
                return _emptyIntensities;

            var empty = new double[bins, bins];

            _emptyIntensities = empty;
            _emptyBins = bins;
            return empty;
        }

        private void ApplyConfigIfChanged(WpfPlot plot)
        {
            var current = PlotConfigSnapshot.From(Settings);
            if (_lastAppliedConfig.HasValue && _lastAppliedConfig.Value.Equals(current))
                return;

            ApplyAxisTicks(plot, resetLimits: false);
            ApplyAxisLabels(plot);
            if (_heatmap != null)
                _heatmap.Extent = new ScottPlot.CoordinateRect(0, Settings.GetBinCount(), 0, Settings.GetBinCount());

            _lastAppliedConfig = current;
        }

        private void ApplyAxisLabels(WpfPlot plot)
        {
            plot.Plot.XLabel(GetFeatureLabel(Settings.XFeature));
            plot.Plot.YLabel(GetFeatureLabel(Settings.YFeature));
        }

        private static string GetFeatureLabel(int featureIndex)
        {
            if (FeatureSelectionStrategy.TryGetChannelWavelength(featureIndex, out var wavelength))
                return wavelength;

            return $"Channel {featureIndex + 1}";
        }

        private void ApplyAxisTicks(WpfPlot plot, bool resetLimits)
        {
            ApplyAxisTicks(plot, AxisOrientation.Bottom, Settings.XAxisScaleType, Settings, resetLimits);
            ApplyAxisTicks(plot, AxisOrientation.Left, Settings.YAxisScaleType, Settings, resetLimits);
        }

        private static void ApplyAxisTicks(
            WpfPlot plot,
            AxisOrientation orientation,
            AxisScaleType scaleType,
            PlotSettings settings,
            bool resetLimits)
        {
            switch (scaleType)
            {
                case AxisScaleType.Linear:
                    ApplyLinearTicks(plot, orientation, settings, resetLimits);
                    break;
                case AxisScaleType.Logarithmic:
                    ApplyLogarithmicTicks(plot, orientation, settings, resetLimits);
                    break;
                default:
                    break;
            }
        }

        private static void ApplyLinearTicks(WpfPlot plot, AxisOrientation orientation, PlotSettings settings, bool resetLimits)
        {
            var tickGen = LinearAxisItem.CreateDataTickGenerator(settings);
            if (orientation == AxisOrientation.Bottom)
            {
                plot.Plot.Axes.Bottom.TickGenerator = tickGen;
                if (resetLimits)
                    plot.Plot.Axes.SetLimitsX(0, settings.GetBinCount());
            }
            else if (orientation == AxisOrientation.Left)
            {
                plot.Plot.Axes.Left.TickGenerator = tickGen;
                if (resetLimits)
                    plot.Plot.Axes.SetLimitsY(0, settings.GetBinCount());
            }

            plot.Plot.Grid.MinorLineColor = ScottPlot.Colors.Black.WithOpacity(.05);
            plot.Plot.Grid.MinorLineWidth = 1;
        }

        private static void ApplyLogarithmicTicks(WpfPlot plot, AxisOrientation orientation, PlotSettings settings, bool resetLimits)
        {
            var tickGen = LogarithmicAxisItem.CreateDataTickGenerator(settings);
            if (orientation == AxisOrientation.Bottom)
            {
                plot.Plot.Axes.Bottom.TickGenerator = tickGen;
                if (resetLimits)
                    plot.Plot.Axes.SetLimitsX(0, settings.GetBinCount());
            }
            else if (orientation == AxisOrientation.Left)
            {
                plot.Plot.Axes.Left.TickGenerator = tickGen;
                if (resetLimits)
                    plot.Plot.Axes.SetLimitsY(0, settings.GetBinCount());
            }

            plot.Plot.Grid.MajorLineColor = ScottPlot.Colors.Black.WithOpacity(.15);
            plot.Plot.Grid.MinorLineColor = ScottPlot.Colors.Black.WithOpacity(.05);
            plot.Plot.Grid.MinorLineWidth = 1;
        }

        private static ScottPlot.IColormap CreateColormap()
        {
            try
            {
                return new ScottPlot.Colormaps.Turbo();
            }
            catch
            {
                return new ScottPlot.Colormaps.Viridis();
            }
        }

        private readonly record struct PlotConfigSnapshot(
            int BinCount,
            AxisScaleType XAxisScaleType,
            AxisScaleType YAxisScaleType,
            double MinValue,
            double MaxValue)
        {
            public static PlotConfigSnapshot From(PlotSettings settings) =>
                new(
                    settings.GetBinCount(),
                    settings.XAxisScaleType,
                    settings.YAxisScaleType,
                    settings.MinValue,
                    settings.MaxValue);
        }
    }
}

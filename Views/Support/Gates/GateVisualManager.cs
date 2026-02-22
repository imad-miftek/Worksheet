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
using Worksheet.Services;
using Worksheet.Views.PlotViews.Gates;
using Worksheet.Models.Gates;

namespace Worksheet.Views.Support.Gates
{
    public sealed class GateVisualManager
    {
        private const double MinGateSizeBins = 1;
        private const double HandleSizeDip = 6;
        private const double PolygonCloseThresholdDip = 16;
        private const double PolygonCloseCueSizeDip = 8;

        private readonly List<GateBase> _gates = new();

        private Func<int> _getBinCount = static () => 256;
        private Func<Guid> _getPlotId = static () => Guid.Empty;
        private Func<PlotType> _getPlotType = static () => PlotType.Pseudocolor;
        private Action<GateSettings>? _gateSettingsSink;
        private Action<Guid>? _gateRemovedSink;
        private PlotItem? _interactionPlotItem;
        private bool _gateInteractionsAttached;

        private Canvas? _gateLayer;
        private Rectangle? _previewRect;
        private Ellipse? _previewEllipse;
        private Polyline? _previewPolygon;
        private Rectangle? _polygonCloseCue;
        private bool _isDrawingGate;
        private bool _isDrawDragActive;
        private Point _dragStartDip;
        private GateType _createGateType = GateType.Rectangle;

        private Canvas? _handlesLayer;
        private Rectangle? _handleTL;
        private Rectangle? _handleTR;
        private Rectangle? _handleBL;
        private Rectangle? _handleBR;
        private Rectangle? _ellipseBoundsRect;
        private readonly List<Rectangle> _polygonVertexHandles = new();
        private int _activePolygonVertexIndex = -1;

        // Debug mask overlay removed (gate stats now align with visuals without needing a footprint overlay).

        private int _selectedGateIndex = -1;
        private GateInteractionMode _interactionMode = GateInteractionMode.None;
        private ScottPlot.Coordinates _mouseStartCoord;
        private GateBounds _gateStartBounds;
        private bool _gateInteractionDirty;
        private enum GateInteractionMode
        {
            None,
            Move,
            ResizeTL,
            ResizeTR,
            ResizeBL,
            ResizeBR,
            VertexDrag,
        }

        private readonly record struct GateBounds(double XMin, double XMax, double YMin, double YMax);

        public void Attach(
            PlotItem plotItem,
            Func<int> getBinCount,
            Func<Guid>? getPlotId = null,
            Func<PlotType>? getPlotType = null,
            Action<GateSettings>? gateSettingsSink = null,
            Action<Guid>? gateRemovedSink = null)
        {
            if (_gateInteractionsAttached)
                return;

            if (plotItem?.PlotContainer?.DragLayer == null || plotItem.PlotContainer?.Overlay == null)
                return;

            _gateInteractionsAttached = true;
            _interactionPlotItem = plotItem;
            _getBinCount = getBinCount ?? _getBinCount;
            _getPlotId = getPlotId ?? _getPlotId;
            _getPlotType = getPlotType ?? _getPlotType;
            _gateSettingsSink = gateSettingsSink;
            _gateRemovedSink = gateRemovedSink;

            EnsureHandlesLayer(plotItem);

            var dragLayer = plotItem.PlotContainer.DragLayer;
            dragLayer.PreviewMouseLeftButtonDown += DragLayer_PreviewMouseLeftButtonDown;
            dragLayer.PreviewMouseMove += DragLayer_PreviewMouseMove;
            dragLayer.PreviewMouseLeftButtonUp += DragLayer_PreviewMouseLeftButtonUp;
            dragLayer.LostMouseCapture += DragLayer_LostMouseCapture;

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

        public bool HasSelectedGate =>
            _selectedGateIndex >= 0 && _selectedGateIndex < _gates.Count;

        public bool RemoveSelectedGate(PlotItem plotItem)
        {
            if (!HasSelectedGate)
                return false;

            int idx = _selectedGateIndex;
            var gate = _gates[idx];

            try
            {
                if (gate.Plottable != null)
                    plotItem.Plot.Plot.Remove(gate.Plottable);
            }
            catch
            {
            }

            try
            {
                if (gate.LabelPlottable != null)
                    plotItem.Plot.Plot.Remove(gate.LabelPlottable);
            }
            catch
            {
            }

            _gates.RemoveAt(idx);
            _selectedGateIndex = -1;
            _interactionMode = GateInteractionMode.None;
            HideHandles();
            _gateInteractionDirty = false;

            try
            {
                _gateRemovedSink?.Invoke(gate.GateId);
            }
            catch (Exception ex)
            {
                AppLog.Exception(ex, "GateVisualManager.RemoveSelectedGate");
            }

            plotItem.Plot.Refresh();
            return true;
        }

        public void BeginAddRectangleGate(PlotItem plotItem)
        {
            BeginAddGate(plotItem, GateType.Rectangle);
        }

        public void BeginAddEllipseGate(PlotItem plotItem)
        {
            BeginAddGate(plotItem, GateType.Ellipse);
        }

        public void BeginAddPolygonGate(PlotItem plotItem)
        {
            BeginAddGate(plotItem, GateType.Polygon);
        }

        private void BeginAddGate(PlotItem plotItem, GateType gateType)
        {
            if (plotItem?.Plot == null)
                return;

            if (_isDrawingGate)
                return;

            var polygonVerticesDip = new List<Point>();

            try
            {
                _createGateType = gateType;
                plotItem.Plot.Refresh();

                EnsureGateLayer(plotItem);
                if (_gateLayer == null || _previewRect == null || _previewEllipse == null || _previewPolygon == null || _polygonCloseCue == null)
                    return;

                _isDrawingGate = true;
                _isDrawDragActive = false;
                _gateLayer.IsHitTestVisible = true;
                _gateLayer.Cursor = Cursors.Cross;
                _previewRect.Visibility = gateType == GateType.Rectangle ? Visibility.Visible : Visibility.Collapsed;
                _previewEllipse.Visibility = gateType == GateType.Ellipse ? Visibility.Visible : Visibility.Collapsed;
                _previewPolygon.Visibility = gateType == GateType.Polygon ? Visibility.Visible : Visibility.Collapsed;
                _polygonCloseCue.Visibility = Visibility.Collapsed;
                _previewRect.Width = 0;
                _previewRect.Height = 0;
                _previewEllipse.Width = 0;
                _previewEllipse.Height = 0;
                _previewPolygon.Points.Clear();

                _gateLayer.MouseLeftButtonDown += GateLayer_MouseLeftButtonDown;
                _gateLayer.MouseMove += GateLayer_MouseMove;
                _gateLayer.MouseLeftButtonUp += GateLayer_MouseLeftButtonUp;
                _gateLayer.MouseRightButtonDown += GateLayer_MouseRightButtonDown;
            }
            catch (Exception ex)
            {
                AppLog.Exception(ex, "GateVisualManager.BeginAddRectangleGate");
                _isDrawingGate = false;
                if (_gateLayer != null)
                {
                    _gateLayer.IsHitTestVisible = false;
                    _gateLayer.Cursor = null;
                }
            }

            void GateLayer_MouseLeftButtonDown(object? sender, MouseButtonEventArgs e)
            {
                if (!_isDrawingGate || _gateLayer == null || _previewRect == null || _previewEllipse == null || _previewPolygon == null || _polygonCloseCue == null)
                    return;

                if (_createGateType == GateType.Polygon)
                {
                    var pos = e.GetPosition(_gateLayer);

                    if (polygonVerticesDip.Count >= 3)
                    {
                        var first = polygonVerticesDip[0];
                        double dx = pos.X - first.X;
                        double dy = pos.Y - first.Y;
                        if (Math.Sqrt(dx * dx + dy * dy) <= PolygonCloseThresholdDip)
                        {
                            try
                            {
                                FinalizePolygonGate(plotItem, polygonVerticesDip);
                            }
                            catch (Exception ex)
                            {
                                AppLog.Exception(ex, "GateVisualManager.FinalizePolygonGate");
                            }
                            finally
                            {
                                ExitDrawMode();
                            }

                            e.Handled = true;
                            return;
                        }
                    }

                    polygonVerticesDip.Add(pos);
                    UpdatePolygonPreviewFixed();
                    e.Handled = true;
                    return;
                }

                _dragStartDip = e.GetPosition(_gateLayer);
                _isDrawDragActive = true;
                _gateLayer.CaptureMouse();
                UpdatePreview(_dragStartDip, _dragStartDip);
                e.Handled = true;
            }

            void GateLayer_MouseMove(object? sender, MouseEventArgs e)
            {
                if (!_isDrawingGate || _gateLayer == null || _previewRect == null || _previewEllipse == null || _previewPolygon == null || _polygonCloseCue == null)
                    return;

                if (_createGateType == GateType.Polygon)
                {
                    if (polygonVerticesDip.Count > 0)
                    {
                        var hoverPos = e.GetPosition(_gateLayer);
                        UpdatePolygonPreviewWithHover(hoverPos);
                    }
                    e.Handled = true;
                    return;
                }

                if (!_gateLayer.IsMouseCaptured)
                    return;

                var dragPos = e.GetPosition(_gateLayer);
                UpdatePreview(_dragStartDip, dragPos);
                e.Handled = true;
            }

            void GateLayer_MouseLeftButtonUp(object? sender, MouseButtonEventArgs e)
            {
                if (!_isDrawingGate || _gateLayer == null || _previewRect == null || _previewEllipse == null || _previewPolygon == null || _polygonCloseCue == null)
                    return;

                if (_createGateType == GateType.Polygon)
                {
                    e.Handled = true;
                    return;
                }

                if (!_isDrawDragActive || !_gateLayer.IsMouseCaptured)
                    return;

                var end = e.GetPosition(_gateLayer);
                _gateLayer.ReleaseMouseCapture();

                try
                {
                    FinalizeGate(plotItem, _dragStartDip, end, _createGateType);
                }
                catch (Exception ex)
                {
                    AppLog.Exception(ex, $"GateVisualManager.FinalizeGate type={_createGateType}");
                }
                finally
                {
                    ExitDrawMode();
                }

                e.Handled = true;
            }

            void GateLayer_MouseRightButtonDown(object? sender, MouseButtonEventArgs e)
            {
                if (!_isDrawingGate || _gateLayer == null || _previewRect == null || _previewEllipse == null || _previewPolygon == null || _polygonCloseCue == null)
                    return;

                if (_createGateType != GateType.Polygon)
                    return;

                try
                {
                    if (polygonVerticesDip.Count >= 3)
                        FinalizePolygonGate(plotItem, polygonVerticesDip);
                    else
                        polygonVerticesDip.Clear();
                }
                catch (Exception ex)
                {
                    AppLog.Exception(ex, "GateVisualManager.FinalizePolygonGate");
                }
                finally
                {
                    ExitDrawMode();
                }

                e.Handled = true;
            }

            void ExitDrawMode()
            {
                if (_gateLayer == null || _previewRect == null || _previewEllipse == null || _previewPolygon == null || _polygonCloseCue == null)
                    return;

                _isDrawingGate = false;
                _isDrawDragActive = false;
                _previewRect.Visibility = Visibility.Collapsed;
                _previewEllipse.Visibility = Visibility.Collapsed;
                _previewPolygon.Visibility = Visibility.Collapsed;
                _polygonCloseCue.Visibility = Visibility.Collapsed;
                _previewPolygon.Points.Clear();
                _gateLayer.IsHitTestVisible = false;
                _gateLayer.Cursor = null;

                _gateLayer.MouseLeftButtonDown -= GateLayer_MouseLeftButtonDown;
                _gateLayer.MouseMove -= GateLayer_MouseMove;
                _gateLayer.MouseLeftButtonUp -= GateLayer_MouseLeftButtonUp;
                _gateLayer.MouseRightButtonDown -= GateLayer_MouseRightButtonDown;
            }

            void UpdatePreview(Point start, Point end)
            {
                if (_previewRect == null || _previewEllipse == null)
                    return;

                double x = Math.Min(start.X, end.X);
                double y = Math.Min(start.Y, end.Y);
                double w = Math.Abs(end.X - start.X);
                double h = Math.Abs(end.Y - start.Y);

                Canvas.SetLeft(_previewRect, x);
                Canvas.SetTop(_previewRect, y);
                _previewRect.Width = w;
                _previewRect.Height = h;

                Canvas.SetLeft(_previewEllipse, x);
                Canvas.SetTop(_previewEllipse, y);
                _previewEllipse.Width = w;
                _previewEllipse.Height = h;
            }

            void UpdatePolygonPreviewWithHover(Point hover)
            {
                if (_previewPolygon == null)
                    return;

                var pts = new PointCollection();
                foreach (var p in polygonVerticesDip)
                    pts.Add(p);

                if (polygonVerticesDip.Count > 0)
                    pts.Add(hover);

                _previewPolygon.Points = pts;
                UpdatePolygonCloseCue(hover);
            }

            void UpdatePolygonPreviewFixed()
            {
                if (_previewPolygon == null)
                    return;

                var pts = new PointCollection();
                foreach (var p in polygonVerticesDip)
                    pts.Add(p);
                _previewPolygon.Points = pts;
                UpdatePolygonCloseCue(polygonVerticesDip.Count > 0 ? polygonVerticesDip[^1] : default);
            }

            void UpdatePolygonCloseCue(Point hover)
            {
                if (_polygonCloseCue == null || polygonVerticesDip.Count == 0)
                    return;

                Point first = polygonVerticesDip[0];
                bool canClose = polygonVerticesDip.Count >= 3;
                double dx = hover.X - first.X;
                double dy = hover.Y - first.Y;
                bool inVicinity = canClose && Math.Sqrt(dx * dx + dy * dy) <= PolygonCloseThresholdDip;

                if (!inVicinity)
                {
                    _polygonCloseCue.Visibility = Visibility.Collapsed;
                    return;
                }

                double halfCue = PolygonCloseCueSizeDip / 2.0;
                Canvas.SetLeft(_polygonCloseCue, first.X - halfCue);
                Canvas.SetTop(_polygonCloseCue, first.Y - halfCue);
                _polygonCloseCue.Width = PolygonCloseCueSizeDip;
                _polygonCloseCue.Height = PolygonCloseCueSizeDip;
                _polygonCloseCue.Visibility = Visibility.Visible;
            }
        }

        private void EnsureGateLayer(PlotItem plotItem)
        {
            if (_gateLayer != null && _previewRect != null && _previewEllipse != null && _previewPolygon != null && _polygonCloseCue != null)
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

            Panel.SetZIndex(_gateLayer, 5);
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

            _previewEllipse = new Ellipse
            {
                Stroke = Brushes.Black,
                StrokeThickness = 1,
                Fill = Brushes.Transparent,
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false,
            };

            _previewPolygon = new Polyline
            {
                Stroke = Brushes.Black,
                StrokeThickness = 1,
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false,
            };

            _polygonCloseCue = new Rectangle
            {
                Stroke = Brushes.Orange,
                StrokeThickness = 2,
                Fill = Brushes.Black,
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false,
            };

            _gateLayer.Children.Add(_previewRect);
            _gateLayer.Children.Add(_previewEllipse);
            _gateLayer.Children.Add(_previewPolygon);
            _gateLayer.Children.Add(_polygonCloseCue);
        }

        private void FinalizeGate(PlotItem plotItem, Point startDip, Point endDip, GateType gateType)
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

            int bins = GetBinCount();
            ClampRect(ref xMin, ref xMax, ref yMin, ref yMax, bins);

            var gateId = Guid.NewGuid();
            string gateName = GenerateNextGateName(_gates.Select(g => g.Name));
            GateBase gate = gateType switch
            {
                GateType.Ellipse => new EllipseGate(gateId, gateName, xMin, xMax, yMin, yMax, GateStyle.DefaultRectangle),
                GateType.Polygon => new PolygonGate(
                    gateId,
                    gateName,
                    new[]
                    {
                        new ScottPlot.Coordinates(xMin, yMin),
                        new ScottPlot.Coordinates(xMax, yMin),
                        new ScottPlot.Coordinates(xMax, yMax),
                        new ScottPlot.Coordinates(xMin, yMax),
                    },
                    GateStyle.DefaultRectangle),
                _ => new RectangleGate(gateId, gateName, xMin, xMax, yMin, yMax, GateStyle.DefaultRectangle),
            };
            gate.RebuildPlottable(plot);
            if (gate.Plottable != null)
                plot.Plot.MoveToTop(gate.Plottable);
            if (gate.LabelPlottable != null)
                plot.Plot.MoveToTop(gate.LabelPlottable);

            _gates.Add(gate);
            SelectGate(_gates.Count - 1, plotItem);
            // debug mask overlay removed
            EmitGateUpsert(gate);
            plot.Refresh();
        }

        private void FinalizePolygonGate(PlotItem plotItem, IReadOnlyList<Point> verticesDip)
        {
            if (verticesDip == null || verticesDip.Count < 3)
                return;

            var plot = plotItem.Plot;
            var dpi = VisualTreeHelper.GetDpi(plot);
            var axes = plot.Plot.Axes;
            int bins = GetBinCount();

            var points = new List<ScottPlot.Coordinates>(verticesDip.Count);
            foreach (var p in verticesDip)
            {
                float pxX = (float)(p.X * dpi.DpiScaleX);
                float pxY = (float)(p.Y * dpi.DpiScaleY);
                var c = plot.Plot.GetCoordinates(pxX, pxY, axes.Bottom, axes.Left);
                points.Add(new ScottPlot.Coordinates(
                    Math.Clamp(c.X, 0, bins),
                    Math.Clamp(c.Y, 0, bins)));
            }

            if (points.Count < 3)
                return;

            var gateId = Guid.NewGuid();
            string gateName = GenerateNextGateName(_gates.Select(g => g.Name));
            var gate = new PolygonGate(gateId, gateName, points, GateStyle.DefaultRectangle);

            gate.RebuildPlottable(plot);
            if (gate.Plottable != null)
                plot.Plot.MoveToTop(gate.Plottable);
            if (gate.LabelPlottable != null)
                plot.Plot.MoveToTop(gate.LabelPlottable);

            _gates.Add(gate);
            SelectGate(_gates.Count - 1, plotItem);
            EmitGateUpsert(gate);
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

            _ellipseBoundsRect = new Rectangle
            {
                Stroke = Brushes.Black,
                StrokeThickness = 1,
                Fill = Brushes.Transparent,
                StrokeDashArray = new DoubleCollection { 4, 3 },
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false,
            };
            Panel.SetZIndex(_ellipseBoundsRect, 0);
            _handlesLayer.Children.Add(_ellipseBoundsRect);

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
            if (_gates.Count == 0 || _interactionPlotItem == null)
                return;

            try
            {
                var dragLayer = _interactionPlotItem.PlotContainer.DragLayer;
                var coord = MouseToCoord(_interactionPlotItem.Plot, e.GetPosition(dragLayer));
                int hitIndex = HitTestGate(coord);
                if (hitIndex < 0)
                {
                    DeselectGate(_interactionPlotItem);
                    return;
                }

                SelectGate(hitIndex, _interactionPlotItem);
                // debug mask overlay removed
                BeginMove(_interactionPlotItem, coord);
                e.Handled = true;
                dragLayer.CaptureMouse();
            }
            catch (Exception ex)
            {
                AppLog.Exception(ex, "GateVisualManager.DragLayer_PreviewMouseLeftButtonDown");
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
                else if (_interactionMode == GateInteractionMode.VertexDrag)
                    ApplyPolygonVertexDrag(_interactionPlotItem, coord);
                else
                    ApplyResize(_interactionPlotItem, coord);

                e.Handled = true;
            }
            catch (Exception ex)
            {
                AppLog.Exception(ex, "GateVisualManager.DragLayer_PreviewMouseMove");
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
            FinalizeGateInteraction();

            e.Handled = true;
        }

        private void DragLayer_LostMouseCapture(object sender, MouseEventArgs e)
        {
            // Safety net for capture transitions where PreviewMouseLeftButtonUp may not fire as expected.
            FinalizeGateInteraction();
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
                var gate = _gates[_selectedGateIndex];
                _gateStartBounds = new GateBounds(gate.XMin, gate.XMax, gate.YMin, gate.YMax);
                _gateInteractionDirty = false;

                e.Handled = true;
                dragLayer.CaptureMouse();
            }
            catch (Exception ex)
            {
                AppLog.Exception(ex, "GateVisualManager.StartResize");
            }
        }

        private void BeginMove(PlotItem plotItem, ScottPlot.Coordinates mouseCoord)
        {
            _interactionPlotItem = plotItem;
            _interactionMode = GateInteractionMode.Move;
            _activePolygonVertexIndex = -1;
            _mouseStartCoord = mouseCoord;
            var gate = _gates[_selectedGateIndex];
            _gateStartBounds = new GateBounds(gate.XMin, gate.XMax, gate.YMin, gate.YMax);
            _gateInteractionDirty = false;
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
            // debug mask overlay removed
            plotItem.Plot.Refresh();
        }

        private void ShowHandles()
        {
            if (_handleTL == null || _handleTR == null || _handleBL == null || _handleBR == null)
                return;

            bool isPolygon = _selectedGateIndex >= 0 && _selectedGateIndex < _gates.Count && _gates[_selectedGateIndex] is PolygonGate;
            _handleTL.Visibility = isPolygon ? Visibility.Collapsed : Visibility.Visible;
            _handleTR.Visibility = isPolygon ? Visibility.Collapsed : Visibility.Visible;
            _handleBL.Visibility = isPolygon ? Visibility.Collapsed : Visibility.Visible;
            _handleBR.Visibility = isPolygon ? Visibility.Collapsed : Visibility.Visible;

            if (_ellipseBoundsRect != null)
                _ellipseBoundsRect.Visibility = Visibility.Collapsed;

            if (!isPolygon && _selectedGateIndex >= 0 && _selectedGateIndex < _gates.Count && _gates[_selectedGateIndex] is EllipseGate && _ellipseBoundsRect != null)
                _ellipseBoundsRect.Visibility = Visibility.Visible;
        }

        private void HideHandles()
        {
            if (_handleTL == null || _handleTR == null || _handleBL == null || _handleBR == null)
                return;
            _handleTL.Visibility = Visibility.Collapsed;
            _handleTR.Visibility = Visibility.Collapsed;
            _handleBL.Visibility = Visibility.Collapsed;
            _handleBR.Visibility = Visibility.Collapsed;
            if (_ellipseBoundsRect != null)
                _ellipseBoundsRect.Visibility = Visibility.Collapsed;
            HidePolygonVertexHandles();
        }

        private void UpdateHandlePositions(PlotItem plotItem)
        {
            if (_selectedGateIndex < 0 || _selectedGateIndex >= _gates.Count)
                return;

            if (_handleTL == null || _handleTR == null || _handleBL == null || _handleBR == null)
                return;

            var gate = _gates[_selectedGateIndex];
            if (gate is PolygonGate polygonGate)
            {
                UpdatePolygonVertexHandles(plotItem, polygonGate);
                return;
            }

            HidePolygonVertexHandles();

            PlaceHandle(plotItem, _handleTL, gate.XMin, gate.YMax);
            PlaceHandle(plotItem, _handleTR, gate.XMax, gate.YMax);
            PlaceHandle(plotItem, _handleBL, gate.XMin, gate.YMin);
            PlaceHandle(plotItem, _handleBR, gate.XMax, gate.YMin);
            UpdateEllipseBoundsRect(plotItem, gate);
        }

        private void UpdateEllipseBoundsRect(PlotItem plotItem, GateBase gate)
        {
            if (_ellipseBoundsRect == null)
                return;

            if (gate is not EllipseGate)
            {
                _ellipseBoundsRect.Visibility = Visibility.Collapsed;
                return;
            }

            var axes = plotItem.Plot.Plot.Axes;
            var topLeftPx = plotItem.Plot.Plot.GetPixel(new ScottPlot.Coordinates(gate.XMin, gate.YMax), axes.Bottom, axes.Left);
            var bottomRightPx = plotItem.Plot.Plot.GetPixel(new ScottPlot.Coordinates(gate.XMax, gate.YMin), axes.Bottom, axes.Left);

            var dpi = VisualTreeHelper.GetDpi(plotItem.Plot);
            double left = Math.Min(topLeftPx.X, bottomRightPx.X) / dpi.DpiScaleX;
            double top = Math.Min(topLeftPx.Y, bottomRightPx.Y) / dpi.DpiScaleY;
            double right = Math.Max(topLeftPx.X, bottomRightPx.X) / dpi.DpiScaleX;
            double bottom = Math.Max(topLeftPx.Y, bottomRightPx.Y) / dpi.DpiScaleY;

            Canvas.SetLeft(_ellipseBoundsRect, left);
            Canvas.SetTop(_ellipseBoundsRect, top);
            _ellipseBoundsRect.Width = Math.Max(0, right - left);
            _ellipseBoundsRect.Height = Math.Max(0, bottom - top);
            _ellipseBoundsRect.Visibility = Visibility.Visible;
        }

        private void UpdatePolygonVertexHandles(PlotItem plotItem, PolygonGate gate)
        {
            if (_handlesLayer == null)
                return;

            EnsurePolygonVertexHandleCount(plotItem, gate.Points.Count);

            var axes = plotItem.Plot.Plot.Axes;
            var dpi = VisualTreeHelper.GetDpi(plotItem.Plot);

            for (int i = 0; i < _polygonVertexHandles.Count; i++)
            {
                var h = _polygonVertexHandles[i];
                if (i >= gate.Points.Count)
                {
                    h.Visibility = Visibility.Collapsed;
                    continue;
                }

                var p = gate.Points[i];
                var px = plotItem.Plot.Plot.GetPixel(p, axes.Bottom, axes.Left);
                double dipX = px.X / dpi.DpiScaleX;
                double dipY = px.Y / dpi.DpiScaleY;
                Canvas.SetLeft(h, dipX - HandleSizeDip / 2);
                Canvas.SetTop(h, dipY - HandleSizeDip / 2);
                h.Visibility = Visibility.Visible;
            }
        }

        private void EnsurePolygonVertexHandleCount(PlotItem plotItem, int count)
        {
            if (_handlesLayer == null)
                return;

            while (_polygonVertexHandles.Count < count)
            {
                int idx = _polygonVertexHandles.Count;
                var handle = MakeHandle(Cursors.Hand);
                handle.Visibility = Visibility.Collapsed;
                handle.MouseEnter += (_, __) => Mouse.OverrideCursor = Cursors.Hand;
                handle.MouseLeave += (_, __) =>
                {
                    if (_interactionMode == GateInteractionMode.None)
                        Mouse.OverrideCursor = null;
                };
                handle.MouseLeftButtonDown += (_, e) => StartPolygonVertexDrag(plotItem, idx, e);
                _polygonVertexHandles.Add(handle);
                _handlesLayer.Children.Add(handle);
            }
        }

        private void HidePolygonVertexHandles()
        {
            foreach (var h in _polygonVertexHandles)
                h.Visibility = Visibility.Collapsed;

            if (_interactionMode == GateInteractionMode.None)
                Mouse.OverrideCursor = null;
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

            double xMin = _gateStartBounds.XMin + dx;
            double xMax = _gateStartBounds.XMax + dx;
            double yMin = _gateStartBounds.YMin + dy;
            double yMax = _gateStartBounds.YMax + dy;

            ClampRect(ref xMin, ref xMax, ref yMin, ref yMax, GetBinCount());
            ReplaceGate(plotItem, _selectedGateIndex, xMin, xMax, yMin, yMax);
        }

        private void ApplyResize(PlotItem plotItem, ScottPlot.Coordinates current)
        {
            if (_selectedGateIndex < 0 || _selectedGateIndex >= _gates.Count)
                return;

            double xMin = _gateStartBounds.XMin;
            double xMax = _gateStartBounds.XMax;
            double yMin = _gateStartBounds.YMin;
            double yMax = _gateStartBounds.YMax;

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
            ClampRect(ref xMin, ref xMax, ref yMin, ref yMax, GetBinCount());
            ReplaceGate(plotItem, _selectedGateIndex, xMin, xMax, yMin, yMax);
        }

        private void ReplaceGate(PlotItem plotItem, int index, double xMin, double xMax, double yMin, double yMax)
        {
            var gate = _gates[index];
            gate.SetBounds(xMin, xMax, yMin, yMax);
            gate.RebuildPlottable(plotItem.Plot);

            if (gate.Plottable != null)
                plotItem.Plot.Plot.MoveToTop(gate.Plottable);
            if (gate.LabelPlottable != null)
                plotItem.Plot.Plot.MoveToTop(gate.LabelPlottable);

            UpdateHandlePositions(plotItem);
            // debug mask overlay removed
            _gateInteractionDirty = true;
            plotItem.Plot.Refresh();
        }

        private void ApplyPolygonVertexDrag(PlotItem plotItem, ScottPlot.Coordinates current)
        {
            if (_selectedGateIndex < 0 || _selectedGateIndex >= _gates.Count)
                return;

            if (_activePolygonVertexIndex < 0)
                return;

            if (_gates[_selectedGateIndex] is not PolygonGate polygonGate)
                return;

            int bins = GetBinCount();
            double x = Math.Clamp(current.X, 0, bins);
            double y = Math.Clamp(current.Y, 0, bins);

            polygonGate.SetPoint(_activePolygonVertexIndex, new ScottPlot.Coordinates(x, y));
            polygonGate.RebuildPlottable(plotItem.Plot);

            if (polygonGate.Plottable != null)
                plotItem.Plot.Plot.MoveToTop(polygonGate.Plottable);
            if (polygonGate.LabelPlottable != null)
                plotItem.Plot.Plot.MoveToTop(polygonGate.LabelPlottable);

            UpdateHandlePositions(plotItem);
            _gateInteractionDirty = true;
            plotItem.Plot.Refresh();
        }

        private void FinalizeGateInteraction()
        {
            if (_interactionPlotItem == null)
            {
                _interactionMode = GateInteractionMode.None;
                _gateInteractionDirty = false;
                return;
            }

            var previousMode = _interactionMode;
            _interactionMode = GateInteractionMode.None;
            _activePolygonVertexIndex = -1;
            Mouse.OverrideCursor = null;

            if (!_gateInteractionDirty && previousMode == GateInteractionMode.None)
                return;

            _gateInteractionDirty = false;

            if (_selectedGateIndex < 0 || _selectedGateIndex >= _gates.Count)
                return;

            try
            {
                var gate = _gates[_selectedGateIndex];
                // debug mask overlay removed
                EmitGateUpsert(gate);
            }
            catch (Exception ex)
            {
                AppLog.Exception(ex, "GateVisualManager.FinalizeGateInteraction");
            }
        }

        private void StartPolygonVertexDrag(PlotItem plotItem, int vertexIndex, MouseButtonEventArgs e)
        {
            if (_selectedGateIndex < 0 || _selectedGateIndex >= _gates.Count)
                return;

            if (_gates[_selectedGateIndex] is not PolygonGate)
                return;

            try
            {
                _interactionPlotItem = plotItem;
                _interactionMode = GateInteractionMode.VertexDrag;
                _activePolygonVertexIndex = vertexIndex;
                _gateInteractionDirty = false;
                Mouse.OverrideCursor = Cursors.Hand;

                var dragLayer = plotItem.PlotContainer.DragLayer;
                e.Handled = true;
                dragLayer.CaptureMouse();
            }
            catch (Exception ex)
            {
                AppLog.Exception(ex, "GateVisualManager.StartPolygonVertexDrag");
            }
        }

        private static void NormalizeRect(ref double xMin, ref double xMax, ref double yMin, ref double yMax)
        {
            if (xMin > xMax)
                (xMin, xMax) = (xMax, xMin);
            if (yMin > yMax)
                (yMin, yMax) = (yMax, yMin);
        }

        private static void ClampRect(ref double xMin, ref double xMax, ref double yMin, ref double yMax, int bins)
        {
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

        private int GetBinCount()
        {
            try
            {
                return Math.Max(1, _getBinCount());
            }
            catch
            {
                return 1;
            }
        }

        private void EmitGateUpsert(GateBase gate)
        {
            var sink = _gateSettingsSink;
            if (sink == null)
                return;

            try
            {
                int bins = GetBinCount();
                var plotId = _getPlotId();
                var plotType = _getPlotType();

                GateType gateType = gate switch
                {
                    EllipseGate => GateType.Ellipse,
                    PolygonGate => GateType.Polygon,
                    _ => GateType.Rectangle,
                };
                GateGeometry geometry;
                if (gateType == GateType.Ellipse)
                {
                    double inv = bins > 0 ? 1.0 / bins : 1.0;
                    double cx = ((gate.XMin + gate.XMax) / 2.0) * inv;
                    double cy = ((gate.YMin + gate.YMax) / 2.0) * inv;
                    double rx = ((gate.XMax - gate.XMin) / 2.0) * inv;
                    double ry = ((gate.YMax - gate.YMin) / 2.0) * inv;
                    geometry = GateGeometry.Ellipse01(cx, cy, rx, ry, angleDeg: 0);
                }
                else if (gateType == GateType.Polygon && gate is PolygonGate polygonGate)
                {
                    double inv = bins > 0 ? 1.0 / bins : 1.0;
                    var points01 = polygonGate.Points
                        .Select(p => new Point01(Math.Clamp(p.X * inv, 0, 1), Math.Clamp(p.Y * inv, 0, 1)))
                        .ToArray();
                    geometry = GateGeometry.Polygon01(points01);
                }
                else
                {
                    geometry = GateGeometry.FromBinRectangle(gate.XMin, gate.XMax, gate.YMin, gate.YMax, bins);
                }

                sink(new GateSettings
                {
                    GateId = gate.GateId,
                    Name = gate.Name,
                    Plot = new GatePlotRef(plotId, plotType),
                    GateType = gateType,
                    Geometry = geometry,
                    UpdatedUtc = DateTime.UtcNow,
                });
            }
            catch (Exception ex)
            {
                AppLog.Exception(ex, "GateVisualManager.EmitGateUpsert");
            }
        }

        private static string GenerateNextGateName(IEnumerable<string> existingNames)
        {
            int maxIndex = -1;
            foreach (var raw in existingNames)
            {
                if (string.IsNullOrWhiteSpace(raw))
                    continue;

                var letters = new string(raw.Where(char.IsLetter).ToArray()).ToUpperInvariant();
                if (letters.Length == 0)
                    continue;

                if (TryParseExcelLabelToIndex(letters, out int idx))
                    maxIndex = Math.Max(maxIndex, idx);
            }

            return ExcelIndexToLabel(maxIndex + 1);
        }

        private static bool TryParseExcelLabelToIndex(string letters, out int index)
        {
            index = -1;
            if (string.IsNullOrWhiteSpace(letters))
                return false;

            int num = 0;
            foreach (char c in letters)
            {
                if (c < 'A' || c > 'Z')
                    return false;

                int v = (c - 'A') + 1;
                num = checked(num * 26 + v);
            }

            index = num - 1;
            return index >= 0;
        }

        private static string ExcelIndexToLabel(int index)
        {
            if (index < 0)
                index = 0;

            int num = index + 1;
            string letters = "";
            while (num > 0)
            {
                num--;
                int rem = num % 26;
                letters = (char)('A' + rem) + letters;
                num /= 26;
            }

            return letters;
        }

        // Debug mask overlay removed.
    }
}

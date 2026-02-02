using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ScottPlot.Interactivity.UserActionResponses;
using ScottPlot.MultiplotLayouts;
using ScottPlot.WPF;
using Grid = System.Windows.Controls.Grid;

namespace Worksheet
{
    public partial class WorksheetGrid : UserControl
    {
        public WorksheetGrid()
        {
            InitializeComponent();
        }

        public void AddPlot()
        {
            // --- ScottPlot control ---
            var plot = new WpfPlot
            {
                Width = 200,
                Height = 200,
            };

            // Disable pan/zoom/etc. by removing common UIP responses
            var uip = plot.UserInputProcessor;
            uip.IsEnabled = true;

            uip.UserActionResponses.RemoveAll(r =>
                r is MouseDragPan ||
                r is MouseDragZoom ||
                r is MouseDragZoomRectangle ||
                r.GetType().Name.Contains("Wheel", StringComparison.OrdinalIgnoreCase) ||
                r.GetType().Name.Contains("Scroll", StringComparison.OrdinalIgnoreCase)
            );

            plot.Plot.FigureBackground.Color = ScottPlot.Color.FromARGB(0);
            plot.Plot.DataBackground.Color = ScottPlot.Color.FromHex("#FFFFFFFF");

            // Optional: show the data-area border so your thumbs visually "sit" on it
            plot.Plot.DataBorder.Width = 1;

            plot.Plot.Add.Scatter(
                new double[] { 1, 2, 3, 4, 5 },
                new double[] { 1, 4, 9, 16, 25 });

            // --- Host that holds plot + overlay ---
            var overlay = new Canvas
            {
                Background = Brushes.Transparent,
                IsHitTestVisible = true,
            };

            var host = new Grid
            {
                Width = plot.Width,
                Height = plot.Height
            };
            host.Children.Add(plot);
            host.Children.Add(overlay);

            // --- Outer container canvas (draggable placement on worksheet) ---
            var container = new Canvas
            {
                Width = host.Width,
                Height = host.Height
            };
            container.Children.Add(host);

            // Place at a default position
            Canvas.SetLeft(container, 10 + 20 * WorksheetGridContainer.Children.Count);
            Canvas.SetTop(container, 10 + 20 * WorksheetGridContainer.Children.Count);

            // --- Drag layer (covers whole plot; reliably receives mouse events) ---
            var dragLayer = new Border
            {
                Background = Brushes.Transparent,
                Cursor = Cursors.SizeAll,
                Width = host.Width,
                Height = host.Height
            };
            overlay.Children.Add(dragLayer);

            // Keep drag layer sized with host
            host.SizeChanged += (_, __) =>
            {
                dragLayer.Width = host.Width;
                dragLayer.Height = host.Height;
            };

            // --- Create 4 resize thumbs on overlay (positioned at DataRect corners) ---
            var tl = MakeThumb(Cursors.SizeNWSE);
            var tr = MakeThumb(Cursors.SizeNESW);
            var bl = MakeThumb(Cursors.SizeNESW);
            var br = MakeThumb(Cursors.SizeNWSE);

            overlay.Children.Add(tl);
            overlay.Children.Add(tr);
            overlay.Children.Add(bl);
            overlay.Children.Add(br);

            // Ensure thumbs are above drag layer for hit testing
            Panel.SetZIndex(dragLayer, 0);
            Panel.SetZIndex(tl, 10);
            Panel.SetZIndex(tr, 10);
            Panel.SetZIndex(bl, 10);
            Panel.SetZIndex(br, 10);

            // --- Thumb positioning: anchor to ScottPlot data area ---
            void UpdateThumbPositions()
            {
                var last = plot.Plot.RenderManager.LastRender;

                // DataRect is pixel coords relative to the plot surface
                var r = last.DataRect;

                // Convert pixels -> WPF DIPs (DPI-aware)
                var dpi = VisualTreeHelper.GetDpi(plot);
                double pxToDipX = 1.0 / dpi.DpiScaleX;
                double pxToDipY = 1.0 / dpi.DpiScaleY;

                double left = r.Left * pxToDipX;
                double right = r.Right * pxToDipX;
                double top = r.Top * pxToDipY;
                double bottom = r.Bottom * pxToDipY;

                PlaceThumb(tl, left, top);
                PlaceThumb(tr, right, top);
                PlaceThumb(bl, left, bottom);
                PlaceThumb(br, right, bottom);
            }

            // Update thumbs after every render (handles axis label size changes etc.)
            plot.Plot.RenderManager.RenderFinished += (_, __) =>
            {
                plot.Dispatcher.Invoke(UpdateThumbPositions);
            };

            // Initial update
            host.Loaded += (_, __) =>
            {
                plot.Refresh();           // triggers render -> RenderFinished -> UpdateThumbPositions
                UpdateThumbPositions();   // try immediately too
            };

            // --- Drag logic (drag the whole container by dragging the dragLayer) ---
            double dragOffsetX = 0, dragOffsetY = 0;

            dragLayer.MouseLeftButtonDown += (s, e) =>
            {
                dragOffsetX = e.GetPosition(WorksheetGridContainer).X - Canvas.GetLeft(container);
                dragOffsetY = e.GetPosition(WorksheetGridContainer).Y - Canvas.GetTop(container);
                dragLayer.CaptureMouse();
                e.Handled = true;
            };

            dragLayer.MouseMove += (s, e) =>
            {
                if (dragLayer.IsMouseCaptured && e.LeftButton == MouseButtonState.Pressed)
                {
                    var pos = e.GetPosition(WorksheetGridContainer);
                    Canvas.SetLeft(container, pos.X - dragOffsetX);
                    Canvas.SetTop(container, pos.Y - dragOffsetY);
                    e.Handled = true;
                }
            };

            dragLayer.MouseLeftButtonUp += (s, e) =>
            {
                dragLayer.ReleaseMouseCapture();
                e.Handled = true;
            };

            // --- Resize logic ---
            AttachResize(tl, HorizontalAlignment.Left, VerticalAlignment.Top);
            AttachResize(tr, HorizontalAlignment.Right, VerticalAlignment.Top);
            AttachResize(bl, HorizontalAlignment.Left, VerticalAlignment.Bottom);
            AttachResize(br, HorizontalAlignment.Right, VerticalAlignment.Bottom);

            void AttachResize(Thumb thumb, HorizontalAlignment hAlign, VerticalAlignment vAlign)
            {
                thumb.DragDelta += (_, e) =>
                {
                    double minSize = 50;

                    double newW = host.Width;
                    double newH = host.Height;

                    // horizontal resize
                    if (hAlign == HorizontalAlignment.Left)
                    {
                        double delta = Math.Min(e.HorizontalChange, host.Width - minSize);
                        newW = host.Width - delta;
                        Canvas.SetLeft(container, Canvas.GetLeft(container) + delta); // keep left edge fixed under cursor
                    }
                    else if (hAlign == HorizontalAlignment.Right)
                    {
                        double delta = Math.Max(e.HorizontalChange, minSize - host.Width);
                        newW = host.Width + delta;
                    }

                    // vertical resize
                    if (vAlign == VerticalAlignment.Top)
                    {
                        double delta = Math.Min(e.VerticalChange, host.Height - minSize);
                        newH = host.Height - delta;
                        Canvas.SetTop(container, Canvas.GetTop(container) + delta);
                    }
                    else if (vAlign == VerticalAlignment.Bottom)
                    {
                        double delta = Math.Max(e.VerticalChange, minSize - host.Height);
                        newH = host.Height + delta;
                    }

                    newW = Math.Max(minSize, newW);
                    newH = Math.Max(minSize, newH);

                    host.Width = plot.Width = container.Width = dragLayer.Width = newW;
                    host.Height = plot.Height = container.Height = dragLayer.Height = newH;

                    plot.Refresh(); // triggers re-render; thumbs will reposition to new DataRect
                };
            }

            // Add to main canvas
            WorksheetGridContainer.Children.Add(container);
        }


        private static Thumb MakeThumb(Cursor cursor)
        {
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.BackgroundProperty, Brushes.Orange);
            borderFactory.SetValue(Border.BorderBrushProperty, Brushes.Black);
            borderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(0.75));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(0));

            var template = new ControlTemplate(typeof(Thumb));
            template.VisualTree = borderFactory;

            return new Thumb
            {
                Width = 6,
                Height = 6,
                Cursor = cursor,
                Template = template
            };
        }

        private static void PlaceThumb(Thumb t, double xDip, double yDip)
        {
            Canvas.SetLeft(t, xDip - t.Width / 2);
            Canvas.SetTop(t, yDip - t.Height / 2);
        }
    }
}

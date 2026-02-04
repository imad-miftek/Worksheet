using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Worksheet.Models;
using Worksheet.Services;
using Worksheet.Views.PlotViews;
using Worksheet.Views.Support;

namespace Worksheet.Views
{
    public partial class WorksheetGrid : UserControl
    {
        private readonly SelectionManager<IWorksheetItem> _selectionManager;
        private readonly PlotFactory _plotFactory;
        private readonly PlotContainerFactory _containerFactory;
        private readonly ThumbManager _thumbManager;
        private readonly DragHandler _dragHandler;
        private readonly ViewportSession _viewportSession;

        private double _snapSize = 0;

        /// <summary>
        /// Grid snap size in pixels. Set to 0 to disable snapping and hide grid lines.
        /// </summary>
        public double SnapSize
        {
            get => _snapSize;
            set
            {
                _snapSize = value;
                UpdateGridBackground();
            }
        }

        public WorksheetGrid() : this(
            new SelectionManager<IWorksheetItem>(),
            new PlotFactory(),
            new PlotContainerFactory(),
            new ThumbManager(),
            new DragHandler(),
            null)
        {
        }

        public WorksheetGrid(
            SelectionManager<IWorksheetItem> selectionManager,
            PlotFactory plotFactory,
            PlotContainerFactory containerFactory,
            ThumbManager thumbManager,
            DragHandler dragHandler,
            ViewportSession? viewportSession)
        {
            _selectionManager = selectionManager;
            _plotFactory = plotFactory;
            _containerFactory = containerFactory;
            _thumbManager = thumbManager;
            _dragHandler = dragHandler;
            _viewportSession = viewportSession ?? new ViewportSession(Dispatcher, System.TimeSpan.FromMilliseconds(250), System.TimeSpan.FromMilliseconds(100));
            _viewportSession.Start();

            InitializeComponent();
            UpdateGridBackground();

            // Click on empty space deselects all items
            WorksheetGridContainer.MouseLeftButtonDown += (s, e) =>
            {
                if (e.OriginalSource == WorksheetGridContainer)
                {
                    _selectionManager.Deselect();
                }
            };
        }

        private void UpdateGridBackground()
        {
            if (_snapSize <= 0)
            {
                // No snapping - solid background
                WorksheetGridContainer.Background = new SolidColorBrush(Colors.WhiteSmoke);
                return;
            }

            // Create grid pattern matching snap size
            var geometryGroup = new GeometryGroup();
            geometryGroup.Children.Add(new RectangleGeometry(new Rect(0, 0, _snapSize, _snapSize)));
            geometryGroup.Children.Add(new LineGeometry(new Point(0, 0), new Point(_snapSize, 0)));
            geometryGroup.Children.Add(new LineGeometry(new Point(0, 0), new Point(0, _snapSize)));

            var drawing = new GeometryDrawing
            {
                Geometry = geometryGroup,
                Brush = new SolidColorBrush(Colors.WhiteSmoke),
                Pen = new Pen(new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)), 0.5)
            };

            var brush = new DrawingBrush
            {
                Drawing = drawing,
                TileMode = TileMode.Tile,
                Viewport = new Rect(0, 0, _snapSize, _snapSize),
                ViewportUnits = BrushMappingMode.Absolute
            };

            WorksheetGridContainer.Background = brush;
        }

        public void AddPlot()
        {
            // Create the plot (with fixed padding that aligns with grid)
            var plot = _plotFactory.CreatePlot(200, 200);

            AddPlotToWorksheet(plot, null, null);
        }

        public void AddPlot(PlotType plotType)
        {
            // Create the plot using PlotFactory defaults
            var plot = _plotFactory.CreatePlot(plotType, out var plotView);

            AddPlotToWorksheet(plot, plotView, plotView?.Settings);
        }

        public void AddPlot(PlotType plotType, AxisScaleType axisScale)
        {
            // Create the plot using PlotFactory defaults
            var plot = _plotFactory.CreatePlot(plotType, axisScale, out var plotView);

            AddPlotToWorksheet(plot, plotView, plotView?.Settings);
        }

        private void AddPlotToWorksheet(ScottPlot.WPF.WpfPlot plot, PlotView? plotView, PlotSettings? settings)
        {
            // Create the container structure (use ActualWidth for grid layout)
            var worksheetWidth = WorksheetGridContainer.ActualWidth > 0
                ? WorksheetGridContainer.ActualWidth
                : 800; // fallback if not yet rendered
            var container = _containerFactory.CreateContainer(plot, WorksheetGridContainer.Children.Count, worksheetWidth);

            // Create and setup thumbs
            var thumbs = _thumbManager.CreateThumbs(container.Overlay);
            _thumbManager.AttachPositioning(plot, thumbs);
            _thumbManager.AttachResize(thumbs, container, plot, SnapSize);

            // Create the worksheet item with metadata
            var plotItem = new PlotItem(plot, container, thumbs)
            {
                PlotView = plotView,
                OnCloseRequested = (item) =>
                {
                    if (settings != null)
                    {
                        _viewportSession.UnregisterPlot(settings.Id);
                    }

                    _selectionManager.Unregister(item);
                    WorksheetGridContainer.Children.Remove(item.Container);
                }
            };

            // Setup drag behavior with snapping
            _dragHandler.AttachDrag(container.DragLayer, plotItem,
                                    WorksheetGridContainer, _selectionManager, SnapSize);

            // Attach plot-specific context menu
            plotView?.AttachContextMenu(plotItem);

            // Register with selection manager
            _selectionManager.Register(plotItem, plotItem.OnSelect, plotItem.OnDeselect);

            if (plotView != null && settings != null)
            {
                _viewportSession.RegisterPlot(settings);
                _viewportSession.RegisterRenderTarget(plot, plotView, settings);
            }

            // Add to worksheet and select
            WorksheetGridContainer.Children.Add(container.Container);
            _selectionManager.Select(plotItem);
        }
    }
}

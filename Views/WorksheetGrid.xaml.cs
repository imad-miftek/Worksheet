using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Worksheet.Interfaces;
using Worksheet.Models;
using Worksheet.Services;

namespace Worksheet.Views
{
    public partial class WorksheetGrid : UserControl
    {
        private readonly ISelectionManager<IWorksheetItem> _selectionManager;
        private readonly IPlotFactory _plotFactory;
        private readonly IPlotContainerFactory _containerFactory;
        private readonly IThumbManager _thumbManager;
        private readonly IDragHandler _dragHandler;
        private readonly IContextMenuHandler _contextMenuHandler;

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
            new ContextMenuHandler())
        {
        }

        public WorksheetGrid(
            ISelectionManager<IWorksheetItem> selectionManager,
            IPlotFactory plotFactory,
            IPlotContainerFactory containerFactory,
            IThumbManager thumbManager,
            IDragHandler dragHandler,
            IContextMenuHandler contextMenuHandler)
        {
            _selectionManager = selectionManager;
            _plotFactory = plotFactory;
            _containerFactory = containerFactory;
            _thumbManager = thumbManager;
            _dragHandler = dragHandler;
            _contextMenuHandler = contextMenuHandler;

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
            // Create the plot of the specified type
            var plot = _plotFactory.CreatePlot(200, 200, plotType);

            AddPlotToWorksheet(plot, plotType, null);
        }

        public void AddPlot(PlotType plotType, AxisScaleType axisScale)
        {
            // Create the plot of the specified type with custom axis scale
            var plot = _plotFactory.CreatePlot(200, 200, plotType, axisScale);

            AddPlotToWorksheet(plot, plotType, axisScale);
        }

        private void AddPlotToWorksheet(ScottPlot.WPF.WpfPlot plot, PlotType? plotType, AxisScaleType? axisScale)
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
                PlotType = plotType,
                CurrentAxisScale = axisScale
            };

            // Setup drag behavior with snapping
            _dragHandler.AttachDrag(container.DragLayer, plotItem,
                                    WorksheetGridContainer, _selectionManager, SnapSize);

            // Attach context menu for histograms
            _contextMenuHandler.AttachContextMenu(plotItem, (newScale) =>
            {
                if (plotItem.PlotType == PlotType.Histogram && plotItem.CurrentAxisScale != newScale)
                {
                    _plotFactory.UpdateHistogramAxisScale(plot, newScale);
                    plotItem.CurrentAxisScale = newScale;
                }
            });

            // Register with selection manager
            _selectionManager.Register(plotItem, plotItem.OnSelect, plotItem.OnDeselect);

            // Add to worksheet and select
            WorksheetGridContainer.Children.Add(container.Container);
            _selectionManager.Select(plotItem);
        }
    }
}

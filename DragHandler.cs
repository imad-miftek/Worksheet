using System;
using System.Windows.Controls;
using System.Windows.Input;

namespace Worksheet
{
    public class DragHandler : IDragHandler
    {
        public void AttachDrag(Border dragLayer, IWorksheetItem item, Canvas worksheet,
                               ISelectionManager<IWorksheetItem> selectionManager, double snapSize = 0)
        {
            double dragOffsetX = 0, dragOffsetY = 0;

            dragLayer.MouseLeftButtonDown += (s, e) =>
            {
                // Select this item (handles overlapping items - topmost receives event)
                selectionManager.Select(item);

                dragOffsetX = e.GetPosition(worksheet).X - Canvas.GetLeft(item.Container);
                dragOffsetY = e.GetPosition(worksheet).Y - Canvas.GetTop(item.Container);
                dragLayer.CaptureMouse();
                e.Handled = true;
            };

            dragLayer.MouseMove += (s, e) =>
            {
                if (dragLayer.IsMouseCaptured && e.LeftButton == MouseButtonState.Pressed)
                {
                    var pos = e.GetPosition(worksheet);
                    double x = pos.X - dragOffsetX;
                    double y = pos.Y - dragOffsetY;

                    // Snap to grid if enabled
                    if (snapSize > 0)
                    {
                        x = SnapToGrid(x, snapSize);
                        y = SnapToGrid(y, snapSize);
                    }

                    Canvas.SetLeft(item.Container, x);
                    Canvas.SetTop(item.Container, y);
                    e.Handled = true;
                }
            };

            dragLayer.MouseLeftButtonUp += (s, e) =>
            {
                // Snap on release as well (in case snapping wasn't applied during move)
                if (snapSize > 0)
                {
                    double x = SnapToGrid(Canvas.GetLeft(item.Container), snapSize);
                    double y = SnapToGrid(Canvas.GetTop(item.Container), snapSize);
                    Canvas.SetLeft(item.Container, x);
                    Canvas.SetTop(item.Container, y);
                }

                dragLayer.ReleaseMouseCapture();
                e.Handled = true;
            };
        }

        private static double SnapToGrid(double value, double gridSize)
        {
            return Math.Round(value / gridSize) * gridSize;
        }
    }
}

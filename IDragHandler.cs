using System.Windows.Controls;

namespace Worksheet
{
    /// <summary>
    /// Handles drag interactions for moving worksheet items.
    /// </summary>
    public interface IDragHandler
    {
        /// <summary>
        /// Attaches drag handlers to the drag layer for moving the item.
        /// Also selects the item when drag starts.
        /// </summary>
        /// <param name="dragLayer">The element that receives mouse events</param>
        /// <param name="item">The worksheet item being dragged</param>
        /// <param name="worksheet">The parent worksheet canvas</param>
        /// <param name="selectionManager">Selection manager for selecting on click</param>
        /// <param name="snapSize">Grid snap size (0 for no snapping)</param>
        void AttachDrag(Border dragLayer, IWorksheetItem item, Canvas worksheet,
                        ISelectionManager<IWorksheetItem> selectionManager, double snapSize = 0);
    }
}

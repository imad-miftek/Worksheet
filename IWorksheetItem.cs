using System.Windows.Controls;

namespace Worksheet
{
    /// <summary>
    /// Interface for items that can be placed on a worksheet (plots, images, text boxes, etc.)
    /// </summary>
    public interface IWorksheetItem
    {
        /// <summary>
        /// The outer container element that is positioned on the worksheet.
        /// </summary>
        Canvas Container { get; }

        /// <summary>
        /// Width of the item.
        /// </summary>
        double Width { get; }

        /// <summary>
        /// Height of the item.
        /// </summary>
        double Height { get; }

        /// <summary>
        /// Called when the item is selected.
        /// </summary>
        void OnSelect();

        /// <summary>
        /// Called when the item is deselected.
        /// </summary>
        void OnDeselect();
    }
}

using System;

namespace Worksheet.Interfaces
{
    public interface ISelectionManager<T> where T : class
    {
        /// <summary>
        /// The currently selected item, or null if nothing is selected.
        /// </summary>
        T? Selected { get; }

        /// <summary>
        /// Raised when the selection changes. Parameter is the newly selected item (or null).
        /// </summary>
        event Action<T?>? SelectionChanged;

        /// <summary>
        /// Selects the specified item. If already selected, does nothing.
        /// </summary>
        void Select(T item);

        /// <summary>
        /// Deselects the current selection.
        /// </summary>
        void Deselect();

        /// <summary>
        /// Registers an item with callbacks for selection state changes.
        /// </summary>
        void Register(T item, Action onSelect, Action onDeselect);

        /// <summary>
        /// Unregisters an item and deselects it if currently selected.
        /// </summary>
        void Unregister(T item);
    }
}

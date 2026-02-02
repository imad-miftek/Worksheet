using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using ScottPlot.WPF;

namespace Worksheet
{
    /// <summary>
    /// Manages thumb creation, positioning, and resize behavior for plot containers.
    /// </summary>
    public interface IThumbManager
    {
        /// <summary>
        /// Creates 4 corner thumbs and adds them to the overlay.
        /// Thumbs start with Collapsed visibility.
        /// </summary>
        Thumb[] CreateThumbs(Canvas overlay);

        /// <summary>
        /// Attaches positioning logic that updates thumb positions when the plot renders.
        /// </summary>
        void AttachPositioning(WpfPlot plot, Thumb[] thumbs);

        /// <summary>
        /// Attaches resize handlers to the thumbs for resizing the plot container.
        /// </summary>
        /// <param name="thumbs">The corner thumbs</param>
        /// <param name="container">The plot container</param>
        /// <param name="plot">The plot control</param>
        /// <param name="snapSize">Grid snap size (0 for no snapping)</param>
        void AttachResize(Thumb[] thumbs, PlotContainer container, WpfPlot plot, double snapSize = 0);
    }
}

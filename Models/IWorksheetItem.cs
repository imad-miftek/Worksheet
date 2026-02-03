using System.Windows.Controls;

namespace Worksheet.Models
{
    public interface IWorksheetItem
    {
        Canvas Container { get; }
        double Width { get; }
        double Height { get; }
    }
}

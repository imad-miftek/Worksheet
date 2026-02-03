using System.Windows.Controls;

namespace Worksheet.Interfaces
{
    public interface IWorksheetItem
    {
        Canvas Container { get; }
        double Width { get; }
        double Height { get; }
    }
}

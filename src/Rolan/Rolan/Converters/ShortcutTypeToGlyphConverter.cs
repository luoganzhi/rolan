using System.Globalization;
using System.Windows.Data;
using Rolan.Models;

namespace Rolan.Converters;

[ValueConversion(typeof(ShortcutType), typeof(string))]
public class ShortcutTypeToGlyphConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            ShortcutType.Application => "APP",
            ShortcutType.Folder => "DIR",
            ShortcutType.Url => "WEB",
            ShortcutType.SystemCommand => "SYS",
            _ => "DOC"
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

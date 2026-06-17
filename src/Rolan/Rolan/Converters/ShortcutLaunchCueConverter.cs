using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Rolan.Converters;

[ValueConversion(typeof(int), typeof(object))]
public class ShortcutLaunchCueConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var index = value is int intValue ? intValue : -1;
        var isVisibleCue = index is >= 0 and < 9;

        if (string.Equals(parameter as string, "Visibility", StringComparison.OrdinalIgnoreCase))
            return isVisibleCue ? Visibility.Visible : Visibility.Collapsed;

        return isVisibleCue ? (index + 1).ToString(CultureInfo.InvariantCulture) : string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

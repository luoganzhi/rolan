using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using Rolan.Helpers;

namespace Rolan.Converters;

[ValueConversion(typeof(byte[]), typeof(BitmapSource))]
public class IconToImageConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is byte[] iconData && iconData.Length > 0)
            return IconHelper.LoadFromBytes(iconData);
        return null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

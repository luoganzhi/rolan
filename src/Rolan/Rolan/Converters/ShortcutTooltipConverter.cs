using System.Globalization;
using System.Text;
using System.Windows.Data;
using Rolan.Models;

namespace Rolan.Converters;

public class ShortcutTooltipConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not ShortcutItem item)
            return string.Empty;

        var builder = new StringBuilder();
        AppendLine(builder, "名称", item.Name);
        AppendLine(builder, "类型", GetTypeLabel(item.Type));
        AppendLine(builder, "目标", item.TargetPath);
        AppendLine(builder, "参数", item.Arguments);
        AppendLine(builder, "工作目录", item.WorkingDirectory);
        AppendLine(builder, "分组", item.GroupName);
        AppendLine(builder, "启动次数", item.LaunchCount.ToString(culture));
        AppendLine(builder, "最近启动", item.LastLaunchedAt?.ToString("yyyy-MM-dd HH:mm", culture));
        return builder.ToString().TrimEnd();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;

    private static void AppendLine(StringBuilder builder, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        builder.Append(label).Append(": ").AppendLine(value.Trim());
    }

    private static string GetTypeLabel(ShortcutType type)
        => type switch
        {
            ShortcutType.Application => "应用",
            ShortcutType.File => "文件",
            ShortcutType.Folder => "文件夹",
            ShortcutType.Url => "网址",
            ShortcutType.SystemCommand => "系统命令",
            _ => type.ToString()
        };
}

using System.Windows;
using System.Windows.Controls;
using Rolan.Helpers;
using Rolan.Models;

namespace Rolan.Views;

public partial class EditShortcutDialog : Window
{
    private readonly ShortcutItem _item;

    public EditShortcutDialog(ShortcutItem item)
    {
        _item = item;
        Width = 440;
        Height = 420;
        Title = "编辑快捷方式";
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        WindowStyle = WindowStyle.ToolWindow;
        ResizeMode = ResizeMode.NoResize;

        var grid = new Grid { Margin = new Thickness(16) };
        for (var i = 0; i < 9; i++)
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // 名称
        grid.Children.Add(new TextBlock { Text = "名称:", Margin = new Thickness(0, 8, 0, 4) });
        Grid.SetRow(grid.Children[^1], 0);

        var nameBox = new System.Windows.Controls.TextBox { Text = item.Name, Margin = new Thickness(0, 0, 0, 8) };
        Grid.SetRow(nameBox, 1);
        grid.Children.Add(nameBox);

        // 路径
        grid.Children.Add(new TextBlock { Text = "路径:", Margin = new Thickness(0, 8, 0, 4) });
        Grid.SetRow(grid.Children[^1], 2);

        var pathBox = new System.Windows.Controls.TextBox { Text = item.TargetPath, Margin = new Thickness(0, 0, 0, 8) };
        Grid.SetRow(pathBox, 3);
        grid.Children.Add(pathBox);

        // 参数
        grid.Children.Add(new TextBlock { Text = "启动参数:", Margin = new Thickness(0, 8, 0, 4) });
        Grid.SetRow(grid.Children[^1], 4);

        var argumentsBox = new System.Windows.Controls.TextBox { Text = item.Arguments ?? string.Empty, Margin = new Thickness(0, 0, 0, 8) };
        Grid.SetRow(argumentsBox, 5);
        grid.Children.Add(argumentsBox);

        // 工作目录
        grid.Children.Add(new TextBlock { Text = "工作目录:", Margin = new Thickness(0, 8, 0, 4) });
        Grid.SetRow(grid.Children[^1], 6);

        var workingDirectoryBox = new System.Windows.Controls.TextBox { Text = item.WorkingDirectory ?? string.Empty, Margin = new Thickness(0, 0, 0, 8) };
        Grid.SetRow(workingDirectoryBox, 7);
        grid.Children.Add(workingDirectoryBox);

        // 按钮
        var btnPanel = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0)
        };
        var saveBtn = new System.Windows.Controls.Button
        {
            Content = "保存",
            Width = 70,
            Height = 28,
            Margin = new Thickness(0, 0, 8, 0)
        };
        saveBtn.Click += (_, _) =>
        {
            var name = nameBox.Text.Trim();
            var targetPath = TargetPathHelper.NormalizeInput(pathBox.Text);
            if (string.IsNullOrWhiteSpace(name))
            {
                System.Windows.MessageBox.Show(this, "名称不能为空。", "Rolan",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (string.IsNullOrWhiteSpace(targetPath))
            {
                System.Windows.MessageBox.Show(this, "路径不能为空。", "Rolan",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _item.Name = name;
            _item.TargetPath = targetPath;
            _item.Arguments = NullIfWhiteSpace(argumentsBox.Text);
            _item.WorkingDirectory = NullIfWhiteSpace(workingDirectoryBox.Text);
            DialogResult = true;
            Close();
        };
        var cancelBtn = new System.Windows.Controls.Button { Content = "取消", Width = 70, Height = 28 };
        cancelBtn.Click += (_, _) => { DialogResult = false; Close(); };

        btnPanel.Children.Add(saveBtn);
        btnPanel.Children.Add(cancelBtn);
        Grid.SetRow(btnPanel, 8);
        grid.Children.Add(btnPanel);

        Content = grid;
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}

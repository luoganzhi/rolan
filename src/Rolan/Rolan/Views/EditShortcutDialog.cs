using System.Windows;
using System.Windows.Controls;
using Rolan.Models;

namespace Rolan.Views;

public partial class EditShortcutDialog : Window
{
    private readonly ShortcutItem _item;

    public EditShortcutDialog(ShortcutItem item)
    {
        _item = item;
        Width = 400;
        Height = 300;
        Title = "编辑快捷方式";
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        WindowStyle = WindowStyle.ToolWindow;
        ResizeMode = ResizeMode.NoResize;

        var grid = new Grid { Margin = new Thickness(16) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // 名称
        grid.Children.Add(new TextBlock { Text = "名称:", Margin = new Thickness(0, 8, 0, 4) });
        Grid.SetRow(grid.Children[^1], 0);

        var nameBox = new TextBox { Text = item.Name, Margin = new Thickness(0, 0, 0, 8) };
        Grid.SetRow(nameBox, 1);
        grid.Children.Add(nameBox);

        // 路径
        grid.Children.Add(new TextBlock { Text = "路径:", Margin = new Thickness(0, 8, 0, 4) });
        Grid.SetRow(grid.Children[^1], 2);

        var pathBox = new TextBox { Text = item.TargetPath, Margin = new Thickness(0, 0, 0, 8) };
        Grid.SetRow(pathBox, 3);
        grid.Children.Add(pathBox);

        // 按钮
        var btnPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0)
        };
        var saveBtn = new Button
        {
            Content = "保存",
            Width = 70,
            Height = 28,
            Margin = new Thickness(0, 0, 8, 0)
        };
        saveBtn.Click += (_, _) =>
        {
            _item.Name = nameBox.Text.Trim();
            _item.TargetPath = pathBox.Text.Trim();
            DialogResult = true;
            Close();
        };
        var cancelBtn = new Button { Content = "取消", Width = 70, Height = 28 };
        cancelBtn.Click += (_, _) => { DialogResult = false; Close(); };

        btnPanel.Children.Add(saveBtn);
        btnPanel.Children.Add(cancelBtn);
        Grid.SetRow(btnPanel, 4);
        grid.Children.Add(btnPanel);

        Content = grid;
    }
}

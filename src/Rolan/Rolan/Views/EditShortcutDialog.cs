using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Rolan.Helpers;
using Rolan.Models;

namespace Rolan.Views;

public partial class EditShortcutDialog : Window
{
    private readonly ShortcutItem _item;

    public EditShortcutDialog(ShortcutItem item)
    {
        _item = item;
        Width = 480;
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

        var pathBox = new System.Windows.Controls.TextBox { Text = item.TargetPath };
        var pathPanel = CreateBrowseRow(pathBox, "浏览...", () => BrowseTarget(pathBox));
        Grid.SetRow(pathPanel, 3);
        grid.Children.Add(pathPanel);

        // 参数
        grid.Children.Add(new TextBlock { Text = "启动参数:", Margin = new Thickness(0, 8, 0, 4) });
        Grid.SetRow(grid.Children[^1], 4);

        var argumentsBox = new System.Windows.Controls.TextBox { Text = item.Arguments ?? string.Empty, Margin = new Thickness(0, 0, 0, 8) };
        Grid.SetRow(argumentsBox, 5);
        grid.Children.Add(argumentsBox);

        // 工作目录
        grid.Children.Add(new TextBlock { Text = "工作目录:", Margin = new Thickness(0, 8, 0, 4) });
        Grid.SetRow(grid.Children[^1], 6);

        var workingDirectoryBox = new System.Windows.Controls.TextBox { Text = item.WorkingDirectory ?? string.Empty };
        var workingDirectoryPanel = CreateBrowseRow(workingDirectoryBox, "浏览...", () => BrowseWorkingDirectory(workingDirectoryBox));
        Grid.SetRow(workingDirectoryPanel, 7);
        grid.Children.Add(workingDirectoryPanel);

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

    private StackPanel CreateBrowseRow(System.Windows.Controls.TextBox textBox, string buttonText, Action browseAction)
    {
        var panel = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 8)
        };

        textBox.VerticalContentAlignment = VerticalAlignment.Center;
        textBox.MinWidth = 0;
        textBox.Width = 356;

        var browseButton = new System.Windows.Controls.Button
        {
            Content = buttonText,
            Width = 76,
            Height = 26,
            Margin = new Thickness(8, 0, 0, 0)
        };
        browseButton.Click += (_, _) => browseAction();

        panel.Children.Add(textBox);
        panel.Children.Add(browseButton);
        return panel;
    }

    private void BrowseTarget(System.Windows.Controls.TextBox pathBox)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择快捷方式目标",
            Filter = "所有文件 (*.*)|*.*|程序 (*.exe)|*.exe|快捷方式 (*.lnk)|*.lnk",
            FileName = TryResolveFileName(pathBox.Text)
        };

        if (dialog.ShowDialog(this) == true)
            pathBox.Text = dialog.FileName;
    }

    private void BrowseWorkingDirectory(System.Windows.Controls.TextBox workingDirectoryBox)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "选择工作目录",
            SelectedPath = TryResolveDirectory(workingDirectoryBox.Text) ?? string.Empty,
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog(new Win32WindowOwner(this)) == System.Windows.Forms.DialogResult.OK)
            workingDirectoryBox.Text = dialog.SelectedPath;
    }

    private static string TryResolveFileName(string? value)
    {
        try
        {
            var normalized = TargetPathHelper.NormalizeInput(value);
            if (string.IsNullOrWhiteSpace(normalized) || TargetPathHelper.IsUrl(normalized))
                return string.Empty;

            var resolved = TargetPathHelper.Resolve(normalized);
            return File.Exists(resolved) ? resolved : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string? TryResolveDirectory(string? value)
    {
        try
        {
            var normalized = TargetPathHelper.NormalizeInput(value);
            if (string.IsNullOrWhiteSpace(normalized))
                return null;

            var resolved = TargetPathHelper.Resolve(normalized);
            return Directory.Exists(resolved) ? resolved : null;
        }
        catch
        {
            return null;
        }
    }

    private sealed class Win32WindowOwner : System.Windows.Forms.IWin32Window
    {
        public Win32WindowOwner(Window window)
        {
            Handle = new WindowInteropHelper(window).Handle;
        }

        public IntPtr Handle { get; }
    }
}

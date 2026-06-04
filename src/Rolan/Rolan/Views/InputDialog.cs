using System.Windows;
using System.Windows.Controls;

namespace Rolan.Views;

/// <summary>
/// 简单的文本输入对话框（替代 Microsoft.VisualBasic.InputBox）
/// </summary>
public partial class InputDialog : Window
{
    public string Result { get; private set; } = string.Empty;

    public InputDialog(string title, string prompt, string defaultValue = "")
    {
        Width = 340;
        Height = 160;
        Title = title;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        WindowStyle = WindowStyle.ToolWindow;
        ResizeMode = ResizeMode.NoResize;

        var grid = new Grid { Margin = new Thickness(16) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // 提示
        var promptText = new TextBlock { Text = prompt, FontSize = 12, Margin = new Thickness(0, 0, 0, 8) };
        Grid.SetRow(promptText, 0);
        grid.Children.Add(promptText);

        // 输入框
        var textBox = new TextBox { Text = defaultValue, FontSize = 12, Margin = new Thickness(0, 0, 0, 12) };
        textBox.SelectAll();
        textBox.Focus();
        Grid.SetRow(textBox, 1);
        grid.Children.Add(textBox);

        // 按钮
        var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var okBtn = new Button { Content = "确定", Width = 70, Height = 26, Margin = new Thickness(0, 0, 8, 0) };
        okBtn.Click += (_, _) => { Result = textBox.Text; DialogResult = true; Close(); };
        var cancelBtn = new Button { Content = "取消", Width = 70, Height = 26 };
        cancelBtn.Click += (_, _) => { DialogResult = false; Close(); };

        btnPanel.Children.Add(okBtn);
        btnPanel.Children.Add(cancelBtn);
        Grid.SetRow(btnPanel, 2);
        grid.Children.Add(btnPanel);

        Content = grid;

        // Enter 键确定
        textBox.KeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Enter)
                okBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        };
    }
}

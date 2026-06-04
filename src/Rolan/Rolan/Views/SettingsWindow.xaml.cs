using System.Windows;

namespace Rolan.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}

using CommunityToolkit.Mvvm.ComponentModel;

namespace Rolan.Models;

public partial class ShortcutGroup : ObservableObject
{
    [ObservableProperty]
    private int _id;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private int _order;

    [ObservableProperty]
    private byte[]? _iconData;

    [ObservableProperty]
    private List<ShortcutItem> _items = new();
}

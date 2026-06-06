using CommunityToolkit.Mvvm.ComponentModel;

namespace Rolan.Models;

public enum ShortcutType
{
    Application,
    File,
    Folder,
    Url,
    SystemCommand
}

public partial class ShortcutItem : ObservableObject
{
    [ObservableProperty]
    private int _id;

    [ObservableProperty]
    private int _groupId;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _targetPath = string.Empty;

    [ObservableProperty]
    private string? _arguments;

    [ObservableProperty]
    private string? _workingDirectory;

    [ObservableProperty]
    private byte[]? _iconData;

    [ObservableProperty]
    private int _order;

    [ObservableProperty]
    private ShortcutType _type;

    [ObservableProperty]
    private DateTime _createdAt = DateTime.Now;

    [ObservableProperty]
    private int _launchCount;

    [ObservableProperty]
    private DateTime? _lastLaunchedAt;

    [ObservableProperty]
    private string? _groupName;
}

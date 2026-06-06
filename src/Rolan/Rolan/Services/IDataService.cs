using Rolan.Models;

namespace Rolan.Services;

public interface IDataService
{
    Task<List<ShortcutGroup>> LoadAllAsync();
    Task SaveGroupAsync(ShortcutGroup group);
    Task DeleteGroupAsync(int groupId);
    Task SaveItemAsync(ShortcutItem item);
    Task DeleteItemAsync(int itemId);
    Task ReorderGroupAsync(int groupId, int newOrder);
    Task ReorderItemAsync(int itemId, int newOrder);
    Task RecordItemLaunchAsync(int itemId, int launchCount, DateTime lastLaunchedAt);
}

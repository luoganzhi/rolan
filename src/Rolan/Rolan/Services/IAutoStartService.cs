namespace Rolan.Services;

public interface IAutoStartService
{
    void SetEnabled(bool enable);
    bool IsEnabled { get; }
}

using WinManager.Common;

namespace WinManager.ViewModels.Optimization;

public abstract class OptimizationCategoryViewModelBase : ObservableObject
{
    public string Title { get; }
    public string PlaceholderMessage { get; }

    protected OptimizationCategoryViewModelBase(string title)
    {
        Title = title;
        PlaceholderMessage = $"Coming soon: {title} optimization tweaks.";
    }
}

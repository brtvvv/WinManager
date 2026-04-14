using WinManager.Common;

namespace WinManager.ViewModels.Customization;

public abstract class CustomizationCategoryViewModelBase : ObservableObject
{
    public string Title { get; }

    protected CustomizationCategoryViewModelBase(string title)
    {
        Title = title;
    }
}

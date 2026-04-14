using WinManager.Common;
using WinManager.Models;
using WinManager.ViewModels.Customization;

namespace WinManager.ViewModels;

public class CustomizationViewModel : ObservableObject
{
    private CustomizationCategoryViewModelBase? _currentCategoryView;
    private bool _showCategories = true;

    public CustomizationViewModel()
    {
        Categories = new List<OptimizationCategoryItem>
        {
            new("theme", "Windows Theme", "Change theme and visual effects"),
            new("taskbar", "Taskbar", "Customize taskbar items and behavior"),
            new("startmenu", "Start Menu", "Configure Start Menu layout and behavior"),
        };

        SelectCategoryCommand = new RelayCommand<string>(OnSelectCategory);
        GoBackCommand = new RelayCommand(OnGoBack);
    }

    public IReadOnlyList<OptimizationCategoryItem> Categories { get; }

    public RelayCommand<string> SelectCategoryCommand { get; }

    public RelayCommand GoBackCommand { get; }

    public bool ShowCategories
    {
        get => _showCategories;
        private set => SetProperty(ref _showCategories, value);
    }

    public CustomizationCategoryViewModelBase? CurrentCategoryView
    {
        get => _currentCategoryView;
        private set => SetProperty(ref _currentCategoryView, value);
    }

    private void OnSelectCategory(string? key)
    {
        CurrentCategoryView = key switch
        {
            "theme" => new WindowsThemeViewModel(),
            "taskbar" => new TaskbarViewModel(),
            "startmenu" => new StartMenuViewModel(),
            _ => null
        };
        ShowCategories = CurrentCategoryView is null;
    }

    private void OnGoBack()
    {
        CurrentCategoryView = null;
        ShowCategories = true;
    }
}

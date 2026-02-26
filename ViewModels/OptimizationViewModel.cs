using WinManager.Common;
using WinManager.Models;
using WinManager.ViewModels.Optimization;

namespace WinManager.ViewModels;

public class OptimizationViewModel : ObservableObject
{
    private OptimizationCategoryViewModelBase? _currentCategoryView;
    private bool _showCategories = true;

    public OptimizationViewModel()
    {
        Categories = new List<OptimizationCategoryItem>
        {
            new("privacy", "Privacy & Security", "Manage privacy and security settings"),
            new("power", "Power", "Configure power and battery options"),
            new("gaming", "Gaming & Performance", "Optimize for gaming and performance"),
            new("update", "Update", "Manage Windows Update settings"),
            new("notifications", "Notifications", "Configure notification preferences"),
            new("sound", "Sound", "Adjust audio and sound settings"),
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

    public OptimizationCategoryViewModelBase? CurrentCategoryView
    {
        get => _currentCategoryView;
        private set => SetProperty(ref _currentCategoryView, value);
    }

    private void OnSelectCategory(string? key)
    {
        CurrentCategoryView = key switch
        {
            "privacy" => new PrivacySecurityViewModel(),
            "power" => new PowerViewModel(),
            "gaming" => new GamingPerformanceViewModel(),
            "update" => new UpdateViewModel(),
            "notifications" => new NotificationsViewModel(),
            "sound" => new SoundViewModel(),
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

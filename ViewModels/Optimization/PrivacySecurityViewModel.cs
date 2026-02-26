using System.Collections.ObjectModel;
using System.Linq;
using WinManager.Common;
using WinManager.Models;
using WinManager.Services;

namespace WinManager.ViewModels.Optimization;

public class PrivacySecurityViewModel : OptimizationCategoryViewModelBase
{
    private readonly UacService _uacService = new();
    private UacOption? _selectedUacOption;
    private string _statusMessage = string.Empty;
    private bool _showStatus;
    private bool _suppressUacChange;

    public PrivacySecurityViewModel() : base("Privacy & Security")
    {
        UacOptions = new ObservableCollection<UacOption>
        {
            new(UacLevel.PromptForCredentials, "Prompt for Credentials",
                "Always prompt for credentials on the secure desktop"),
            new(UacLevel.AlwaysNotify, "Always notify",
                "Notify when apps or you try to make changes"),
            new(UacLevel.NotifyAppChanges, "Notify when apps try to make changes",
                "Default. Notify only when apps try to make changes"),
            new(UacLevel.NotifyAppChangesNoDim, "Notify when apps try to make changes (no dim)",
                "Same as above but without dimming the desktop"),
            new(UacLevel.NeverNotify, "Never notify",
                "Never notify about changes (not recommended)")
        };

        DismissStatusCommand = new RelayCommand(() => ShowStatus = false);

        DetectCurrentLevel();
    }

    public ObservableCollection<UacOption> UacOptions { get; }

    public RelayCommand DismissStatusCommand { get; }

    public UacOption? SelectedUacOption
    {
        get => _selectedUacOption;
        set
        {
            var previous = _selectedUacOption;
            if (!SetProperty(ref _selectedUacOption, value))
                return;

            if (_suppressUacChange || value is null || previous is null)
                return;

            var (success, message, _) = _uacService.SetUacLevel(value.Level);
            StatusMessage = message;
            ShowStatus = true;

            if (!success)
            {
                _suppressUacChange = true;
                SelectedUacOption = previous;
                _suppressUacChange = false;
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool ShowStatus
    {
        get => _showStatus;
        private set => SetProperty(ref _showStatus, value);
    }

    public bool IsAdministrator => _uacService.IsAdministrator;

    private void DetectCurrentLevel()
    {
        var current = _uacService.GetCurrentUacLevel();
        _suppressUacChange = true;
        SelectedUacOption = UacOptions.FirstOrDefault(o => o.Level == current);
        _suppressUacChange = false;
    }
}

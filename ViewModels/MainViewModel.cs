using WinManager.Common;
using WinManager.Models;

namespace WinManager.ViewModels;

/// <summary>
/// Root VM: keeps current section (sidebar navigation) and exposes Programs VM.
/// </summary>
public class MainViewModel : ObservableObject
{
    public MainViewModel()
    {
        Programs = new ProgramsViewModel();
        SelectedSection = AppSection.Programs;
        SelectSectionCommand = new RelayCommand<AppSection>(s => SelectedSection = s);
    }

    public ProgramsViewModel Programs { get; }

    public RelayCommand<AppSection> SelectSectionCommand { get; }

    private AppSection _selectedSection;
    public AppSection SelectedSection
    {
        get => _selectedSection;
        set
        {
            if (SetProperty(ref _selectedSection, value))
            {
                // Future: trigger loading for other sections
            }
        }
    }

    public string OptimizationMessage => "Coming soon: system optimization tweaks.";

    public string CustomizationMessage => "Coming soon: personalization and UI tweaks.";
}


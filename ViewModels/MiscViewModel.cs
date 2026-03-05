using System.Collections.ObjectModel;
using System.Diagnostics;
using WinManager.Common;
using WinManager.Models;

namespace WinManager.ViewModels;

public class MiscViewModel : ObservableObject
{
    public MiscViewModel()
    {
        Panels = new ObservableCollection<SystemPanelItem>
        {
            new("Computer Management", "compmgmt.msc"),
            new("Control Panel", "control"),
            new("Network Connections", "ncpa.cpl"),
            new("Power Options", "powercfg.cpl"),
            new("Printer Panel", "control", "printers"),
            new("System Properties", "sysdm.cpl"),
            new("Time and Date", "timedate.cpl")
        };

        OpenPanelCommand = new RelayCommand<SystemPanelItem>(OpenPanel);
    }

    public ObservableCollection<SystemPanelItem> Panels { get; }

    public RelayCommand<SystemPanelItem> OpenPanelCommand { get; }

    private static void OpenPanel(SystemPanelItem? panel)
    {
        if (panel is null)
            return;

        try
        {
            Process.Start(new ProcessStartInfo(panel.FileName)
            {
                Arguments = panel.Arguments ?? string.Empty,
                UseShellExecute = true
            });
        }
        catch
        {
        }
    }
}

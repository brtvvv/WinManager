namespace WinManager.Models.Config;

public class WinManagerConfig
{
    public string Version { get; set; } = "1.0";
    public DateTime SavedAt { get; set; } = DateTime.Now;

    public Dictionary<string, bool> WindowsFeatures { get; set; } = new();
    public List<string> SelectedExternalApps { get; set; } = new();

    public Dictionary<string, bool> Privacy { get; set; } = new();
    public string? UacLevelName { get; set; }
    public string? DnsProviderName { get; set; }

    public Dictionary<string, bool> Gaming { get; set; } = new();
    public Dictionary<string, bool> Updates { get; set; } = new();
    public Dictionary<string, bool> Notifications { get; set; } = new();
    public Dictionary<string, bool> Sound { get; set; } = new();

    public string? PowerPlanGuid { get; set; }
    public bool? HibernateEnabled { get; set; }
    public Dictionary<string, PowerSettingConfig> PowerSettings { get; set; } = new();
}

public class PowerSettingConfig
{
    public int AcValue { get; set; }
    public int DcValue { get; set; }
}

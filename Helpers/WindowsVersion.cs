namespace WinManager.Helpers;

public static class WindowsVersion
{
    public static readonly int Build = GetBuild();

    public static bool IsAtLeast22H2 => Build >= 22621;
    public static bool IsAtLeast23H2 => Build >= 22631;
    public static bool IsAtLeast24H2 => Build >= 26100;

    public static string DisplayName => Build switch
    {
        >= 26100 => $"Windows 11 24H2 (build {Build})",
        >= 22631 => $"Windows 11 23H2 (build {Build})",
        >= 22621 => $"Windows 11 22H2 (build {Build})",
        >= 22000 => $"Windows 11 21H2 (build {Build})",
        _ => $"Windows (build {Build})"
    };

    private static int GetBuild()
    {
        var raw = Microsoft.Win32.Registry.GetValue(
            @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion",
            "CurrentBuildNumber", null)?.ToString();
        return int.TryParse(raw, out var b) ? b : 0;
    }
}

namespace WinManager.Helpers;

public static class WindowsVersion
{
    private static readonly int Build = GetBuild();

    public static bool IsAtLeast22H2 => Build >= 22621;
    public static bool IsAtLeast23H2 => Build >= 22631;

    private static int GetBuild()
    {
        var raw = Microsoft.Win32.Registry.GetValue(
            @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion",
            "CurrentBuildNumber", null)?.ToString();
        return int.TryParse(raw, out var b) ? b : 0;
    }
}

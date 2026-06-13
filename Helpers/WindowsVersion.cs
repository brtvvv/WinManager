namespace WinManager.Helpers;

public static class WindowsVersion
{
    public static readonly int Build = GetBuild();
    public static readonly string DisplayVersion = GetDisplayVersion();

    // 25H2 ships on build 26100 — the same as 24H2 — so the build number alone
    // can't distinguish them. DisplayVersion ("25H2", "24H2", ...) is the
    // authoritative marker; fall back to build for older releases.
    public static bool IsAtLeast22H2 => Build >= 22621;
    public static bool IsAtLeast23H2 => Build >= 22631;
    public static bool IsAtLeast24H2 => Build >= 26100 && !IsAtLeast25H2;
    public static bool IsAtLeast25H2 => DisplayVersion is "25H2" or "26H1" || Build >= 27000;

    public static bool IsProOrEnterprise
    {
        get
        {
            var edition = Microsoft.Win32.Registry.GetValue(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion",
                "EditionID", null)?.ToString();
            return edition is "Professional" or "Enterprise" or "Education";
        }
    }

    public static string DisplayName
    {
        get
        {
            var ver = string.IsNullOrEmpty(DisplayVersion) ? GuessVersionFromBuild() : DisplayVersion;
            return $"Windows 11 {ver} (build {Build})";
        }
    }

    private static int GetBuild()
    {
        var raw = Microsoft.Win32.Registry.GetValue(
            @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion",
            "CurrentBuildNumber", null)?.ToString();
        return int.TryParse(raw, out var b) ? b : 0;
    }

    private static string GetDisplayVersion()
    {
        return Microsoft.Win32.Registry.GetValue(
            @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion",
            "DisplayVersion", null)?.ToString() ?? "";
    }

    private static string GuessVersionFromBuild() => Build switch
    {
        >= 26100 => "24H2",
        >= 22631 => "23H2",
        >= 22621 => "22H2",
        >= 22000 => "21H2",
        _ => "Unknown"
    };
}

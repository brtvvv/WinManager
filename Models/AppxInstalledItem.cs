namespace WinManager.Models;

/// <summary>
/// Single entry from Get-AppxPackage -AllUsers (Name + PackageFullName).
/// </summary>
public class AppxInstalledItem
{
    public string Name { get; set; } = string.Empty;
    public string PackageFullName { get; set; } = string.Empty;
}

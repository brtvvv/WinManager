namespace WinManager.Models;

public class DnsProvider
{
    public DnsProvider(string name, string? primaryDns, string? secondaryDns, bool isDefault = false)
    {
        Name = name;
        PrimaryDns = primaryDns;
        SecondaryDns = secondaryDns;
        IsDefault = isDefault;
    }

    public string Name { get; }
    public string? PrimaryDns { get; }
    public string? SecondaryDns { get; }
    public bool IsDefault { get; }
}

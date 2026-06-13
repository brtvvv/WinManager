using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32;
using WinManager.Models;

namespace WinManager.Services;

public class PrivacySettingsService
{
    private readonly ProcessRunner _runner = new();
    private static readonly SemaphoreSlim _concurrency = new(8);

    // PowerShell child processes inherit a fresh token without the privileges
    // required to take ownership of ConsentStore keys (owned by TrustedInstaller).
    // We enable SeTakeOwnershipPrivilege + SeRestorePrivilege in-process, then
    // use the .NET registry API to set the owner, grant FullControl, and write.

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr h, uint a, out IntPtr t);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool LookupPrivilegeValue(string? s, string n, out long luid);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool AdjustTokenPrivileges(IntPtr t, bool d, ref TOKEN_PRIVILEGES n, int b, IntPtr p, IntPtr l);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [StructLayout(LayoutKind.Sequential)]
    private struct TOKEN_PRIVILEGES { public int Count; public long Luid; public int Attr; }

    private static void EnablePrivilege(string name)
    {
        if (!OpenProcessToken(GetCurrentProcess(), 0x28, out var tok)) return;
        if (!LookupPrivilegeValue(null, name, out var luid)) return;
        var tp = new TOKEN_PRIVILEGES { Count = 1, Luid = luid, Attr = 2 /* SE_PRIVILEGE_ENABLED */ };
        AdjustTokenPrivileges(tok, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
    }

    public async Task ReadAllStatesAsync(IEnumerable<PrivacyToggleItem> items)
    {
        var tasks = items.Select(async item =>
        {
            await _concurrency.WaitAsync();
            try { await ReadStateAsync(item); }
            finally { _concurrency.Release(); item.IsChecking = false; }
        });
        await Task.WhenAll(tasks);
    }

    public async Task ReadStateAsync(PrivacyToggleItem item)
    {
        try
        {
            if (item.ServiceName is not null)
            {
                var result = await RunPsAsync(
                    $"(Get-Service '{item.ServiceName}' -ErrorAction SilentlyContinue).StartType");
                var output = result.Trim();
                item.IsEnabled = output.Length > 0
                    && !output.Equals("Disabled", StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                var hp = $"{item.Hive}:\\{item.Path}";
                var result = await RunPsAsync(
                    $"try {{ (Get-ItemProperty -Path '{hp}' -Name '{item.ValueName}' -ErrorAction Stop).'{item.ValueName}' }} catch {{ 'NOTFOUND' }}");
                var output = result.Trim();

                if (output is "NOTFOUND" or "")
                {
                    item.IsEnabled = item.DefaultIsEnabled;
                }
                else if (item.IsStringValue)
                {
                    item.IsEnabled = output.Equals(
                        item.EnabledValue?.ToString(), StringComparison.OrdinalIgnoreCase);
                }
                else
                {
                    item.IsEnabled = int.TryParse(output, out var val)
                        && val == Convert.ToInt32(item.EnabledValue);
                }
            }
        }
        catch
        {
            item.IsEnabled = item.DefaultIsEnabled;
        }
        finally
        {
            // Always clear the checking flag so the UI never sticks on
            // "Checking..." for items whose underlying policy key is missing
            // or whose PowerShell read failed (e.g. Widgets on 21H2 Home where
            // the HKLM Dsh key doesn't exist out of the box).
            item.IsChecking = false;
        }
    }

    public async Task<bool> SetStateAsync(PrivacyToggleItem item, bool enable)
    {
        try
        {
            if (item.ServiceName is not null)
            {
                var cmd = enable
                    ? $"Set-Service '{item.ServiceName}' -StartupType Manual"
                    : $"Set-Service '{item.ServiceName}' -StartupType Disabled; Stop-Service '{item.ServiceName}' -Force -ErrorAction SilentlyContinue";
                return await RunPsSuccessAsync(cmd);
            }

            if (enable && item.DeleteOnEnable)
            {
                var hp = $"{item.Hive}:\\{item.Path}";
                return await RunPsSuccessAsync(
                    $"Remove-ItemProperty -Path '{hp}' -Name '{item.ValueName}' -ErrorAction SilentlyContinue");
            }

            var value = enable ? item.EnabledValue : item.DisabledValue;

            // The CapabilityAccessManager\ConsentStore tree is owned by
            // TrustedInstaller on 22H2+. Set-ItemProperty silently no-ops as
            // Administrator, so SetStateAsync would "succeed" but the value
            // would never change. Take ownership of the key first, then write.
            if (item.Hive == "HKLM" &&
                item.Path.IndexOf(@"CapabilityAccessManager\ConsentStore",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return await WriteConsentStoreValueAsync(item, value);
            }

            return await WriteRegistryValueAsync(item, value);
        }
        catch
        {
            return false;
        }
    }

    private Task<bool> WriteConsentStoreValueAsync(PrivacyToggleItem item, object value)
    {
        return Task.Run(() => WriteConsentStoreInProcess(item.Path, item.ValueName, value.ToString() ?? ""));
    }

    private static bool WriteConsentStoreInProcess(string subKey, string name, string value)
    {
        try
        {
            EnablePrivilege("SeTakeOwnershipPrivilege");
            EnablePrivilege("SeRestorePrivilege");

            using var key = Registry.LocalMachine.OpenSubKey(
                subKey, RegistryKeyPermissionCheck.ReadWriteSubTree,
                RegistryRights.TakeOwnership | RegistryRights.ChangePermissions | RegistryRights.SetValue);
            if (key is null) return false;

            var sec = key.GetAccessControl();
            sec.SetOwner(new NTAccount("Administrators"));
            key.SetAccessControl(sec);

            sec = key.GetAccessControl();
            sec.AddAccessRule(new RegistryAccessRule(
                new NTAccount("Administrators"),
                RegistryRights.FullControl, AccessControlType.Allow));
            key.SetAccessControl(sec);

            key.SetValue(name, value, RegistryValueKind.String);
            return true;
        }
        catch
        {
            return false;
        }
    }

    // Writes a registry value using the .NET API directly. This avoids the
    // PowerShell `New-Item -Path X -Force` pitfall: on an existing key that
    // cmdlet RECREATES the key, wiping all sibling values. Multiple toggles
    // pointing at the same key (e.g. all four AppPrivacy policies, or
    // SearchboxTaskbarMode + cache) would erase each other's writes.
    // Registry.SetValue only creates the key when missing; never wipes.
    private Task<bool> WriteRegistryValueAsync(PrivacyToggleItem item, object value)
    {
        return Task.Run(() =>
        {
            try
            {
                var root = item.Hive switch
                {
                    "HKLM" => @"HKEY_LOCAL_MACHINE\",
                    "HKCU" => @"HKEY_CURRENT_USER\",
                    "HKCR" => @"HKEY_CLASSES_ROOT\",
                    "HKU"  => @"HKEY_USERS\",
                    _ => null
                };
                if (root is null) return false;

                var keyPath = root + item.Path;
                var kind = item.IsStringValue ? RegistryValueKind.String : RegistryValueKind.DWord;
                object writeVal = item.IsStringValue ? value.ToString() ?? "" : Convert.ToInt32(value);

                Registry.SetValue(keyPath, item.ValueName, writeVal, kind);

                if (item.ExtraValueNames is { Length: > 0 })
                {
                    foreach (var extra in item.ExtraValueNames)
                        Registry.SetValue(keyPath, extra, writeVal, kind);
                }
                return true;
            }
            catch
            {
                return false;
            }
        });
    }

    public async Task<string[]> GetCurrentDnsServersAsync()
    {
        try
        {
            var result = await RunPsAsync(
                "try { (Get-DnsClientServerAddress -InterfaceAlias (Get-NetAdapter | Where-Object Status -eq 'Up' | Select-Object -First 1 -ExpandProperty InterfaceAlias) -AddressFamily IPv4).ServerAddresses -join ',' } catch { '' }");
            var output = result.Trim();
            if (string.IsNullOrEmpty(output)) return Array.Empty<string>();
            return output.Split(',', StringSplitOptions.RemoveEmptyEntries);
        }
        catch { return Array.Empty<string>(); }
    }

    public async Task<bool> SetDnsProviderAsync(DnsProvider provider)
    {
        try
        {
            string dnsCmd;
            int dohValue;

            if (provider.IsDefault)
            {
                dnsCmd = "Get-NetAdapter | Where-Object Status -eq 'Up' | ForEach-Object { Set-DnsClientServerAddress -InterfaceAlias $_.InterfaceAlias -ResetServerAddresses }";
                dohValue = 0;
            }
            else
            {
                dnsCmd = string.Format(
                    "Get-NetAdapter | Where-Object Status -eq 'Up' | ForEach-Object {{ Set-DnsClientServerAddress -InterfaceAlias $_.InterfaceAlias -ServerAddresses ('{0}','{1}') }}",
                    provider.PrimaryDns, provider.SecondaryDns);
                dohValue = 2;
            }

            var fullCmd = dnsCmd +
                "; New-Item -Path 'HKLM:\\SYSTEM\\CurrentControlSet\\Services\\Dnscache\\Parameters' -Force -EA SilentlyContinue | Out-Null" +
                "; Set-ItemProperty -Path 'HKLM:\\SYSTEM\\CurrentControlSet\\Services\\Dnscache\\Parameters' -Name 'EnableAutoDoh' -Value " + dohValue + " -Type DWord -Force";

            return await RunPsSuccessAsync(fullCmd);
        }
        catch { return false; }
    }

    private async Task<string> RunPsAsync(string command)
    {
        var result = await _runner.RunAsync("powershell.exe",
            $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"");
        return result.Output;
    }

    private async Task<bool> RunPsSuccessAsync(string command)
    {
        var result = await _runner.RunAsync("powershell.exe",
            $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"");
        return result.Success;
    }
}

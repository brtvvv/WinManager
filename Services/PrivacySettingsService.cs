using WinManager.Models;

namespace WinManager.Services;

public class PrivacySettingsService
{
    private readonly ProcessRunner _runner = new();
    private static readonly SemaphoreSlim _concurrency = new(8);

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

    private async Task<bool> WriteConsentStoreValueAsync(PrivacyToggleItem item, object value)
    {
        var fullKey = $"HKLM\\{item.Path}";
        var psKey = $"HKLM:\\{item.Path}";
        var script =
            $"$key = '{fullKey}'; $ps = '{psKey}'; " +
            "$acl = (Get-Item -Path Registry::$key).GetAccessControl(); " +
            "$rule = New-Object System.Security.AccessControl.RegistryAccessRule(" +
            "[System.Security.Principal.NTAccount]'Administrators','FullControl','Allow'); " +
            "$acl.SetAccessRule($rule); " +
            "(Get-Item -Path Registry::$key).SetAccessControl($acl); " +
            $"Set-ItemProperty -Path $ps -Name '{item.ValueName}' -Value '{value}' -Type String -Force";
        return await RunPsSuccessAsync(script);
    }

    private async Task<bool> WriteRegistryValueAsync(PrivacyToggleItem item, object value)
    {
        var hp = $"{item.Hive}:\\{item.Path}";
        var type = item.IsStringValue ? "String" : "DWord";
        var valLiteral = item.IsStringValue ? $"'{value}'" : value.ToString();

        var cmd = $"New-Item -Path '{hp}' -Force -EA SilentlyContinue | Out-Null; " +
                  $"Set-ItemProperty -Path '{hp}' -Name '{item.ValueName}' -Value {valLiteral} -Type {type} -Force";

        if (item.ExtraValueNames is { Length: > 0 })
        {
            foreach (var extra in item.ExtraValueNames)
            {
                cmd += $"; Set-ItemProperty -Path '{hp}' -Name '{extra}' -Value {valLiteral} -Type {type} -Force";
            }
        }

        return await RunPsSuccessAsync(cmd);
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

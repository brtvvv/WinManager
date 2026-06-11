using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using WinManager.Models;

namespace WinManager.Services;

public class PowerService
{
    private readonly ProcessRunner _runner = new();

    private static readonly string SettingsFile = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "winmanager_power_plan.txt");

    private const string HighPerformanceGuid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";

    private static readonly Regex GuidInParensRegex = new(
        @"([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})\s+\((.+?)\)(\s*\*)?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex HexValueRegex = new(
        @":\s+0x([0-9a-fA-F]+)",
        RegexOptions.Compiled);

    public async Task<List<PowerPlan>> GetPlansAsync()
    {
        var result = await _runner.RunAsync("powercfg", "/list");
        var plans = new List<PowerPlan>();

        foreach (Match m in GuidInParensRegex.Matches(result.Output))
        {
            plans.Add(new PowerPlan(
                m.Groups[1].Value,
                m.Groups[2].Value.Trim(),
                m.Groups[3].Value.Contains('*')));
        }

        return plans;
    }

    public async Task<bool> SetActivePlanAsync(string guid)
    {
        var result = await _runner.RunAsync("powercfg", $"/setactive {guid}");
        return result.Success;
    }

    public async Task<(int ac, int dc)> QuerySettingAsync(
        string planGuid, string subGuid, string settingGuid)
    {
        var result = await _runner.RunAsync("powercfg",
            $"/query {planGuid} {subGuid} {settingGuid}");

        var hexValues = HexValueRegex.Matches(result.Output)
            .Cast<Match>()
            .Select(m => int.Parse(m.Groups[1].Value, NumberStyles.HexNumber))
            .ToList();

        int ac = hexValues.Count >= 2 ? hexValues[^2] : 0;
        int dc = hexValues.Count >= 2 ? hexValues[^1] : 0;

        return (ac, dc);
    }

    public async Task<bool> SetValueAsync(
        string planGuid, string subGuid, string settingGuid, bool isAc, int value)
    {
        var flag = isAc ? "/setacvalueindex" : "/setdcvalueindex";
        var result = await _runner.RunAsync("powercfg",
            $"{flag} {planGuid} {subGuid} {settingGuid} {value}");
        if (!result.Success) return false;

        await _runner.RunAsync("powercfg", $"/setactive {planGuid}");
        return true;
    }

    public async Task<string?> EnsureRecommendedPlanAsync(List<PowerPlan> existingPlans)
    {
        if (File.Exists(SettingsFile))
        {
            var stored = (await File.ReadAllTextAsync(SettingsFile)).Trim();
            if (!string.IsNullOrEmpty(stored) &&
                existingPlans.Any(p => p.Guid.Equals(stored, StringComparison.OrdinalIgnoreCase)))
                return stored;
        }

        var result = await _runner.RunAsync("powercfg",
            $"/duplicatescheme {HighPerformanceGuid}");
        if (!result.Success) return null;

        var guidMatch = GuidInParensRegex.Match(result.Output);
        if (!guidMatch.Success)
        {
            var fallback = Regex.Match(result.Output,
                @"([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})",
                RegexOptions.IgnoreCase);
            if (!fallback.Success) return null;
            var fg = fallback.Groups[1].Value;
            await _runner.RunAsync("powercfg", $"/changename {fg} \"WinManager Recommended\"");
            await File.WriteAllTextAsync(SettingsFile, fg);
            return fg;
        }

        var newGuid = guidMatch.Groups[1].Value;
        await _runner.RunAsync("powercfg", $"/changename {newGuid} \"WinManager Recommended\"");
        await File.WriteAllTextAsync(SettingsFile, newGuid);
        return newGuid;
    }

    public async Task<bool> IsHibernateEnabledAsync()
    {
        var result = await _runner.RunAsync("powershell.exe",
            "-NoProfile -Command \"(Get-ItemProperty 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\Power').HibernateEnabled\"");
        return result.Output.Trim() == "1";
    }

    public async Task<bool> SetHibernateAsync(bool enable)
    {
        // cmd.exe is guaranteed to exist and resolve powercfg correctly with
        // current elevation; powershell.exe occasionally fails to launch on
        // minimal/locked-down 21H2 images.
        var result = await _runner.RunAsync("cmd.exe",
            $"/c powercfg {(enable ? "/hibernate on" : "/hibernate off")}");
        return result.Success;
    }

    public async Task<bool> IsLaptopAsync(string planGuid)
    {
        var result = await _runner.RunAsync("powercfg",
            $"/query {planGuid} 4f971e89-eebd-4455-a8de-9e59040e7347 5ca83367-6e45-459f-a27b-476b1d01c936");
        return result.Success && HexValueRegex.IsMatch(result.Output);
    }
}

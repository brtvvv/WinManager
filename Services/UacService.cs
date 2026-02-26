using System.Security.Principal;
using Microsoft.Win32;
using WinManager.Models;

namespace WinManager.Services;

public class UacService
{
    private const string PolicyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System";

    public bool IsAdministrator
    {
        get
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    public UacLevel GetCurrentUacLevel()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(PolicyPath);
            if (key is null)
                return UacLevel.NotifyAppChanges;

            var consent = (int)(key.GetValue("ConsentPromptBehaviorAdmin") ?? 5);
            var secureDesktop = (int)(key.GetValue("PromptOnSecureDesktop") ?? 1);
            var enableLua = (int)(key.GetValue("EnableLUA") ?? 1);

            return (consent, secureDesktop, enableLua) switch
            {
                (1, 1, 1) => UacLevel.PromptForCredentials,
                (2, 1, 1) => UacLevel.AlwaysNotify,
                (5, 1, 1) => UacLevel.NotifyAppChanges,
                (5, 0, 1) => UacLevel.NotifyAppChangesNoDim,
                (0, 0, _) => UacLevel.NeverNotify,
                _ => UacLevel.NotifyAppChanges
            };
        }
        catch
        {
            return UacLevel.NotifyAppChanges;
        }
    }

    public (bool Success, string Message, bool RestartRequired) SetUacLevel(UacLevel level)
    {
        if (!IsAdministrator)
            return (false, "Administrator privileges required to change UAC settings.", false);

        try
        {
            var (consent, secureDesktop, enableLua) = level switch
            {
                UacLevel.PromptForCredentials => (1, 1, 1),
                UacLevel.AlwaysNotify => (2, 1, 1),
                UacLevel.NotifyAppChanges => (5, 1, 1),
                UacLevel.NotifyAppChangesNoDim => (5, 0, 1),
                UacLevel.NeverNotify => (0, 0, 1),
                _ => (5, 1, 1)
            };

            var currentEnableLua = 1;
            try
            {
                using var readKey = Registry.LocalMachine.OpenSubKey(PolicyPath);
                currentEnableLua = (int)(readKey?.GetValue("EnableLUA") ?? 1);
            }
            catch { }

            using var key = Registry.LocalMachine.OpenSubKey(PolicyPath, writable: true);
            if (key is null)
                return (false, "Failed to open registry key.", false);

            key.SetValue("ConsentPromptBehaviorAdmin", consent, RegistryValueKind.DWord);
            key.SetValue("PromptOnSecureDesktop", secureDesktop, RegistryValueKind.DWord);
            key.SetValue("EnableLUA", enableLua, RegistryValueKind.DWord);

            var restartRequired = currentEnableLua != enableLua;
            var message = restartRequired
                ? $"UAC level changed to \"{GetDisplayName(level)}\". Restart required for full effect."
                : $"UAC level changed to \"{GetDisplayName(level)}\".";

            return (true, message, restartRequired);
        }
        catch (UnauthorizedAccessException)
        {
            return (false, "Access denied. Run as Administrator.", false);
        }
        catch (Exception ex)
        {
            return (false, $"Failed to set UAC level: {ex.Message}", false);
        }
    }

    private static string GetDisplayName(UacLevel level) => level switch
    {
        UacLevel.PromptForCredentials => "Prompt for Credentials",
        UacLevel.AlwaysNotify => "Always notify",
        UacLevel.NotifyAppChanges => "Notify when apps try to make changes",
        UacLevel.NotifyAppChangesNoDim => "Notify when apps try to make changes (no dim)",
        UacLevel.NeverNotify => "Never notify",
        _ => level.ToString()
    };
}

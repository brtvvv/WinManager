namespace WinManager.Models;

/// <summary>Result of a process execution (success flag, output, exit code).</summary>
public record ProcessResult(bool Success, string Output, int ExitCode);


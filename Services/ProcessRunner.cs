using System.Diagnostics;
using System.IO;
using System.Text;
using WinManager.Models;

namespace WinManager.Services;

public class ProcessRunner
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(5);

    public async Task<ProcessResult> RunAsync(
        string fileName,
        string arguments,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        try
        {
            using var process = new Process { StartInfo = psi };
            var outputBuilder = new StringBuilder();

            using var timeoutCts = new CancellationTokenSource(timeout ?? DefaultTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, timeoutCts.Token);
            var token = linkedCts.Token;

            process.Start();

            var stdoutTask = ReadStreamAsync(process.StandardOutput, outputBuilder, token);
            var stderrTask = ReadStreamAsync(process.StandardError, outputBuilder, token);

            try
            {
                await process.WaitForExitAsync(token);
            }
            catch (OperationCanceledException)
            {
                TryKillProcess(process);
                var partial = outputBuilder.ToString();
                var reason = cancellationToken.IsCancellationRequested
                    ? "Cancelled by user"
                    : $"Timed out after {(timeout ?? DefaultTimeout).TotalSeconds:0}s";
                return new ProcessResult(false, $"{reason}\n{partial}", -1);
            }

            await Task.WhenAll(
                SafeAwait(stdoutTask),
                SafeAwait(stderrTask));

            return new ProcessResult(process.ExitCode == 0, outputBuilder.ToString(), process.ExitCode);
        }
        catch (OperationCanceledException)
        {
            return new ProcessResult(false, "Operation cancelled", -1);
        }
        catch (Exception ex)
        {
            return new ProcessResult(false, ex.Message, -1);
        }
    }

    public async Task<IReadOnlyCollection<string>> RunPowerShellListAsync(
        string command, CancellationToken cancellationToken = default)
    {
        var result = await RunAsync(
            "powershell.exe",
            $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"",
            cancellationToken);

        if (!result.Success)
            return Array.Empty<string>();

        return result.Output
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
    }

    private static async Task ReadStreamAsync(StreamReader reader, StringBuilder builder, CancellationToken token)
    {
        try
        {
            char[] buffer = new char[1024];
            while (!token.IsCancellationRequested)
            {
                var bytesRead = await reader.ReadAsync(buffer.AsMemory(), token);
                if (bytesRead == 0)
                    break;

                lock (builder)
                    builder.Append(buffer, 0, bytesRead);
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException) { }
    }

    private static void TryKillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch { }
    }

    private static async Task SafeAwait(Task task)
    {
        try { await task; }
        catch { }
    }
}

using System.Diagnostics;
using System.IO;
using System.Text;
using WinManager.Models;

namespace WinManager.Services;

/// <summary>
/// Runs external commands (winget, powershell), collects stdout/stderr, returns ProcessResult.
/// </summary>
public class ProcessRunner
{
    public async Task<ProcessResult> RunAsync(string fileName, string arguments, CancellationToken cancellationToken = default)
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

            process.Start();

            var stdoutTask = ReadStreamAsync(process.StandardOutput, outputBuilder, cancellationToken);
            var stderrTask = ReadStreamAsync(process.StandardError, outputBuilder, cancellationToken);

            await process.WaitForExitAsync(cancellationToken);
            await Task.WhenAll(stdoutTask, stderrTask);

            return new ProcessResult(process.ExitCode == 0, outputBuilder.ToString(), process.ExitCode);
        }
        catch (Exception ex)
        {
            return new ProcessResult(false, ex.Message, -1);
        }
    }

    public async Task<IReadOnlyCollection<string>> RunPowerShellListAsync(string command, CancellationToken cancellationToken = default)
    {
        var result = await RunAsync("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"", cancellationToken);
        if (!result.Success)
        {
            return Array.Empty<string>();
        }

        return result.Output
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
    }

    private static async Task ReadStreamAsync(StreamReader reader, StringBuilder builder, CancellationToken token)
    {
        while (!reader.EndOfStream && !token.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync();
            if (line is null)
            {
                break;
            }
            builder.AppendLine(line);
        }
    }
}


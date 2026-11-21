using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Rfkunck.Core;

public sealed record TelegramProxySession(Uri ConnectionLink, Task<int> ExitCode);

public sealed class TelegramProxy(string executablePath)
{
    private readonly string executable = Path.GetFullPath(executablePath);
    private readonly SemaphoreSlim lifecycle = new(1, 1);
    private Process? child;
    private Task<int>? exitCode;

    public async Task<TelegramProxySession> StartAsync(
        int port,
        bool disableCfTls = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(port, 1024);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, 65535);

        await lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (child is { HasExited: false })
                throw new InvalidOperationException("The Telegram proxy is already running.");

            await StopCurrentAsync().ConfigureAwait(false);

            if (!File.Exists(executable))
                throw new FileNotFoundException("The TG WS Proxy CLI executable is missing.", executable);

            var proxyInfo = new ProcessStartInfo(executable)
            {
                WorkingDirectory = Path.GetDirectoryName(executable)!,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            proxyInfo.ArgumentList.Add("--host");
            proxyInfo.ArgumentList.Add("127.0.0.1");
            proxyInfo.ArgumentList.Add("--port");
            proxyInfo.ArgumentList.Add(port.ToString(CultureInfo.InvariantCulture));
            if (disableCfTls)
                proxyInfo.ArgumentList.Add("--no-secure");

            var ready = new TaskCompletionSource<Uri>(TaskCreationOptions.RunContinuationsAsynchronously);
            var linkPattern = new Regex($@"tg://proxy\?server=127\.0\.0\.1&port={port}&secret=dd[0-9a-f]{{32}}\b");
            var process = new Process { StartInfo = proxyInfo };
            process.OutputDataReceived += ReceiveLine;
            process.ErrorDataReceived += ReceiveLine;

            try
            {
                if (!process.Start())
                    throw new InvalidOperationException("Unable to start TG WS Proxy.");

                child = process;
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                exitCode = ObserveExitCodeAsync(process);

                await Task.WhenAny(ready.Task, exitCode)
                    .WaitAsync(TimeSpan.FromSeconds(15), cancellationToken)
                    .ConfigureAwait(false);

                if (ready.Task.IsCompleted)
                {
                    Uri link = await ready.Task.ConfigureAwait(false);
                    if (!process.HasExited)
                        return new TelegramProxySession(link, exitCode);
                }

                int code = await exitCode.ConfigureAwait(false);
                throw new InvalidOperationException($"TG WS Proxy exited before it became ready (code {code}).");
            }
            catch
            {
                if (child == process)
                    await StopCurrentAsync().ConfigureAwait(false);
                else
                    process.Dispose();
                throw;
            }

            void ReceiveLine(object _, DataReceivedEventArgs received)
            {
                if (received.Data is not string line)
                    return;

                Match link = linkPattern.Match(line);
                if (link.Success)
                {
                    ready.TrySetResult(new Uri(link.Value));
                }
                else if (line.Contains("[Errno 10048]", StringComparison.Ordinal) ||
                         line.Contains("[WinError 10048]", StringComparison.Ordinal) ||
                         line.Contains("address already in use", StringComparison.OrdinalIgnoreCase))
                {
                    ready.TrySetException(new InvalidOperationException($"Local proxy port {port} is already in use."));
                }
            }
        }
        finally
        {
            lifecycle.Release();
        }
    }

    public async Task StopAsync()
    {
        await lifecycle.WaitAsync().ConfigureAwait(false);
        try
        {
            await StopCurrentAsync().ConfigureAwait(false);
        }
        finally
        {
            lifecycle.Release();
        }
    }

    private async Task StopCurrentAsync()
    {
        if (child is not Process process)
            return;

        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException) when (process.HasExited)
        {
            // The child exited between checking its status and asking it to stop.
        }

        await (exitCode ?? ObserveExitCodeAsync(process)).WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        process.Dispose();
        child = null;
        exitCode = null;
    }

    private static async Task<int> ObserveExitCodeAsync(Process process)
    {
        await process.WaitForExitAsync().ConfigureAwait(false);
        return process.ExitCode;
    }
}

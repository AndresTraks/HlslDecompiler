using NUnit.Framework;
using System;
using System.Diagnostics;

namespace HlslDecompiler.Tests;

/// <summary>
/// Holds one Wine client open for the whole run: Wine tears the prefix down when
/// its last client exits, and restarting it costs ~10x an fxc call. `wine cmd`
/// waiting on stdin is used because, unlike wineserver, it is always reachable.
/// </summary>
[SetUpFixture]
public class WinePrefixWarmUp
{
    private static Process _client;

    [OneTimeSetUp]
    public void HoldThePrefixOpen()
    {
        if (OperatingSystem.IsWindows() || RecompileTests.FxcPath == null)
        {
            return;
        }
        try
        {
            _client = Process.Start(new ProcessStartInfo("wine")
            {
                ArgumentList = { "cmd" },
                // Never written to, so cmd waits.
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            });
            // Drained so a full pipe cannot block it.
            _client.OutputDataReceived += (_, _) => { };
            _client.ErrorDataReceived += (_, _) => { };
            _client.BeginOutputReadLine();
            _client.BeginErrorReadLine();
        }
        catch (Exception)
        {
            // A speedup only.
            _client = null;
        }
    }

    [OneTimeTearDown]
    public void LetThePrefixGo()
    {
        try
        {
            // The whole tree: Wine puts a start.exe in front of cmd.
            if (_client is { HasExited: false })
            {
                _client.Kill(entireProcessTree: true);
                _client.WaitForExit(milliseconds: 5000);
            }
            _client?.Dispose();
        }
        catch (Exception)
        {
            // Exited between the check and the kill.
        }
        _client = null;
    }
}

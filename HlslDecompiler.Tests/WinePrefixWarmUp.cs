using NUnit.Framework;
using System;
using System.Diagnostics;

namespace HlslDecompiler.Tests;

/// <summary>
/// Keeps the Wine prefix warm for the length of a run, by holding one client open in
/// it from the first test to the last.
///
/// Off Windows every fxc call is `wine fxc.exe`, and Wine tears the prefix down once
/// its last client has gone. Decompiling a shader and writing it out is a long enough
/// gap for that to happen, so a run that calls fxc one shader at a time pays a full
/// prefix start every single time. Measured on this corpus: 306ms for a call with a
/// gap in front of it, 24ms for one made while a client is alive. fxc itself is the
/// 24ms. The rest was starting Wine again, three thousand times over.
///
/// Running the tests several at once hides it, because one of them nearly always has
/// a client alive - which is why the Recompile tier went from seven minutes to twenty
/// seconds on nothing but a second worker. That speedup is worth keeping and is not a
/// reason to leave the cause in place: a filtered run of a single test, which is how
/// a fix gets checked while it is being written, has nothing to overlap with.
///
/// `cmd` waiting on a pipe nothing is written to is what holds it. The documented way
/// is `wineserver -p`, and wineserver is not reliably on PATH - on this machine only
/// `wine` is, and the server lives somewhere under /usr/lib that it is not this
/// file's business to know. `cmd` is in every prefix.
/// </summary>
[SetUpFixture]
public class WinePrefixWarmUp
{
    private static Process _client;

    [OneTimeSetUp]
    public void HoldThePrefixOpen()
    {
        // Nothing to hold open where fxc runs natively, and no reason to hold it
        // where fxc was not found at all - the tiers that would call it skip
        // themselves.
        if (OperatingSystem.IsWindows() || RecompileTests.FxcPath == null)
        {
            return;
        }
        try
        {
            _client = Process.Start(new ProcessStartInfo("wine")
            {
                ArgumentList = { "cmd" },
                // Never written to, so cmd waits on it rather than running anything.
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            });
            // Drained, not merely redirected: a pipe nobody reads fills up and stops
            // the process writing into it. There is nothing to do with the lines.
            _client.OutputDataReceived += (_, _) => { };
            _client.ErrorDataReceived += (_, _) => { };
            _client.BeginOutputReadLine();
            _client.BeginErrorReadLine();
        }
        catch (Exception)
        {
            // A speedup, not a dependency: a run without it is slow, not broken.
            _client = null;
        }
    }

    [OneTimeTearDown]
    public void LetThePrefixGo()
    {
        try
        {
            // The whole tree: Wine starts a `start.exe` of its own in front of cmd.
            if (_client is { HasExited: false })
            {
                _client.Kill(entireProcessTree: true);
                _client.WaitForExit(milliseconds: 5000);
            }
            _client?.Dispose();
        }
        catch (Exception)
        {
            // Gone of its own accord between the check and the kill. Either way
            // there is nothing left to do about it.
        }
        _client = null;
    }
}

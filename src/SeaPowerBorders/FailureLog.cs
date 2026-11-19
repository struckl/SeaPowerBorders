using System;

namespace SeaPowerBorders;

/// <summary>
/// Logs the first failure of a part that runs every frame and keeps quiet after that: the same exception on every
/// frame would bury the log.
/// </summary>
internal sealed class FailureLog
{
    private readonly string what;
    private bool logged;

    public FailureLog(string what) => this.what = what;

    public void Report(Exception ex)
    {
        if (logged)
            return;
        logged = true;
        ModLog.Error($"{what} failed: {ex}");
    }
}

/// <summary>A wall-clock gate for work that should run at most every so many milliseconds.</summary>
internal struct Throttle
{
    private int next;
    private bool started;

    public bool Ready(int milliseconds)
    {
        int now = Environment.TickCount;
        if (started && now - next < 0)
            return false;
        started = true;
        next = now + milliseconds;
        return true;
    }
}

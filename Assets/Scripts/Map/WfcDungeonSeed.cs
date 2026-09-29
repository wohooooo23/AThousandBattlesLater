using System;

/// <summary>Wall-clock seeds belong at the UI boundary, never inside deterministic generation.</summary>
public static class WfcDungeonSeed
{
    private static readonly object Gate = new object();
    private static long lastTimestamp = long.MinValue;

    public static int Next()
    {
        lock (Gate)
        {
            // Whole UTC Unix seconds preserve the existing int seed API. Increment within
            // one second so rapid clicks (or a clock moving backwards) do not repeat a map.
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            lastTimestamp = Math.Max(now, lastTimestamp + 1);
            return unchecked((int)lastTimestamp);
        }
    }
}

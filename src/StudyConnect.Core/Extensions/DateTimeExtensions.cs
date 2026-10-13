using System.Globalization;

namespace StudyConnect.Core.Extensions;

/// <summary>Extension methods for presenting dates in the UI.</summary>
public static class DateTimeExtensions
{
    /// <summary>
    /// Converts a UTC timestamp into a short relative description
    /// such as "just now", "5m ago", "3h ago", "yesterday" or "12 Mar".
    /// </summary>
    /// <param name="utc">The timestamp (UTC).</param>
    /// <param name="nowUtc">Optional "current" time, mainly for unit tests.</param>
    public static string ToRelativeTime(this DateTime utc, DateTime? nowUtc = null)
    {
        DateTime now = nowUtc ?? DateTime.UtcNow;
        TimeSpan elapsed = now - utc;

        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        return elapsed switch
        {
            { TotalSeconds: < 45 } => "just now",
            { TotalMinutes: < 60 } => $"{(int)elapsed.TotalMinutes}m ago",
            { TotalHours: < 24 } => $"{(int)elapsed.TotalHours}h ago",
            { TotalDays: < 2 } => "yesterday",
            { TotalDays: < 7 } => $"{(int)elapsed.TotalDays}d ago",
            _ => utc.ToString("d MMM", CultureInfo.InvariantCulture)
        };
    }

    /// <summary>Formats a local session time, e.g. "Mon 14 Oct, 2:00 PM".</summary>
    public static string ToSessionTime(this DateTime local) =>
        local.ToString("ddd d MMM, h:mm tt", CultureInfo.InvariantCulture);

    /// <summary>Formats a UTC chat timestamp in local time, e.g. "2:05 PM".</summary>
    public static string ToChatTime(this DateTime utc)
    {
        DateTime local = DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime();
        string format = local.Date == DateTime.Today ? "h:mm tt" : "d MMM, h:mm tt";
        return local.ToString(format, CultureInfo.InvariantCulture);
    }
}

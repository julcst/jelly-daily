namespace JellyDaily;

public sealed class ReleaseState
{
    public List<string> Unlocked { get; set; } = [];
    public HashSet<string> Completed { get; set; } = [];
    public Dictionary<string, DateOnly> CompletedOn { get; set; } = [];
    public DateOnly? LastReleaseDay { get; set; }
}

public static class ReleaseEngine
{
    // A local calendar boundary, rather than elapsed 24-hour periods, survives DST.
    public static DateOnly Day(DateTimeOffset now, string zone, TimeOnly releaseTime)
    {
        var local = TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(zone));
        var day = DateOnly.FromDateTime(local.DateTime);
        return TimeOnly.FromDateTime(local.DateTime) < releaseTime ? day.AddDays(-1) : day;
    }

    public static int Release(ReleaseState state, IReadOnlyList<string> ordered, DateOnly day,
        int count = 1, bool bonus = false, bool bypassWatch = false)
    {
        if (count is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(count));
        // Never catch up, including after clock rollback or a timezone change.
        if (!bonus && state.LastReleaseDay is { } last && day <= last) return 0;
        var latest = state.Unlocked.LastOrDefault();
        if (!bypassWatch && latest != null && !state.Completed.Contains(latest)) return 0;
        if (!bonus && latest != null && state.CompletedOn.TryGetValue(latest, out var finished) && day <= finished) return 0;
        var next = ordered.Where(x => !state.Unlocked.Contains(x)).Take(count).ToArray();
        if (next.Length == 0) return 0;
        state.Unlocked.AddRange(next);
        // A bonus consumes today's opportunity, but never tomorrow's.
        if (state.LastReleaseDay is null || day > state.LastReleaseDay) state.LastReleaseDay = day;
        return next.Length;
    }
}

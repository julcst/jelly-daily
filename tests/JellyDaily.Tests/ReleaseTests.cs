using Xunit;
namespace JellyDaily.Tests;
public sealed class ReleaseTests
{
    private static readonly DateOnly Monday = new(2026, 10, 12);
    private static readonly string[] Episodes = ["1:1", "1:2", "1:3", "1:4", "1:5"];
    [Fact] public void MissedDaysNeverAccumulate()
    {
        var s = new ReleaseState();
        Assert.Equal(1, ReleaseEngine.Release(s, Episodes, Monday));
        Assert.Equal(0, ReleaseEngine.Release(s, Episodes, Monday.AddDays(4)));
        s.Completed.Add("1:1");
        Assert.Equal(1, ReleaseEngine.Release(s, Episodes, Monday.AddDays(4)));
        Assert.Equal(["1:1", "1:2"], s.Unlocked);
    }
    [Fact] public void CompletionNeverReleasesImmediately()
    {
        var s = new ReleaseState(); ReleaseEngine.Release(s, Episodes, Monday); s.Completed.Add("1:1");
        Assert.Equal(0, ReleaseEngine.Release(s, Episodes, Monday));
        Assert.Equal(1, ReleaseEngine.Release(s, Episodes, Monday.AddDays(1)));
    }
    [Fact] public void BonusRequiresLatestAndConsumesOnlyToday()
    {
        var s = new ReleaseState(); ReleaseEngine.Release(s, Episodes, Monday);
        Assert.Equal(0, ReleaseEngine.Release(s, Episodes, Monday, 2, true));
        Assert.Equal(2, ReleaseEngine.Release(s, Episodes, Monday, 2, true, true));
        s.Completed.Add("1:2");
        Assert.Equal(0, ReleaseEngine.Release(s, Episodes, Monday.AddDays(1)));
        s.Completed.Add("1:3");
        Assert.Equal(0, ReleaseEngine.Release(s, Episodes, Monday));
        Assert.Equal(1, ReleaseEngine.Release(s, Episodes, Monday.AddDays(1)));
    }
    [Fact] public void RestartDoesNotDuplicateRelease()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid()+"/state.json");
        try {
            var store = new StateStore(path); var state = new ReleaseState(); ReleaseEngine.Release(state, Episodes, Monday);
            store.Save(new() { ["child/series"] = state }); var restored = new StateStore(path).Load()["child/series"];
            restored.Completed.Add("1:1"); Assert.Equal(0, ReleaseEngine.Release(restored, Episodes, Monday));
        } finally { Directory.Delete(Path.GetDirectoryName(path)!, true); }
    }
    [Fact] public void CorruptStateFailsClosed()
    {
        var path = Path.GetTempFileName();
        try { File.WriteAllText(path, "broken"); Assert.Throws<System.Text.Json.JsonException>(() => new StateStore(path).Load()); }
        finally { File.Delete(path); }
    }
    [Theory]
    [InlineData("2026-03-29T04:30:00Z", "2026-03-29")]
    [InlineData("2026-10-25T05:30:00Z", "2026-10-25")]
    [InlineData("2026-10-25T04:30:00Z", "2026-10-24")]
    public void LocalCalendarBoundaryHandlesDst(string now, string expected) =>
        Assert.Equal(DateOnly.Parse(expected), ReleaseEngine.Day(DateTimeOffset.Parse(now), "Europe/Berlin", new TimeOnly(6, 0)));
    [Fact] public void LateCompletionWaitsUntilNextReleaseDay()
    {
        var s = new ReleaseState(); ReleaseEngine.Release(s, Episodes, Monday);
        s.Completed.Add("1:1"); s.CompletedOn["1:1"] = Monday.AddDays(4);
        Assert.Equal(0, ReleaseEngine.Release(s, Episodes, Monday.AddDays(4)));
        Assert.Equal(1, ReleaseEngine.Release(s, Episodes, Monday.AddDays(5)));
    }
    [Fact] public void ClockRollbackDoesNotGrant()
    {
        var s = new ReleaseState(); ReleaseEngine.Release(s, Episodes, Monday); s.Completed.Add("1:1");
        Assert.Equal(0, ReleaseEngine.Release(s, Episodes, Monday.AddDays(-1)));
    }
    [Fact] public void RescanOrderingDoesNotRelockExistingGrant()
    {
        var s = new ReleaseState(); ReleaseEngine.Release(s, Episodes, Monday); s.Completed.Add("1:1");
        ReleaseEngine.Release(s, ["1:0", ..Episodes], Monday.AddDays(1));
        Assert.Contains("1:1", s.Unlocked); Assert.Contains("1:0", s.Unlocked);
    }
}

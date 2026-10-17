using System.Collections.Concurrent;
using Jellyfin.Data;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Enums;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;

namespace JellyDaily;

public interface IReleaseAccess
{
    bool IsChild(Guid user);
    Task<Dictionary<string, ReleaseState>> SyncAsync(Guid? bonusUser = null, Guid? bonusSeries = null, int count = 1, bool bypass = false, bool reset = false);
    bool IsLocked(Guid user, Guid itemId, Dictionary<string, ReleaseState> state);
}

public sealed class ReleaseService : IReleaseAccess, IDisposable
{
    private readonly ILibraryManager library;
    private readonly IUserManager users;
    private readonly IUserDataManager data;
    private readonly StateStore store;
    private readonly SemaphoreSlim gate = new(1);
    private readonly ConcurrentDictionary<(Guid User, Guid Item), DateTimeOffset> completions = new();
    private const string Prefix = "jelly-daily:";
    public ReleaseService(ILibraryManager library, IUserManager users, IUserDataManager data, IApplicationPaths paths)
    {
        this.library = library; this.users = users; this.data = data;
        store = new StateStore(Path.Combine(paths.DataPath, "jelly-daily", "state.json"));
        data.UserDataSaved += OnUserDataSaved;
    }
    private void OnUserDataSaved(object? sender, UserDataSaveEventArgs e)
    {
        if (e.Item is not Episode episode || !IsChild(e.UserId) ||
            !Plugin.Instance.Configuration.Rules.Any(r => r.UserId == e.UserId && r.SeriesId == episode.SeriesId)) return;
        var threshold = Plugin.Instance.Configuration.CompletionPercent;
        if (e.UserData.Played || (episode.RunTimeTicks > 0 &&
            (double)e.UserData.PlaybackPositionTicks / episode.RunTimeTicks.Value * 100 >= threshold))
            completions.TryAdd((e.UserId, episode.Id), DateTimeOffset.UtcNow);
    }
    public void Dispose() => data.UserDataSaved -= OnUserDataSaved;

    public static string Key(SeriesRule rule) => $"{rule.UserId:N}/{rule.SeriesId:N}";
    public static string EpisodeKey(Episode episode) => $"{episode.ParentIndexNumber}:{episode.IndexNumber}";
    private Episode[] Episodes(Guid series) => library.GetItemList(new InternalItemsQuery {
        IncludeItemTypes = [BaseItemKind.Episode], Recursive = true, AncestorIds = [series]
    }).OfType<Episode>().Where(x => x.ParentIndexNumber > 0 && x.IndexNumber > 0)
      .OrderBy(x => x.ParentIndexNumber).ThenBy(x => x.IndexNumber).ToArray();

    public async Task<Dictionary<string, ReleaseState>> SyncAsync(Guid? bonusUser = null,
        Guid? bonusSeries = null, int count = 1, bool bypass = false, bool reset = false)
    {
        await gate.WaitAsync();
        try
        {
            var config = Plugin.Instance.Configuration;
            if (!double.IsFinite(config.CompletionPercent) || config.CompletionPercent <= 0 || config.CompletionPercent > 100)
                throw new InvalidOperationException("Completion threshold must be between 0 and 100.");
            var day = ReleaseEngine.Day(DateTimeOffset.UtcNow, config.TimeZone, TimeOnly.Parse(config.ReleaseTime));
            var state = store.Load();
            var rules = config.Rules.DistinctBy(Key).Where(r =>
                users.GetUserById(r.UserId) is { } u && !u.HasPermission(PermissionKind.IsAdministrator)).ToArray();
            var items = new Dictionary<Guid, Episode[]>();
            foreach (var rule in rules)
            {
                var user = users.GetUserById(rule.UserId) ?? throw new InvalidOperationException("Selected child does not exist.");
                if (user.HasPermission(PermissionKind.IsAdministrator)) throw new InvalidOperationException("Administrators cannot be selected as children.");
                if (library.GetItemById(rule.SeriesId) is not Series) throw new InvalidOperationException("Selected series does not exist.");
                var episodes = items.TryGetValue(rule.SeriesId, out var cached) ? cached : items[rule.SeriesId] = Episodes(rule.SeriesId);
                if (episodes.GroupBy(EpisodeKey).Any(g => g.Count() > 1)) throw new InvalidOperationException("Ambiguous episode numbering; resolve duplicate episodes first.");
                if (!state.TryGetValue(Key(rule), out var progress)) state[Key(rule)] = progress = new();
                foreach (var episode in episodes)
                {
                    var watched = data.GetUserData(user, episode);
                    var fromEvent = completions.TryGetValue((rule.UserId, episode.Id), out var observed);
                    if (fromEvent || watched?.Played == true || (episode.RunTimeTicks > 0 && watched != null &&
                        (double)watched.PlaybackPositionTicks / episode.RunTimeTicks.Value * 100 >= config.CompletionPercent))
                        {
                        var episodeKey = EpisodeKey(episode);
                        if (progress.Completed.Add(episodeKey)) progress.CompletedOn[episodeKey] = fromEvent
                            ? ReleaseEngine.Day(observed, config.TimeZone, TimeOnly.Parse(config.ReleaseTime)) : day;
                    }
                }
                bool manual = rule.UserId == bonusUser && rule.SeriesId == bonusSeries;
                if (manual && reset)
                {
                    progress = new ReleaseState { LastReleaseDay = day };
                    state[Key(rule)] = progress;
                }
                else if (manual || !rule.Paused)
                    ReleaseEngine.Release(progress, episodes.Select(EpisodeKey).ToArray(), day,
                        manual ? count : 1, manual, manual && bypass);
            }
            // Commit before exposing grants: a crash cannot repeat today's release.
            store.Save(state);
            // Event observations are removed only after their completion has been persisted.
            foreach (var observation in completions.ToArray())
                if (library.GetItemById(observation.Key.Item) is Episode observedEpisode &&
                    rules.Where(r => r.UserId == observation.Key.User && r.SeriesId == observedEpisode.SeriesId)
                         .Any(r => state[Key(r)].Completed.Contains(EpisodeKey(observedEpisode))))
                    completions.TryRemove(observation.Key, out _);
            foreach (var user in users.GetUsers())
            {
                string tag = Prefix + user.Id.ToString("N");
                bool child = rules.Any(r => r.UserId == user.Id);
                var old = user.GetPreference(PreferenceKind.BlockedTags);
                var updated = old.Where(x => !x.Equals(tag, StringComparison.OrdinalIgnoreCase))
                    .Concat(child ? [tag] : Array.Empty<string>()).ToArray();
                if (!old.SequenceEqual(updated)) { user.SetPreference(PreferenceKind.BlockedTags, updated); await users.UpdateUserAsync(user); }
            }
            // Remove only our own tags, including stale rules. Never alter other metadata.
            foreach (var episode in library.GetItemList(new InternalItemsQuery { IncludeItemTypes = [BaseItemKind.Episode], Recursive = true }).OfType<Episode>())
            {
                var tags = episode.Tags.Where(x => !x.StartsWith(Prefix, StringComparison.Ordinal)).ToList();
                foreach (var rule in rules.Where(r => r.SeriesId == episode.SeriesId))
                    if (!state[Key(rule)].Unlocked.Contains(EpisodeKey(episode))) tags.Add(Prefix + rule.UserId.ToString("N"));
                if (!episode.Tags.SequenceEqual(tags))
                {
                    episode.Tags = tags.ToArray();
                    await library.UpdateItemAsync(episode, episode.GetParent(), ItemUpdateType.MetadataEdit, CancellationToken.None);
                }
            }
            return state;
        }
        finally { gate.Release(); }
    }

    public bool IsChild(Guid user) => users.GetUserById(user) is { } u &&
        !u.HasPermission(PermissionKind.IsAdministrator) && Plugin.Instance.Configuration.Rules.Any(r => r.UserId == user);
    public bool IsLocked(Guid user, Guid itemId, Dictionary<string, ReleaseState> state)
    {
        if (library.GetItemById(itemId) is not Episode episode) return false;
        return Plugin.Instance.Configuration.Rules.Where(r => r.UserId == user && r.SeriesId == episode.SeriesId)
            .Any(r => !state.TryGetValue(Key(r), out var p) || !p.Unlocked.Contains(EpisodeKey(episode)));
    }
}

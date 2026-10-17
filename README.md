# Daily Episodes for Jellyfin

Native C# plugin targeting **Jellyfin 12.2.0 / .NET 10**. Experimental MVP: per-child, per-series sequential releases, watch prerequisites, manual bonus releases, persistent progress, and a Jellyfin dashboard page.

**Do not rely on this as a production parental-control boundary yet.** See [API investigation and limitations](docs/access-control.md). A successful build and scheduler tests do not establish enforcement on every client or streaming route.

## Release rules

* When a child/series rule is first enabled, one numbered episode becomes available. Previously watched episodes are not automatically granted.
* Normal releases grant at most one episode per series per local release day, ordered by season then episode number. No missed-day accumulation.
* The latest granted episode must reach the configured completion threshold (default 90%) or be marked played by Jellyfin. Completion is sticky. The next grant waits until a later release day than both the previous grant and the observed completion.
* Progress is independent per child and series. Already granted episodes remain available for rewatching.
* Manual bonuses grant 1–100 episodes. The administrator must explicitly select **Release even if … unwatched** to bypass the prerequisite. Bonuses consume today's opportunity and require completion of the latest bonus episode before the next normal release; they do not consume tomorrow's quota.
* Pause prevents automatic grants, without revoking existing ones. Reset relocks the selected series, retains Jellyfin watch history, and suppresses an automatic grant until the next release day. Remove a rule to end restrictions for that child/series.
* Season zero and unnumbered episodes remain locked in controlled series. Duplicate season/episode numbers fail validation. Resolve alternate versions before enabling scheduling.

The dashboard is under **Dashboard → Plugins → Daily Episodes → Settings**. Select a non-administrator child, select a series, add a rule, and save. Defaults: UTC, 06:00, 90%. Choose an IANA timezone such as `Europe/Berlin`. Scheduling reconciles every minute, at startup, and before controlled child controller requests.

## Build and test

Install a .NET 10 SDK, then:

```sh
./scripts/build.sh
```

The installable assembly is `artifacts/plugin/JellyDaily.dll`. Only copy this DLL; Jellyfin supplies its own API dependencies. For a disposable real-server integration test (requires Docker, Python 3 and ffmpeg):

```sh
./scripts/smoke-test.sh
```

It creates a temporary Jellyfin 12.2.0 instance, tiny generated episodes and temporary accounts; it removes that instance afterward. Never point the Python test directly at an existing server.

The automated tests cover release timing, bonuses, missed days, DST, restart persistence, corruption, direct-request guard behavior, and adult isolation.

Docker build (no local SDK required):

```sh
docker build --target artifact --output type=local,dest=artifacts/docker .
```

This exports `artifacts/docker/JellyDaily.dll`; it does not build or install a Jellyfin server.

## Install from the plugin catalog

After the repository/tag is published, add this URL in **Dashboard → Plugins → Repositories**:

```text
https://raw.githubusercontent.com/julcst/jelly-daily/main/manifest.json
```

Open the catalog, install **Daily Episodes**, and restart Jellyfin. The manifest points at a version-tagged ZIP and includes the MD5 required by Jellyfin's installer. `releases/SHA256SUMS` provides a separate SHA-256 checksum. The GitHub release workflow uploads the same verified ZIP and manifest as release assets.

## Install on TrueNAS / Docker

1. Back up Jellyfin's persistent configuration and database. Confirm the server is exactly 12.2.0; other releases are unverified.
2. Stop the Jellyfin app/container using TrueNAS Apps or your container manager.
3. In the host dataset mounted as Jellyfin's `/config`, create `plugins/DailyEpisodes_0.2.0.0/` and copy `JellyDaily.dll` there. The resulting container path should be `/config/plugins/DailyEpisodes_0.2.0.0/JellyDaily.dll`. Host dataset paths vary; use your app's actual `/config` mount, rather than an image's ephemeral filesystem. Preserve the container UID/GID's read/write access.
   When upgrading from 0.1.0.0, replace the old plugin DLL directory so only one copy of `JellyDaily.dll` is installed. Keep the plugin configuration and `jelly-daily/state.json`; the plugin ID and state format are unchanged.
4. Start Jellyfin. Check logs for plugin-loading errors, then open the settings page.
5. Use a disposable non-administrator child account first. Keep child passwords and administrator credentials separate. Disable child remote control of other users and download permissions in Jellyfin's user policy.
6. Configure a test series, then perform the access checks in [the validation guide](docs/access-control.md). Test your actual Jellyfin clients, including Streamyfin, before relying on restrictions.

The state file lives at `<Jellyfin DataPath>/jelly-daily/state.json` (normally `/config/data/jelly-daily/state.json` in the official container); the plugin configuration is managed by Jellyfin. Back up both alongside the Jellyfin database. State writes use a flushed temporary file and atomic replacement; malformed state fails closed rather than resetting releases. A missing state file is treated as first installation: deleting it deliberately resets progress.

For removal, first remove all rules in plugin settings and save, allowing synchronization to remove plugin-owned blocked tags and metadata tags. Then stop Jellyfin and delete the DLL directory. Removing the DLL first can leave locked tags behind. Existing non-plugin tags and permissions are retained.

## Scope and remaining work

Implemented MVP controls: child/series selection, timezone/release time, watch threshold, pause/resume, reset, manual multi-episode bonus, and raw release/completion progress.

Not implemented: daily screen-time accounting or playback termination, recurring bonus schedules, shared quotas, autoplay control, custom episode ordering, dedicated skip controls, and a computed next-release timestamp. There is no screen-time limit in this version. Client-side buffering, offline downloads made before restrictions, or direct filesystem access cannot be revoked by this plugin.

Episode grants use series ID plus season/episode numbers so ordinary episode ID changes do not reset progress. Deleting/recreating the series changes its ID and requires administrator review; renumbering episodes can reassign a grant. Do not renumber or merge controlled series without reviewing progress. Watched-state synchronization is conservative after downtime: the next release waits until a later day than when completion is first observed.

Initial experimental MVP for Jellyfin 10.11.11 / .NET 9.

Includes per-child, per-series sequential daily releases, watch gating, manual multi-episode bonuses, pause/resume/reset, persistent progress and dashboard controls. Blocked tags hide locked episodes; an authenticated server-side action filter denies direct requests.

Validated with automated tests and a disposable Jellyfin 10.11.11 server: child browsing, direct detail/playback-info/static stream/HLS manifest/download denial, unrestricted adult playback, child E1 playback, admin-only bonuses and persisted grants after restart.

Daily screen-time limits, recurring bonuses and shared quotas are deferred. Streamyfin and every streaming/subtitle/remote-control route have not been exhaustively verified. Test on a disposable child account before relying on this for parental controls. See README and docs/access-control.md.

Add this plugin repository URL in Jellyfin Dashboard → Plugins → Repositories:
https://raw.githubusercontent.com/julcst/jelly-daily/main/manifest.json

Then install Daily Episodes from the catalog and restart Jellyfin. For manual installation, extract only JellyDaily.dll into the server's persistent plugins/DailyEpisodes_0.1.0.0/ directory while Jellyfin is stopped.

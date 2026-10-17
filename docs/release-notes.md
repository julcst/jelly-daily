Daily Episodes 0.2.0.0 for Jellyfin 12.2 / .NET 10.

Retargets the plugin and tests to .NET 10 and Jellyfin.Controller / Jellyfin.Model 12.2.0. The catalog ABI is 12.2.0.0. Build automation, Docker build, packaging and the disposable integration test target the new runtime. The smoke test uses the Authorization token header and checks server and plugin versions.

Per-child sequential daily releases, watch gating, manual bonuses, pause/resume/reset and dashboard controls retain their behavior. The plugin ID and state format are unchanged. Stop Jellyfin, replace the old plugin DLL directory with plugins/DailyEpisodes_0.2.0.0/, and retain the plugin configuration and jelly-daily/state.json. Install only one copy of JellyDaily.dll.

Validation: Release build succeeds without warnings; all 18 automated tests pass. The disposable Jellyfin 12.2.0 smoke test passed plugin loading, child restrictions, adult playback, bonuses, restart persistence, reset and rule removal. See docs/access-control.md for coverage limits.

Daily screen-time limits, recurring bonuses and shared quotas remain deferred. Streamyfin and every streaming/subtitle/remote-control route have not been exhaustively verified.

After publishing the v0.2.0.0 tag/package, add this repository URL in Jellyfin Dashboard → Plugins → Repositories:
https://raw.githubusercontent.com/julcst/jelly-daily/main/manifest.json

Then install Daily Episodes from the catalog and restart Jellyfin.

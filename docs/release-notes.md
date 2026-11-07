Daily Episodes 2.0.1 for Jellyfin 12.2 / .NET 10.

Fixes empty GUID handling in access checks and configured rules. Empty user claims and optional empty item, parent and user identifiers no longer reach lookups that reject empty GUIDs. Invalid rules with empty user or series identifiers are skipped. Locked episodes remain blocked when a request includes both empty and locked identifiers.

The plugin ID and state format are unchanged. Restart Jellyfin after upgrading. For manual installations, replace the old plugin directory with plugins/DailyEpisodes_2.0.1/ and retain the plugin configuration and jelly-daily/state.json. Install only one copy of JellyDaily.dll.

Validation: all 24 automated tests pass, including empty-GUID regression tests. Package contents and MD5/SHA-256 checksums verified.

This remains an experimental MVP. Daily screen-time limits, recurring bonuses and shared quotas remain deferred. See docs/access-control.md for coverage limits.

Plugin repository URL:
https://raw.githubusercontent.com/julcst/jelly-daily/main/manifest.json

# Access-control investigation and validation

## Investigated upstream version

This port targets Jellyfin stable tag `v12.2` (NuGet packages `12.2.0`, server ABI `12.2.0.0`) and .NET 10. The original implementation targeted 10.11.11 / .NET 9.

Relevant source, pinned to that tag:

* [`BaseItem.IsParentalAllowed` / `IsVisibleViaTags`](https://github.com/jellyfin/jellyfin/blob/v12.2/MediaBrowser.Controller/Entities/BaseItem.cs): compares item/inherited tags with the user's `BlockedTags` preference.
* [`InternalItemsQuery`](https://github.com/jellyfin/jellyfin/blob/v12.2/MediaBrowser.Controller/Entities/InternalItemsQuery.cs): translates blocked user tags into query exclusions.
* [`StreamingHelpers.GetStreamingState`](https://github.com/jellyfin/jellyfin/blob/v12.2/Jellyfin.Api/Helpers/StreamingHelpers.cs): fetches the requested item without a user argument. Blocked tags alone are insufficient evidence of direct-stream denial.
* [`IPluginServiceRegistrator`](https://github.com/jellyfin/jellyfin/blob/v12.2/MediaBrowser.Controller/Plugins/IPluginServiceRegistrator.cs) and [`ApiServiceCollectionExtensions`](https://github.com/jellyfin/jellyfin/blob/v12.2/Jellyfin.Server/Extensions/ApiServiceCollectionExtensions.cs): permit plugin service registration and MVC controller discovery.

## Chosen MVP mechanism

Each child gets a distinct `jelly-daily:<user-id>` blocked tag. Only locked episodes receive that child's tag. Adults have no corresponding blocked preference and keep ordinary access. Administrator accounts are always excluded even if promoted after a rule was configured. No media files are moved or deleted. Changes go to Jellyfin metadata/database; only this plugin's namespaced tags are reconciled.

A global MVC action filter reads the **authenticated principal**, never a client-selected user identity. Before child controller requests, it reconciles state/metadata, denies cross-user `userId` overrides, and returns 403 when a bound GUID argument (including `itemId`, `id`, or `mediaSourceId`) identifies a locked episode. State/configuration failures return 503 for controlled children. Scheduled tasks also reconcile on startup and at one-minute intervals.

This is an explicit server-side playback guard, not just UI hiding. However, coverage must be verified against real server routes. Non-MVC endpoints, opaque media-source IDs, nested request-body identifiers, remote session commands, cached stream URLs, and future API changes need separate auditing. Blocked tags must also be verified on every browse/detail endpoint of interest. Cached client metadata may remain visible. Do not assert production acceptance criterion 1 solely from unit tests.

The smallest stronger alternative if any route escapes enforcement is a Jellyfin server patch that applies authenticated item visibility centrally before **every** streaming, download, subtitle, image, and metadata response. A reverse proxy would require a maintained route-aware authorization service and is not silently substituted here.

## Validation of the 12.2 port

The Release build against Jellyfin.Controller and Jellyfin.Model 12.2.0 succeeded with no warnings; all 18 automated tests passed on .NET 10.

The disposable `jellyfin/jellyfin:12.2` smoke test confirmed server version 12.2.0 and loaded plugin version 0.2.0.0. It passed child browse filtering; locked detail, playback-info, static stream, HLS manifest and download denial; cross-user override denial; adult and unlocked child playback; admin-only bonuses; watch completion without a same-day release; restart persistence; reset; and access restoration after rule removal. The test now supplies its token in the Authorization header.

This validates representative server routes. Actual Jellyfin Web and Streamyfin UI interactions, all subtitle/segment/remote-control paths, and the SDK Docker build were not tested in this port.

## Historical validation on 10.11.11

On a disposable official `jellyfin/jellyfin:10.11.11` container with generated H.264/AAC episodes:

* The plugin loaded successfully.
* A child episode query returned only E1. Locked E2 detail, playback-info, static stream, HLS master manifest and download requests returned 403.
* Adult E2 static playback and child E1 static playback succeeded. A child could not override the effective user or call admin bonus endpoints.
* Watch-state events recorded completion without granting another episode on the same day. An admin bonus granted two episodes. All grants survived a server restart.

This covers representative paths, not every client, subtitle endpoint, HLS segment type or remote-control request. The reproducible smoke test is in `scripts/smoke-test.sh`.

These results describe the original 10.11.11 build; see the release notes for validation of the 12.2 port. The smoke-test script now selects `jellyfin/jellyfin:12.2`.

## Required live-server checks

Use a disposable server, three or more tiny numbered episodes in one series, a non-admin child, and an unrestricted adult. Never use administrator API keys to simulate a child: those requests have different authority.

1. Add a child/series rule. Verify the child can list and play E1, cannot list E2/E3, and receives 403 for direct E2 detail, playback-info, static stream, HLS manifest/segments, download and subtitle routes. Attempt known-ID URLs directly. Verify adult requests succeed.
2. Try overriding `userId` with an adult's ID while authenticated as the child. Verify denial. Try alternate versions, media-source IDs and remote-control routes.
3. Mark E1 played. Verify no same-day E2 release. At the following local release boundary verify exactly E2 unlocks. Keep E2 unwatched for multiple days; verify E3 stays locked.
4. Manually release two episodes with prerequisite bypass. Verify both unlock and existing episodes remain playable. Verify normal releases remain blocked until the latest granted episode is completed and a later eligible day arrives.
5. Restart Jellyfin. Verify grants persist and no extra episode is released. Rescan the library, checking both tags and playback denial again.
6. Repeat with actual Jellyfin Web and Streamyfin clients, direct play and transcoding. Verify child downloads and control of adult sessions are disabled in user policy.

Daily screen-time acceptance criteria are explicitly deferred. No playback termination or cross-device time accounting is claimed.

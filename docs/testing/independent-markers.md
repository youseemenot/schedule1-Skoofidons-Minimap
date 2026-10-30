# Independent markers: verification and performance acceptance

## Current status — 2026-10-30

- Release version: **2.2.8**, assembly/file version **2.2.8.0**. Behavior and appearance match the accepted **2.2.8-dev.3** build.
- User-confirmed target game: **Schedule I 0.4.7f12**, MelonLoader 0.7.3 and Phone App 2.2.4.
- User screenshots/feedback confirm map texture, NPC portraits, clock and amber dealer appearance. The supplied dev.2 log has no minimap errors.
- **Co-op has not been tested in game.** Host/guest movement, disconnect/reconnect and edge behavior remain pending.
- The complete closed-phone/reload acceptance matrix and quantitative FPS/frame-time/memory comparison remain pending; visual approval is not a benchmark.
- Offline checks: 18/18 regression tests, Release build with zero warnings/errors, and metadata API check passed.
- User-tested installed dev.3 SHA256: `31AAAD05C1EA75ADB7E785CEED1CECFE21AF9C2CF18EF8BD1242AB2FF2E75993`. The subsequent inner-background change was cancelled at the user's request; keep the accepted dev.3 appearance. Release preparation changes version metadata and documentation only; the installed DLL is not replaced.

## Candidate design

- NPC discovery uses a postfix on `NPCPoI.SetNPC`, before phone UI is needed. References are retained through Main-scene initialization and cleared on session exit.
- NPC coordinates come from the same world POI Transform used by the game's `POI.UpdatePosition`. Portraits use the current interop property `NPC.MugshotSprite` and the game's `IconContainer`, falling back to that container in the serialized UIPrefab before the phone has opened. No phone app is activated, and no portrait camera or texture renderer is added.
- The actual 0.4.7f12 NPC and potential-customer UI prefabs both contain a RectTransform-only IconContainer with Shade, Outline/Icon and Fill children. Native `NPCPoI.InitializeUI` calls Transform.Find with `Outline/Icon` before assigning the portrait. The candidate uses that exact child, preserving decorative images and masks.
- Remote players come from `Player.PlayerList`; `Player.Local`/`IsLocalPlayer` excludes the local player. Green dots and edge clamping/opacity are retained. Registry reconciliation starts every 0.5 seconds and processes at most two entries per frame.
- Sampling targets are at most 10 Hz for NPCs and 20 Hz for co-op. Fair queues inspect at most eight NPC and four co-op entries per frame. Under load these limits intentionally reduce freshness instead of performing a catch-up burst.
- Discovery/sampling shares a **soft** 0.35 ms budget, checked between operations, and at most one icon creation attempt per frame. A native call/Instantiate cannot be interrupted; this is not a hard upper bound on frame time. Co-op dot rendering and deferred Unity Canvas rebuilding also require measurement.
- NPC binding callbacks do a fixed amount of identity/source setup when the game binds an NPC. These event-time lookups are not included in the per-frame sampling budget; include initial loading and NPC spawn batches in runtime measurements.
- No global scene search, NPC name scan, regex scan, new police scan, or additional network messages. Lists, identity maps, marker instances and the co-op dot sprite are reused. Disabled categories do not sample their sources.
- F4 prints aggregate tracking CPU statistics before the existing diagnostic scan. Do not repeatedly press F4 during frame-time measurement: the existing diagnostic scan and its logging are additional work. Its CPU counters exclude deferred Unity rendering and do not prove total game allocation behavior.

## Automated checks

Run `dotnet run --project tests/TrackingPolicy.Tests/TrackingPolicy.Tests.csproj -c Release`.

The executable links the production queue/projection source, covering cadence, bounded searches, fairness, disabled categories, no catch-up bursts, identity changes, removal/reconnection, reload and coordinate projection. It does not simulate Harmony detours, native IL2CPP lifetime or Unity visual output.

Build the real adapter with `dotnet build SniccupMiniMap.csproj -c Release -p:GameDirectory=<game directory>`. For a sandbox or checkout with read-only old obj files, redirect `BaseIntermediateOutputPath` and `OutputPath` into a writable workspace folder, using the same intermediate path for restore and build.

The first candidate used older reference DLLs; the user test on regenerated interop reported a missing `NPC.GetMessagingIcon` method. The corrected candidate resolves Unity, loader and game references from the same installed generation and uses `NPC.MugshotSprite`. `tools/Test-GameApi.ps1` fails the old candidate with the exact missing member and passes the corrected one. The current user test exercised NPC tracking after this correction; full lifecycle and co-op coverage remains pending.

## Manual acceptance (not performed by the source-only task)

Use a disposable test session and the same resolution, graphics, minimap settings, save, route, weather/time and police-radar state for baseline 2.2.7 and candidate. Allow both to warm up, then repeat the route rather than comparing unrelated areas. Test NPC-only, co-op-only and both enabled so a regression can be attributed.

1. Load a save without ever opening the phone. NPC portraits must match the built-in map when subsequently compared, including frame/mask/proportions. Verify dealers and potential-customer filters, no workers/unknown NPCs introduced, no duplicate icons, correct position at several distant locations and zoom levels.
2. Walk with the phone down; open messages/products and close the phone. NPC markers must keep moving. Check idle NPCs, NPC visibility transitions and travel in a vehicle.
3. Test as host and as guest. A moving partner is one green dot; the local arrow remains distinct. Confirm edge clamp/opacity, nav rotation, zoom, join, leave and reconnect.
4. Disable each category and the whole minimap, re-enable them, return to menu, load the same save, then a different save. No stale views, growing marker counts, duplicate callbacks or repeated errors.
5. Compare average frame time, p95/p99, 1% lows, long-frame counts and memory over repeated runs of at least 10 minutes (covering the historical 5/15-second stalls). Include the busiest NPC area and co-op join/leave. Record baseline variation before setting a numerical regression tolerance; no performance result has been measured yet.
6. Reject the candidate if it introduces periodic stalls, sustained degradation outside baseline variation, growing memory/marker counts or repeated native exceptions. Compile success alone is not acceptance.

The user subsequently authorized installing the candidate with a verified backup, replacing only `Mods/SkoofidMinimap.dll`. No game launch or changes to other files are authorized by that installation request.

## Source review

A separate read-only reviewer found three P2 lifecycle issues in the first candidate: permanent NPC loss after transient exceptions, stale portraits when Unity had already destroyed the previous owner, and visible views after rebinding into a disabled category. Each received a failing regression test before its fix; all 15 offline tests then passed, and Release rebuilt with zero warnings/errors. Valid NPC sources now retry after a one-second cooldown; retained owner identity drives portrait invalidation; binding immediately applies category visibility.

The reviewer explicitly left Harmony delivery, visual mask/layout fidelity, world-POI activation semantics, first-load hierarchy, host/guest lifecycle and measured FPS/memory impact for runtime acceptance. They are not claimed as verified by the source-only task.

## 2026-10-30 user-test failure and dev.2 correction

The user's screenshots show a blank map circle, missing NPC portraits and a clock left at its placeholder. Their log identifies the loaded candidate SHA256 `C337DDB8EA243BC2AB06D2A142A3700AA1232C686A7AD2A9E5EC51E773EB0BA2` and repeated `Method not found: NPC.GetMessagingIcon()` warnings. Building the same sources against the regenerated installed assembly reproduces CS1061; the old compiled DLL also fails the new metadata API regression check.

The correction uses `NPC.MugshotSprite`, resolves map resources through `MapApp.Instance` with `Phone.Instance`/`GameplayMenu.Instance` inactive-child fallbacks, and retries missing sprite/controller resources every 0.5 seconds. It removes the assumed `Player_Local/CameraContainer/Camera/OverlayCamera/GameplayMenu` ancestry. The clock reads `TimeManager.Instance` directly once per second. No scene enumeration, UI activation or new texture rendering is introduced.

Serialized asset evidence confirms `MapApp.ContentRect` and `PoIContainer` point to the same RectTransform (sharedassets0.assets MapApp fields at offsets 92/104 both reference fileID 0, pathID 82869), preserving the original static POI scaling/container.

The user subsequently confirmed these visible fixes on 0.4.7f12. Quantitative performance and co-op acceptance remain pending.

## Dealer border (dev.3)

Dealer NPCs in the dealer POI category receive an amber (#FFB347) circle using the existing cloned Shade image. Customers and non-dealer NPCs retain their source styling. The original portrait/mask hierarchy is reused; no new textures, cameras or per-frame styling work is added.

The original Outline Mask is reparented inside that outer circle and inset, keeping the portrait clipped to a smaller circle. This produces a complete ring even with square mugshot textures, without adding UI components. The clip inset targets 1.5 physical pixels using the outer circle's effective transform scale in the ScreenSpaceOverlay canvas. Zoom/settings changes apply it immediately; a once-per-second canvas-scale comparison handles resolution/UI scaling changes. At extreme zoom-out the inset is capped at one quarter of the parent diameter so the portrait remains visible.

Manual check: compare a recruited dealer and potential customer at minimum/normal/maximum map zoom and after changing resolution. Confirm the amber ring stays thin, the portrait remains circular and customer styling/green co-op dots are unchanged.

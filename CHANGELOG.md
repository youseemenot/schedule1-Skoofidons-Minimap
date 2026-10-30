# Changelog

All notable changes to Skoofidon's Minimap are documented in this file.

## [2.2.8-dev.3] - 2026-10-30

### Added

- Independent NPC portrait tracking using world POI references and the game's mugshot sprites and icon templates.
- Amber dealer highlighting with geometry compensated for map zoom and canvas scaling; the current appearance was approved in the user's game test.
- 18 offline regression tests for scheduling, work limits, filtering, lifecycle recovery, projection and border scaling.
- A read-only metadata check for obsolete direct game API references.

### Fixed

- Map texture initialization through the game-owned map controller, including inactive-phone hierarchy fallbacks and resource retries.
- Portrait compatibility with the regenerated `NPC.MugshotSprite` API on Schedule I `0.4.7f12`.
- Clock updates now read the game time manager directly instead of depending on phone UI text.
- Transient NPC marker failures retain their source and retry after a cooldown. Rebinding replaces stale portraits and immediately applies category visibility.

### Changed

- Co-op positions now use the game player registry instead of phone-map marker order; partners retain green dots with edge clamping and opacity.
- Coordinate sampling uses bounded fair queues, targets up to 10 Hz for NPCs and 20 Hz for co-op, and avoids global scene searches and catch-up bursts.
- NPC icon creation is limited to one attempt per frame. Visuals, buffers and co-op dots are reused; the existing police scanner is preserved.
- Build target is .NET 6 for the MelonLoader IL2CPP host. Unity, loader and game references resolve from the installed game rather than older copied assemblies.

### Verification status

- Target/user-tested game: **Schedule I `0.4.7f12`**, MelonLoader `0.7.3`, Mod Manager & Phone App `2.2.4`.
- User screenshots and feedback confirm the map texture, NPC portraits, clock and accepted dealer appearance. The supplied dev.2 log contains no minimap errors.
- **Co-op is not yet verified in game:** host/guest movement, join/leave and reconnect remain pending.
- Automated checks: 18/18 tests, Release build with zero warnings/errors, current direct game API metadata check passes.
- Quantitative FPS/frame-time and long-run memory benchmarks remain pending. The CPU budget is soft and does not guarantee zero impact.
- This remains a development version, not a fully verified stable release.

## [2.2.7] - 2026-08-18

### Compatibility

- Added support for Schedule I `0.4.6f13` on the default IL2CPP branch.
- Updated the required MelonLoader version to `0.7.3`.
- Verified optional integration with Mod Manager & Phone App `2.2.4`.

### Fixed

- Fixed the F3 settings UI after the Schedule I update.
- Removed incompatible `GUI.DrawTexture` calls that caused `Method unstripping failed` errors.
- Prevented repeated IMGUI exceptions from flooding the MelonLoader log.
- Fixed duplicate background coroutines after returning to the main menu or reloading a save.
- Cleared stale scene objects, POIs, and radar markers during scene changes.
- Stopped minimap background processing while the minimap is disabled.
- Improved diagnostics for keyboard controls and Phone App integration.

### Performance

- Added time-budgeted incremental police scanning.
- Converted the F4 diagnostic scanner to an incremental scan.
- Cached marker components and repeated player calculations.
- Reused scan buffers and coroutine wait instructions.
- Improved POI lookup and cleanup.
- Debounced preference file writes while dragging F3 sliders.

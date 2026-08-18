# Changelog

All notable changes to Skoofidon's Minimap are documented in this file.

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

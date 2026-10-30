# Independent NPC and co-op markers

The user approved implementation after the investigation in this chat.

- NPCs retain the built-in map portraits and icon layout. Co-op partners retain green dots. The local player's existing arrow remains unchanged.
- Dynamic positions and initial discovery must work without opening the phone map, including after switching phone applications and reloading a save.
- Use game-owned NPC/POI and player references, with cached views and bounded work. Do not introduce recurring scene searches, name matching, forced phone opening, per-frame registry enumeration, or repeated texture creation.
- Preserve the existing police scanner and static POI behavior. Remove duplicate NPC/co-op UI markers from the old synchronization path.
- Limit coordinate sampling and creation work, reuse buffers, stop processing disabled categories, and remove stale scene references. Performance must be measured in game before claiming no regression.
- Initial source-only scope kept all installed game files read-only. The user later authorized replacing only the minimap DLL with a backup, and on 2026-10-30 authorized a local Git commit. Other game files remain untouched; game launch and publishing are not authorized. Build/test outputs stay in the workspace.

## Approach

Use explicit game references for marker identity and position. Retain the game's NPC icon container; move only the mod's copy. Co-op uses Player.Local/IsLocalPlayer rather than UI order. A small independently testable scheduler bounds sampling and reconciliation work. Native UI and registry calls are confined to the Unity adapter.

Keeping the phone map running would retain the unwanted lifecycle dependency. Full-scene discovery repeats the historical source of periodic stalls. Both are excluded.

## Verification

Automated tests cover sampling rate, bounded work, removal/reconnection, filtering, and projection. A Release build checks the real interop API. Manual acceptance compares baseline and candidate on the same game version, route, settings, and save: frame-time distribution, periodic stalls, memory growth, phone closed from first load, NPC portraits, co-op join/leave, and save reload.

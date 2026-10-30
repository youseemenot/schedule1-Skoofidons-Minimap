# Skoofidon's Minimap

A configurable minimap and real-time radar mod for **Schedule I**.

Skoofidon's Minimap keeps the most useful information from the in-game phone map visible while you play. It can display points of interest, co-op players, and nearby police, and includes controls for the map's size, zoom, opacity, position, clock, and player marker.

> Current version: **2.2.8**.
>
> This release adds world-position tracking independent of the phone map. The user has checked the map texture, NPC portraits, clock and dealer appearance on **Schedule I 0.4.7f12**. **Co-op has not yet been tested in game.** FPS/frame-time benchmarking remains pending. See [acceptance checks](docs/testing/independent-markers.md).

## Features

- Always-visible minimap with smooth player tracking
- Optional navigation mode that rotates the map with the player
- In-game time display
- Configurable 2D markers for:
  - orders
  - dealers
  - customers
  - dead drops
  - properties
  - owned vehicles
  - co-op players
- Amber dealer highlighting, compensated for map zoom and canvas scale; customer portraits retain the original style
- Real-time police radar
- Incremental, time-budgeted radar scanning to reduce frame-time spikes
- Adjustable minimap size, zoom, opacity, and screen position
- Adjustable clock position and scale
- Customizable player-arrow size and color
- Settings are saved between sessions
- Optional integration with **Mod Manager & Phone App**

## Requirements

- **Schedule I** on PC — default IL2CPP branch
- Game version `0.4.7f12`
- [MelonLoader 0.7.3](https://github.com/LavaGang/MelonLoader/releases/tag/v0.7.3)

MelonLoader 0.7.2 is not compatible with the current Schedule I IL2CPP build.

**Mod Manager & Phone App is optional.** Version `2.2.4` has been tested with this release. The minimap has its own in-game settings menu and works without the Phone App.

Version 2.2.8 is compiled against the installed game's regenerated interop/Unity/loader assemblies and uses `NPC.MugshotSprite`. Phone App `2.2.4` was present during the user's single-player check. Release 2.2.7 targeted game `0.4.6f13`; see the changelog for that historical baseline. Co-op host/guest behavior is still unverified.

## Building

Use a .NET SDK with .NET 6 targeting references and pass the read-only game directory to resolve IL2CPP/Harmony build references:

```powershell
dotnet build SniccupMiniMap.csproj -c Release '-p:GameDirectory=S:\SteamLibrary\steamapps\common\Schedule I'
dotnet run --project tests/TrackingPolicy.Tests/TrackingPolicy.Tests.csproj -c Release
./tools/Test-GameApi.ps1 -ModPath '<candidate DLL>' -GameDirectory 'S:\SteamLibrary\steamapps\common\Schedule I'
```

The offline regression executable requires the .NET 10 SDK. Build output stays in this checkout; there is no deployment target. Only `SkoofidMinimap.dll` is the mod artifact. Game interop references are not copied to the output or distributed.

The metadata API check runs in PowerShell 7 and reads the candidate plus the current `Assembly-CSharp.dll` without executing game/mod code. It catches removed direct game-member references; the full current-reference build is still needed for signatures, generic/inherited API calls and Unity types.

## Installation

1. Install MelonLoader 0.7.3 for Schedule I.
2. Download the latest `SkoofidMinimap.dll` from the [Releases](https://github.com/youseemenot/schedule1-Skoofidons-Minimap/releases) page.
3. Copy `SkoofidMinimap.dll` into the game's `Mods` folder:
   ```text
   ...\Steam\steamapps\common\Schedule I\Mods
   ```
4. Launch the game.

If the `Mods` folder does not exist, start the game once after installing MelonLoader or create the folder manually.

## Controls

| Key | Action |
| --- | --- |
| `F3` | Open or close the minimap settings |
| `F4` | Run the incremental diagnostic scanner and write results to the MelonLoader log |

## Configuration

Open the settings panel with `F3`. You can enable or disable individual marker groups and adjust:

- minimap size
- map zoom
- opacity
- minimap X/Y position
- clock X/Y position and scale
- player-arrow size and RGB color
- navigation mode
- co-op player markers
- police radar

Changes are saved automatically through MelonLoader preferences.

## Updating

Replace the old `SkoofidMinimap.dll` in the `Mods` folder with the file from the newest release. Your saved preferences should remain available.

## Troubleshooting

### No mods load after updating Schedule I

Update MelonLoader to version `0.7.3`. MelonLoader `0.7.2` cannot generate the required IL2CPP interop assemblies for Schedule I `0.4.6f13`.

### The Phone App does not appear

Make sure you are using Mod Manager & Phone App `2.2.4` or a compatible newer version. Older versions reference game types that were removed in the Schedule I update. The minimap's built-in F3 settings remain available without the Phone App.

### The minimap does not appear

- Confirm that MelonLoader starts when the game launches.
- Confirm that `SkoofidMinimap.dll` is directly inside the `Mods` folder.
- Press `F3` and make sure **Minimap Enabled** is turned on.
- Check `MelonLoader\Latest.log` in the game directory for errors.

### Markers are missing

Release 2.2.7 synchronizes some 2D markers from the phone map. The development candidate obtains NPC positions from world POIs and co-op positions from the player registry. NPC portraits use the game's icon template and messaging portrait. Static POIs retain the original synchronization path; police tracking retains its separate scanner. If NPC binding cannot initialize, the candidate reports a warning and preserves the old phone NPC path rather than disabling the entire map.

### The layout is off-screen

Open the `F3` menu and reset the minimap or clock offsets toward `0`.

When reporting a problem, include your game version, MelonLoader version, mod version, and the relevant section of `MelonLoader\Latest.log`.

## Credits and inspiration

Skoofidon's Minimap was inspired by the functional minimap mod created by **Hiccup**. It was the example through which we first learned how minimap modding for Schedule I worked. Building on that experience, we created our own mod and developed it into a significantly more capable minimap with a much broader feature set.

- **Youseemenot** — Skoofidon's Minimap creator and developer
- **Hiccup** — original inspiration and learning reference

This is an unofficial community mod and is not affiliated with TVGS or the developers of Schedule I.

## Source and releases

- [Source code](https://github.com/youseemenot/schedule1-Skoofidons-Minimap)
- [Latest releases](https://github.com/youseemenot/schedule1-Skoofidons-Minimap/releases)
- [Issues and bug reports](https://github.com/youseemenot/schedule1-Skoofidons-Minimap/issues)

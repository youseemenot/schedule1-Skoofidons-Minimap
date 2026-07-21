# Skoofidon's Minimap

A configurable minimap and real-time radar mod for **Schedule I**.

Skoofidon's Minimap keeps the most useful information from the in-game phone map visible while you play. It can display points of interest, co-op players, and nearby police, and includes controls for the map's size, zoom, opacity, position, clock, and player marker.

> Current mod version: **2.2.5**

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
- Real-time police radar
- Adjustable minimap size, zoom, opacity, and screen position
- Adjustable clock position and scale
- Customizable player-arrow size and color
- Settings are saved between sessions
- Optional integration with **Mod Manager & Phone App**

## Requirements

- **Schedule I** on PC
- [MelonLoader](https://github.com/LavaGang/MelonLoader)

**Mod Manager & Phone App is optional.** The minimap has its own in-game settings menu and works without it.

## Installation

1. Install MelonLoader for Schedule I.
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
| `F4` | Run the diagnostic scanner and write results to the MelonLoader log |

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

### The minimap does not appear

- Confirm that MelonLoader starts when the game launches.
- Confirm that `SkoofidMinimap.dll` is directly inside the `Mods` folder.
- Press `F3` and make sure **Minimap Enabled** is turned on.
- Check `MelonLoader\Latest.log` in the game directory for errors.

### Markers are missing

Some 2D markers are synchronized from the in-game phone map. Open the phone map once and allow a moment for the markers to refresh. Police tracking uses a separate real-time scanner.

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

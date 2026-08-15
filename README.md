# Stonewards X-Ray

[![Release](https://img.shields.io/badge/release-v1.1.0-blue)](https://github.com/sawetco/Stonewards-XRay/releases/tag/v1.1.0)
[![License](https://img.shields.io/github/license/sawetco/Stonewards-XRay)](LICENSE)

Stonewards X-Ray is a client-side visual mod for [Stonewards](https://store.steampowered.com/app/4502710/Stonewards/) (Steam App ID 4502710) by sawet. It highlights chests and useful discoverable objects through terrain and provides optional terrain hiding for full X-Ray visibility.

## Features

- Highlights chests and useful discoverable objects through terrain.
- Optional terrain hiding for full X-Ray visibility.
- Separate Chest, Discoverables, and combined target modes.
- Adjustable reveal distance from 15m to 100m.
- Adjustable target opacity from 25% to 100%.
- Overlay localization based on the language selected in Stonewards.
- No continuous full-world target scanning.

## Controls

| Key | Action |
| --- | --- |
| `X` | X-Ray on/off |
| `F5` | Terrain on/off |
| `F6` | Change target mode |
| `F7` | Change distance |
| `F8` | Change opacity |
| `F9` | Overlay on/off |

Defaults: **Chests**, **100m**, **25% opacity**.

### Target modes

- **Chests** — chests and treasure chests.
- **Discoverables** — lore, pets, pickups, item barrels, minerals, and other collectible items.
- **Chests + Discoverables** — both groups together.

Structural objects, hazards, enemies, lamps, and generic destructible props are intentionally excluded in v1.1.0. Support for additional target categories is planned for future releases.

## Languages

English, French, German, Spanish (Spain), Simplified Chinese, Traditional Chinese, Japanese, Portuguese (Portugal), and Russian.

## Installation

1. Download the ZIP from the [latest release](https://github.com/sawetco/Stonewards-XRay/releases/latest).
2. Close Stonewards (if running).
3. Extract the ZIP next to `Stonewards.exe`.
4. Run `Install-Stonewards-XRay.cmd`.
5. Start the game normally through Steam.

The installer downloads and verifies BepInEx 5.4.23.5 x64 when required, then compiles the plugin locally against the assemblies shipped with Stonewards.

## Usage

Press `X` to enable or disable the mod. With X-Ray enabled, selected targets are highlighted while the normal terrain remains visible.

Press `F5` to hide or restore terrain without disabling target highlighting.

Changing the target mode with `F6` refreshes the target list. Distance and opacity changes use the already cached targets.

## Multiplayer

Stonewards X-Ray is implemented as a client-side visual game mod, not a gameplay trainer. It does not intentionally modify inventory, drops, damage, movement, player stats, or network state.

Multiplayer servers and communities may have their own rules regarding visual mods. Check the applicable rules before using it in public multiplayer sessions.

## Uninstall

Run `Uninstall-Stonewards-XRay.cmd`. BepInEx is left installed so other BepInEx plugins are not affected.

## License

MIT. See [LICENSE](LICENSE).

Stonewards X-Ray is an unofficial community mod and is not affiliated with or endorsed by the Stonewards developers or publisher.

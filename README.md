# Stonewards X-Ray by sawet

Client-side X-Ray and discovery helper for **Stonewards**.

## Features

- Reveal chests and discoverables through terrain.
- Toggle terrain X-Ray without disabling target highlights.
- Adjustable target distance and opacity.
- Optional name tags.
- No continuous world scanning.

## Controls

| Key | Action |
| --- | --- |
| `F5` | Activate / deactivate mod |
| `X` | X-Ray on/off |
| `F6` | Target mode |
| `F7` | Name tags on/off |
| `F8` | Opacity |
| `F9` | Distance |
| `F10` | Overlay on/off |

`F6` cycles through **Chests**, **Chests + Discoverables**, and **All**.

`F8` cycles through **100%**, **75%**, **50%**, and **25%** opacity. Default: **75%**.

`F9` cycles through **15m**, **30m**, **50m**, **75m**, **100m**, and **Unlimited**. Default: **30m**.

## Requirements

- Windows x64
- Stonewards installed through Steam
- Internet connection during first-time installation if BepInEx is not already installed

## Installation

1. Download the ZIP from the [latest release](https://github.com/sawetco/Stonewards-XRay/releases/latest).
2. Extract it next to `Stonewards.exe`.
3. Close Stonewards if it is running.
4. Run `Install-Stonewards-XRay.cmd`.
5. Start the game normally through Steam.

The installer downloads and verifies BepInEx 5.4.23.5 x64 when needed, then compiles the plugin locally against the assemblies shipped with Stonewards.

## Usage

Press `F5` to activate the mod. Target highlights are shown while terrain remains visible.

Press `X` to hide or restore terrain rendering without disabling the mod. Press `F5` again to restore normal rendering and disable all mod features.

Activating the mod refreshes the target list. Changing the target mode with `F6` also refreshes it.

## Multiplayer

The plugin is client-side and only changes local rendering and target visualization. It does not intentionally modify inventory, drops, damage, or network state.

Compatibility with future game versions or multiplayer rules is not guaranteed.

## Uninstall

Run `Uninstall-Stonewards-XRay.cmd`. BepInEx is left installed so other BepInEx plugins are not affected.

## License

MIT. See [LICENSE](LICENSE).

Stonewards X-Ray by sawet is an unofficial community mod and is not affiliated with or endorsed by the Stonewards developers or publisher.

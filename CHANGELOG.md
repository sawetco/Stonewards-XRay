# Changelog

## 1.1.0

- Fixed terrain X-Ray being reset by terrain updates from other players in co-op.
- Reworked controls around `X` as the main X-Ray toggle and `F5` as the terrain toggle.
- Removed name tags and screen-space target labels.
- Replaced the previous target modes with Chests, Discoverables, and Chests + Discoverables.
- Removed structural, hazard, enemy, lamp, and generic destructible targets.
- Removed the unlimited distance option.
- Changed the default target mode to Chests.
- Changed the default reveal distance to 100m.
- Changed the default opacity to 25%.
- Added overlay localization based on the language selected in Stonewards.
- Added English, French, German, Spanish, Simplified Chinese, Traditional Chinese, Japanese, Portuguese, and Russian overlay text.

## 1.0.0

Initial public release.

- Client-side highlighting for chests and discoverable objects.
- Optional terrain X-Ray mode.
- Adjustable reveal distance and target opacity.
- In-game status overlay.
- Event-driven target scanning to avoid periodic frame hitches.
- Automatic BepInEx installation and local plugin compilation.

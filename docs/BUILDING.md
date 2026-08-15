# Building

The release installer compiles the plugin against the assemblies from an installed copy of Stonewards and BepInEx.

Required references are listed in `scripts/Install-Stonewards-XRay.ps1`.

The installer supports both layouts:

- Release package: `StonewardsXRayPlugin.cs` next to the installer.
- Repository: `src/StonewardsXRayPlugin.cs` with the installer under `scripts/`.

Game assemblies are not redistributed by this repository.

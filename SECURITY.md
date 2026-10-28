# Security policy

Please disclose installation or download-verification vulnerabilities privately
to the maintainer, using GitHub private vulnerability reporting if available.

The installer downloads third-party BepInEx binaries and verifies a pinned
SHA-256 digest. Do not remove or bypass the integrity check.

The automated CI checks PowerShell syntax without running the installer or game.
A full plugin compilation requires Stonewards/BepInEx assemblies from a
legitimate local game installation; these are not redistributed in CI.

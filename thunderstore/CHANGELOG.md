# Changelog

## 0.1.1

- Fixed fast-start trigger reliability on new games by avoiding a single-frame timing dependency.
- Added clearer startup/apply diagnostic logs for troubleshooting.
- Added error guard around fast-start apply flow to surface runtime exceptions in BepInEx logs.
- Re-saved `thunderstore/README.md` as UTF-8 without BOM for Thunderstore upload validation.

## 0.1.0

- Initial Thunderstore package.
- Custom fast-start logic at game start for new saves only.
- Configurable tech and item grants for combat/non-combat mode.

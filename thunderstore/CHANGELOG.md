# Changelog

## 0.1.2

- Fixed new-game detection when DSP internally calls `LoadCurrentGame` during new save creation.
- Added guarded context handling so pending new-game flow is not overwritten by load callbacks.
- Confirmed fast-start items now apply on actual new game starts.

## 0.1.1

- Fixed fast-start trigger reliability on new games by avoiding a single-frame timing dependency.
- Added clearer startup/apply diagnostic logs for troubleshooting.
- Added error guard around fast-start apply flow to surface runtime exceptions in BepInEx logs.
- Re-saved `thunderstore/README.md` as UTF-8 without BOM for Thunderstore upload validation.

## 0.1.0

- Initial Thunderstore package.
- Custom fast-start logic at game start for new saves only.
- Configurable tech and item grants for combat/non-combat mode.

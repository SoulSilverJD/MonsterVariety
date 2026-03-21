# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.7.3] - 2026-03-21

### Fixed
- Wrong type in asset initial load

## [0.7.2] - 2026-03-20

### Changed
- The Id field is removed

### Fixed
- Maybe fix another null problem

## [0.7.1] - 2026-02-10

### Fixed
- Add special case for skeleton mages

## [0.7.0] - 2025-11-19

### Added
- New item query `mushymato.MonsterVariety_MONSTER_DROPS`
- `AlwaysOverride` is now a model that individually controls custom monster class / custom texture checking. Previous bool values still accepted.

### Changed
- Dangerous monsters no longer default to normal varieties.

## [0.6.0] - 2025-09-28

### Added
- New field `LightProps` attach light source to variety

## [0.5.0] - 2025-09-22

### Added
- New GSQ `mushymato.MonsterVariety_LUCKY_RANDOM` and `mushymato.MonsterVariety_SYNCED_LUCKY_RANDOM`, versions of `RANDOM` and `SYNCED_RANDOM` allowed to use the player's luck buffs
- New field `HUDNotif` and `HUDNotifIconItem` to spawn a HUD message when a specific variety appears in the location

### Fixed
- NRE on Season check
- Stacked item drops should drop multiple

## [0.4.0] - 2025-03-25

### Added

- Hardcoded a list of vanilla monster sprites for better detection
- New feature ExtraDrops on monsters

## [0.3.0] - 2025-03-23

### Added

- Rework content pack structure for better compatibility, this is a breaking change

## [0.2.0] - 2025-03-11

### Added

- Avoid overriding texture on custom monsters (e.g. FTM), add AlwaysOverride option to allow override

## [0.1.0] - 2025-03-08

### Added

- Made mod work.

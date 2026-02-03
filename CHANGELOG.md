# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.1.2]

### Fixed
- Tile in house NRE when no child beds are present.
- Make the clinic map edit really late.

## [1.1.1]

### Fixed
- Adopted NPC without any appearance entries still get their sprites loaded.

## [1.1.0]

### Changed
- Improved compatibility with LittleNPC, Immersive Family, etc. by excluding AdoptedFromNPC kids from the children list.
- New GSQ mushymato.HaveMoreKids_HAS_PLAYER_CHILD, which is like mushymato.HaveMoreKids_HAS_CHILD excluding the AdoptedFromNPC kids.
- Added a C# facing API.

## [1.0.6]

### Fixed
- Another NRE in speaker stuff

## [1.0.5]

### Fixed
- 3rd times the charm with weird dialogue stack empty
- NRE problem with kids having gift tastes without data

## [1.0.4]

### Changed
- Improved compatibility with Free Love to allow multiple cribs to function.
- Altered some logic regarding isdarkskined/not custom kid picking, darkskinned now acts only as priority for spouse kids, but is enforced for shared kids.

### Fixed
- Solo new kid events now have higher priority

## [1.0.3]

### Fixed
- 2nd try at fixing the strange issue with dialogue stack being empty.
- Fix some adoption logic for kid counting, adopted from NPC kids do not count towards Max Child in config.

## [1.0.2]

### Fixed
- Android incompatibility
- Crashloop if kids are outside of farmhouse when sleeping

## [1.0.1]

### Fixed
- No longer attempt to sync children age according to config.
- Maybe fix some strange issue with dialogue stack being empty in the child question event.
- Missing zh translations.

## [1.0.0]

### Added
- It's out for reals!

## [0.13.0] - 2026-01-07

### Added
- Adoption registry changed to children registry, kids can be renamed there.
- GSQ for number of days in age phase.

### Fixed
- Post-festival day return home.

## [0.12.0] - 2025-12-09

### Added
- Dark Shrine of Selfishness that allows only 1 kid to be picked :(.

## [0.11.0] - 2025-11-09

### Added
- NPC mode improvements, kids will now go outside and back home visually.
- Support for festival dialogue on kids.

## [0.10.0] - 2025-10-09

### Added
- Support for Free Love / Polysweet (probably)
- Ensure roaming kids don't explode anyone's saves when doved
- New config allow default child even when custom kids exist

## [0.9.0] - 2025-09-08

### Added
- more spouse dialogue
- various bug fixes that I didn't write down oops

## [0.8.0] - 2025-08-28

### Added
- Custom crib furniture, HMK comes with a built in one that uses `Maps/farmhouse_tiles` directly.
- Multiple children can now be born as twins together
- Add condition for kid unlock

## [0.7.0] - 2025-08-18

### Added
- Child NPC mode, generate an NPC version of the child if they are set to CanSocialize
- Fix various social displays (calendar, social tab)
- Portraiture support
- Refactor various backend assets

## [0.4.0] - 2025-04-02

### Added
- Child talk and gift mode
- Birth trigger action, including support for solo birth

## [0.3.0] - 2025-03-29

### Added

- Some support for player couples, and solo adoption via trigger action.

## [0.2.0] - 2025-03-07

### Added

- Pre-release, mostly works.

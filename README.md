# PvPKit

A V Rising mod that gives the complete Dracula Set with all weapons and accessories to any player who uses the .pvpkit command.

## Changelog

### v0.1.0
- Initial release of the mod

## Installation

### Client
No client installation needed, this is a server-side only mod.

### Server
* Extract _PvPKit.dll_ into _(VRising server folder)/BepInEx/plugins_

## Configuration

The configuration file will be generated after first launch at:
_BepInEx\config\PvPKit.cfg_

```ini
[PvPKit]
# Enable kit command that gives Dracula Set.
EnableKitCommand = true
```

## Commands

`.pvpkit` - Gives you the complete Dracula Set with all weapons and accessories.

Armor set commands:
- `.pvpkit rogue` - Gives you the Dracula Rogue armor set
- `.pvpkit warrior` - Gives you the Dracula Warrior armor set
- `.pvpkit sorcerer` - Gives you the Dracula Scholar armor set
- `.pvpkit brute` - Gives you the Dracula Brute armor set

## Features

- Complete Dracula Set with all weapons and accessories
- Unlimited potions (consumables do not get consumed when used)
- Unlimited siege golems (can be reused)

## Disclaimer

**IMPORTANT**: This mod makes modifications to game prefabs which will permanently affect the server while the mod is active. Specifically:

1. All potions and consumables will have unlimited doses (will not be consumed on use)
2. Drop tables for empty consumables are cleared to prevent empty containers from dropping
3. These changes affect ALL players on the server and ALL instances of these items

It is recommended to use this mod on dedicated servers specifically set up for it, or with awareness of these changes.

## Credits

Thanks to:
- The V Rising modding community
- BepInEx team
- deca for VampireCommandFramework


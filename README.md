# ZLevelsRemastered

Oxide plugin for Rust. Allows player to level up 5 different skills (Mining, Woodcutting, Crafting, Skinning, Aquire).

This plugin, is remastered version of ZLevels plugin, which provides custom skills for players to level up to.

All of the skills works kinda the same like in the original ZLevels, a new skill was introduced called Crafting, which boosts your crafting speed depending on your Crafting skill level, to increase it, you must craft things obviously. The more time you spend on crafting, the more XP you get. Also when player reaches a point, where he has 100% faster crafting, all his items are instant crafted, if you craft more than 10 items at once, then items are Magic Crafted.

The XP loss formula was rewritten, so now you loose percentage of your current level XP instead of some fixed amount if you are level 10 and have 80% exp, and you should loose 60% exp on death, that means you'll be level 10 and 20% experience.
Every hour spent alive, you'll loss less experience when killed. For example if you set XPPercentToLoose to 100%, after 5 hours you'll only loose 50% instead of 100%, hours counts even when you are offline. There is also 10 mins grace period, that when you die within first 10 mins you don't loose any experience. You can check how much XP you'll loose by typing `/stats`.

## Available Skills

* Mining - Gathering all types or ore
* Woodcutting - Gathering wood, cactus
* Skinning - Gathering animal
* Aquire - Collecting _(if disabled splitted over the above types)_
* Crafting

All of the features can be disabled via configuration file.

## Permissions

**Enable Permission** is specifically for **zlevelsremastered.use** only

jackhammer and chainsaw permissions are separate and are always required

zlevelsremastered.jackhammer.allowed - allows players to mine with a jackhammer
zlevelsremastered.chainsaw.allowed - allows players to mine with a chainsaw
zlevelsremastered.weapons.blocked - prevents players from gathering with any weapon
Allow Mining Multiplier On Gibs - this will allow the player get their mining multiplier applied to the yielded amount, and allow them to get experience for it as well. setting this false will disable the multiplier but still allow experience gains.

## The CUI

The CUI allows you to avoid typing `/stats` every second and check how much XP is left till you level up, etc.

## Incompatible plugins

* Hunt RPG for Rust _(to make them work together, you must disable crafting in my plugin settings)_
* CraftingController

## Console Commands

* `zl.info` -- Display info for specified player; supports player name or Steam ID
* `zl.lvl` -- Set levels for specific players, see the FAQ for more info
* `zl.pointsperhit` - Change points per hit for all or specific skills *(not saved to config)*

## Chat Commands

* /stats - displays stats.
* /statsui - toggle's stats interface.
* /statinfo - Displays information about certain skill, including server configuration.

You can disable certain skills, by setting _LevelCaps_ value for that skill to _-1_.
The crafting skill is limited to 20.

The part "**NightBonus**" of the config works only with Time Of Day.

## Configuration

```json
{
  "CUI": {
    "Bounds": {
      "Width Left": "0.725",
      "Width Right": "0.83",
      "Height Lower": "0.02",
      "Height Upper": "0.1225"
    },
    "Xp Bar Colors": {
      "ACQUIRE": "0.4 0 0.8 0.5",
      "CRAFTING": "0 1 0 0.5",
      "MINING": "0 0 1 0.5",
      "SKINNING": "1 0 0 0.5",
      "WOODCUTTING": "1 0.6 0 0.5"
    },
    "Bounds Background": "0.1 0.1 0.1 0.1",
    "CUI Enabled": true,
    "Font Color": "0.74 0.76 0.78 1",
    "FontSize Bar": 11,
    "FontSize Level": 11,
    "FontSize Percent": 11,
    "Text Shadow Enabled": true,
    "Xp Bar Background": "0.2 0.2 0.2 0.2"
  },
  "Functions": {
    "Collectible Entities": {
      "diesel_collectable": true,
      "sulfur-collectable": true,
      "stone-collectable": true,
      "metal-collectable": true,
      "hemp-collectable": true,
      "mushroom-cluster-6": true,
      "mushroom-cluster-5": true,
      "wood-collectable": true,
      "potato-collectable": true,
      "berry-blue-collectable": true,
      "berry-green-collectable": true,
      "berry-red-collectable": true,
      "berry-white-collectable": true,
      "berry-yellow-collectable": true,
      "pumpkin-collectable": true,
      "corn-collectable": true,
      "halloween-wood-collectable": true,
      "halloween-sulfur-collectible": true,
      "halloween-stone-collectable": true,
      "halloween-metal-collectable": true,
      "halloween-bone-collectable": true,
      "berry-black-collectable": true
    },
    "Enable Collectible Pickup": true,
    "Enable Crop Gather": true,
    "Enable Wood Gather": true,
    "Enable Stone Ore Gather": true,
    "Enable Sulfur Ore Gather": true,
    "Enable Metal Gather": true,
    "Enable HQM Gather": true,
    "Allow Mining Multiplier On Gibs": true
  },
  "Generic": {
    "Enable Level Up Broadcast": false,
    "Enable Permission": false,
    "Chainsaw On Gather Permission": "zlevelsremastered.chainsaw.allowed",
    "Jackhammer On Gather Permission": "zlevelsremastered.jackhammer.allowed",
    "Weapons On Gather Permission": "zlevelsremastered.weapons.blocked",
    "gameProtocol": 2356,
    "Penalty Minutes": 10,
    "Penalty On Death": true,
    "Permission Name": "zlevelsremastered.use",
    "Permission Name XP": "zlevelsremastered.noxploss",
    "Player CUI Default Enabled": true,
    "Player Plugin Default Enabled": true,
    "Plugin Prefix": "<color=orange>ZLevels</color>: ",
    "SteamID Icon": 0,
    "Wipe Data OnNewSave": false
  },
  "Night Bonus": {
    "Points Per Hit At Night": {
      "ACQUIRE": 60.0,
      "MINING": 60.0,
      "SKINNING": 60.0,
      "WOODCUTTING": 60.0
    },
    "Points Per PowerTool At Night": {
      "MINING": 60.0,
      "WOODCUTTING": 60.0
    },
    "Resource Per Level Multiplier At Night": {
      "ACQUIRE": 2.0,
      "MINING": 2.0,
      "SKINNING": 2.0,
      "WOODCUTTING": 2.0
    },
    "Enable Night Bonus": false,
    "Broadcast Enabled Bonus": true,
    "Log Enabled Bonus Console": false
  },
  "Settings": {
    "Crafting Details": {
      "Time Spent": 1.0,
      "XP Per Time Spent": 3.0,
      "Percent Faster Per Level": 5.0,
      "Require Permission For Instant Bulk Craft": false,
      "Permission For Instant Bulk Crafting At Max Level": "zlevelsremastered.crafting.instantbulk"
    },
    "Default Resource Multiplier": {
      "ACQUIRE": 1.0,
      "MINING": 1.0,
      "SKINNING": 1.0,
      "WOODCUTTING": 1.0
    },
    "Level Caps": {
      "ACQUIRE": 200.0,
      "CRAFTING": 20.0,
      "MINING": 200.0,
      "SKINNING": 200.0,
      "WOODCUTTING": 200.0
    },
    "Percent Lost On Death": {
      "ACQUIRE": 50.0,
      "CRAFTING": 50.0,
      "MINING": 50.0,
      "SKINNING": 50.0,
      "WOODCUTTING": 50.0
    },
    "No Penalty Zones": [
      "adminzone1",
      "999999"
    ],
    "Points Per Hit": {
      "ACQUIRE": 30.0,
      "MINING": 30.0,
      "SKINNING": 30.0,
      "WOODCUTTING": 30.0
    },
    "Points Per Power Tool": {
      "MINING": 30.0,
      "WOODCUTTING": 30.0
    },
    "Resource Per Level Multiplier": {
      "ACQUIRE": 2.0,
      "MINING": 2.0,
      "SKINNING": 2.0,
      "WOODCUTTING": 2.0
    },
    "Skill Colors": {
      "ACQUIRE": "#7700AA",
      "CRAFTING": "#00FF00",
      "MINING": "#0000FF",
      "SKINNING": "#FF0000",
      "WOODCUTTING": "#FF9900"
    },
    "Starting Stats": {
      "Acquire Level": 1.0,
      "Acquire Points": 10.0,
      "Crafting Level": 1.0,
      "Crafting Points": 10.0,
      "Mining Level": 1.0,
      "Mining Points": 10.0,
      "Skinning Level": 1.0,
      "Skinning Points": 10.0,
      "Woodcutting Level": 1.0,
      "Woodcutting Points": 10.0,
      "XP Multiplier": 100.0
    }
  },
  "Level Up Rewards (Reward * Level = Amount)": {
    "Economics Money": {
      "Acquire": 0.0,
      "Crafting": 0.0,
      "Mining": 0.0,
      "Skinning": 0.0,
      "Woodcutting": 0.0
    },
    "ServerRewards Points": {
      "Acquire": 0.0,
      "Crafting": 0.0,
      "Mining": 0.0,
      "Skinning": 0.0,
      "Woodcutting": 0.0
    },
    "SkillTree XP": {
      "Acquire": 0.0,
      "Crafting": 0.0,
      "Mining": 0.0,
      "Skinning": 0.0,
      "Woodcutting": 0.0
    }
  }
}
```

## FAQ

### How to disable Crafting (or any other skill)?

You can disable it by simply setting it's LevelCap to **-1**.

### Can I change player levels easily?

You can modify players level via RCON/Console, using the following commands:

* `zl.lvl <playername | steamid> <skillshortcutname> <XX>` -- Set player level to XX level.
* `zl.lvl <playername | steamid> <skillshortcutname>   <XX>` -- Increase player level by XX levels.
* `zl.lvl <playername | steamid> <skillshortcutname> -<XX>` -- Decrease player level by XX levels.
* `zl.lvl <playername | steamid> <skillshortcutname> /<XX>` -- Divide player level by XX.
* `zl.lvl <playername | steamid> <skillshortcutname> *<XX>` -- Set player XP rates to XX% of server rates (100 is default).

Instead of names you can use wildcard(*):

* `*` -- affects online players.
* `**` -- affects all players.

* Example: `zl.lvl Player WC /2` -- Player gets his WC level divided by 2.
* Example: `zl.lvl * *  3` -- Everyone currently playing in the server gets  3 for all skills.
* Example: `zl.lvl ** * /2` -- Everyone (including offline players) gets their level divided by 2.

Shortcut codes for skills are:

* **C** - Crafting
* **WC** - Woodcutting
* **M** - Mining
* **S** - Skinning
* **A** - Aquire
* **\*** - All skills

### API

**These hooks are called after Item/ItemAmount is modified.**

```cs
        void OnZLevelDispenserGather(ResourceDispenser dispenser, BasePlayer player, Item item, int prevAmount, int newAmount, bool isPowerTool) { }

        void OnZLevelCollectiblePickup(ItemAmount ia, BasePlayer player, CollectibleEntity ce, int prevAmount, float newAmount) { }

        void OnZLevelGrowableGathered(GrowableEntity ge, Item item, BasePlayer player, int prevAmount, int newAmount) { }
```

## Credits

* **Zeiser**, the author of the original ZLevels plugin
* **Visagalis**, the original author of the plugin
* **Fujicura**, for helping maintain the  plugin
* **Default**, for helping maintain the  plugin
* **nivex**, for helping maintain the plugin

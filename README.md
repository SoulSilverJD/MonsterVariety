# MonsterVariety

Framework mod, allow modders to reskin monsters.

Made this because AT is failing to cover a few edge-cases.

Note: `[CP] Visual Monster Variety` lacks the actual assets since they don't belong to me. Please visual enemy variety on nexus.

## Model

Target `mushymato.MonsterVariety/Data` and add an entry like this:

```json
// The Key should use your mod id, to achieve compatibility.
"{{ModId}}_Armored Bug": {
  // Internal name of the monster, mandatory.
  // If you aren't sure about the name, look for "Try ApplyMonsterVariety on <monster name>" in the trace logs.
  // It's possible for other mods to change this name.
  "MonsterName": "Armored Bug",
  "Varieties": {
    "{{ModId}}_Armored Bug/texture_0": {
      // Load the sprite to this target
      "Sprite": "{{ModId}}_Armored Bug/texture_0",
      // Optional fields
      "Condition": null, // Game State Query
      "Season": null, // Current season, respects the location
      "Precedence": 0, // Order to check in, lower is earlier
      "AlwaysOverride": {
        // Control how textures can override forcifully
        // Besides specifying every field you can also put:
        // - "AlwaysOverride": false (equal to { "CustomMonsterClass": false, "CustomTextures": false, "SpecificTextureName": null })
        // - "AlwaysOverride": true (equal to { "CustomMonsterClass": true, "CustomTextures": true, "SpecificTextureName": null })
        "CustomMonsterClass": false, // Normally only monsters from namespace StardewValley.Monsters get variety, having 'true' here causes custom monster class to be overriden as well
        "CustomTextures": false, // Normally only vanilla textures (a hardcoded list) get variety, having 'true' here causes custom textures to be overriden as well.
        "SpecificTextureName": null // Only override if the current texture name is this value, check monster texture names with lookup anything.
      },
      "HUDNotif": "Message Here", // Optional HUD notif message that will appear when this variety appears
      "HUDNotifIconItem": "(O)QualifiedItemId", // Optional item icon to use for HUD notif message
      "LightProps": "5 Red", // Optional light source to attach, format is "Radius" or "Radius Color"
      "Fields": {
        // Optional Data/Monsters field edits for this variety only, using Content Patcher field indexes or aliases.
        0: 500, // Health
        1: 15 // Damage
      },
      "ExcludeDrops": [ "(O)684" ], // Optional item IDs to exclude from this monster's already-rolled Data/Monsters drops.
      "ExtraDrops": {
        // extra drop items, these are item queries with Condition https://stardewvalleywiki.com/Modding:Item_queries
        "{{ModId}}_ExtraMeat1": {
          "Id": "{{ModId}}_ExtraMeat1",
          "ItemId": "(O)684"
        }
      }
    },
    // This entry is the vanilla appearance, it's treated the samesame as any other variety.
    // You do not have to include it if you wish to completely override this monster's sprites.
    "Default": {
      "Sprite": "Characters/Monsters/Armored Bug"
    }
    // add more varieties as desired
  },
  // shared extra drop, applies to all monsters with this name
  "SharedExtraDrops": {
    // shared extra drop items, these are item queries with Condition https://stardewvalleywiki.com/Modding:Item_queries
   "{{ModId}}_ExtraMeat2": {
      "Id": "{{ModId}}_ExtraMeat2",
      "ItemId": "(O)684"
    }
  },
  // same as Varieties, but for dangerous monsters
  "DangerousVarieties": {
    "{{ModId}}_Armored Bug_dangerous/texture_0": {
        "Sprite": "{{ModId}}_Armored Bug_dangerous/texture_0"
    },
  },
  // like SharedExtraDrops but for dangerous monsters
  "DangerousSharedExtraDrops": {
   "{{ModId}}_ExtraMeat3": {
      "Id": "{{ModId}}_ExtraMeat3",
      "ItemId": "(O)684"
    }
  },
}
```

`mushymato.MonsterVariety/Data` is actually a list, two mods adding varieties to the same monster will appear as 2 different entries so as long as they use unique id. These entries will be merged before used to check what variants should apply.

### Fields

The optional `Fields` block edits monster stats after a variety is chosen. Keys can be `Data/Monsters` field indexes or readable aliases.

Supported fields are `0`/`Health`, `1`/`Damage`, `4`/`IsGlider`, `7`/`Resilience`, `8`/`Jitteriness`, `9`/`MoveTowardPlayerThreshold`, `10`/`Speed`, `11`/`MissChance`, `12`/`MineMonster`, `13`/`ExperienceGained`, and `14`/`DisplayName`. `Health` sets both current and max health, matching monster data parsing.

### ExcludeDrops

The optional `ExcludeDrops` list excludes matching item IDs from the monster's already-rolled `Data/Monsters` drops after a variety is chosen. This only filters the base monster drops that existed before Monster Variety adds shared or variety extra drops.

```json
"ExcludeDrops": [ "(O)684" ]
```

This is intended for variants that should not keep a specific vanilla monster drop, without editing `Data/Monsters` for the whole monster type. It does not patch later rerolls like Burglar's Ring.

## Special Cases

- `Armored Bug` is the MonsterName used for armored bugs in the skull cavern
- `Assassin Bug` is the MonsterName used for assassin bugs in desert festival
- `Skeleton Mage` is the MonsterName used for skeleton mages, who are actually just named `"Skeleton"`
- Slime enemies will fall back to `Green Slime`, if a less specific entry could not be found

## Game State Queries

Monster Variety adds the following game state queries for use in it's `Condition` fields (or anywhere else that accepts game state queries).

### `mushymato.MonsterVariety_LUCKY_RANDOM <Rate> [optional @ modifiers]`
### `mushymato.MonsterVariety_SYNCED_LUCKY_RANDOM <Interval> <Key> <Rate> [optional @ modifiers]`

These are quite similar to `RANDOM` and `SYNCED_RANDOM` but in addition to `@addDailyLuck`, you can also use `@addPlayerLuck [ratio]` to add some multiple of player luck level to the calculation.

The number in `[ratio]` is a percentage, i.e. if you put 75 then player luck level will be multiplied by 0.75 which is 75%.

To have this be interpreted as is, write the number as `.75` where `.` is the first character and the multplier will be exactly `.75`.

## Item Queries

Monster Variety adds the following item queries for use in it's `ExtraDrops` fields (or anywhere else that accepts item queries).

### `mushymato.MonsterVariety_MONSTER_DROPS <monsterId> [dropRateMult]`

Return the drop items of a particular monster as defined in `Data/Monsters`, optionally with the drop rates multiplied by a flat rate.

When multiplier is -1, the drop rate check is skipped and all items will be returned unconditionally. Otherwise it's possible for this query to return no items.

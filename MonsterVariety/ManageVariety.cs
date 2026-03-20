using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Delegates;
using StardewValley.Extensions;
using StardewValley.GameData;
using StardewValley.Internal;
using StardewValley.Monsters;

namespace MonsterVariety;

internal static class ManageVariety
{
    internal const string ModData_AppliedVariety = $"{ModEntry.ModId}/HasAppliedVariety";
    internal const string ModData_AppliedVarietyLight = $"{ModEntry.ModId}/HasAppliedVarietyLight";

    private static readonly ConditionalWeakTable<Monster, MonsterLightWatcher> monsterLightWatchers = [];

    internal static void Apply(IModHelper helper)
    {
        helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
        helper.Events.GameLoop.DayStarted += OnDayStarted;
        helper.Events.GameLoop.DayEnding += OnDayEnding;
        helper.Events.Player.Warped += OnWarped;
    }

    private static void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
    {
        foreach ((_, MonsterLightWatcher? mlw) in monsterLightWatchers)
        {
            mlw?.Deactivate();
        }
        monsterLightWatchers.Clear();
    }

    private static void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        SetupLocation(Game1.currentLocation);
    }

    private static void OnDayEnding(object? sender, DayEndingEventArgs e)
    {
        TeardownLocation(Game1.currentLocation);
    }

    private static void OnWarped(object? sender, WarpedEventArgs e)
    {
        TeardownLocation(e.OldLocation);
        SetupLocation(e.NewLocation);
    }

    private static void TeardownLocation(GameLocation location)
    {
        if (location == null)
            return;
        ModEntry.Log($"TEARDOWN {location.NameOrUniqueName}");
        location.characters.OnValueAdded -= OnMonsterAdded;
        location.characters.OnValueRemoved -= OnMonsterRemoved;
    }

    private static void SetupLocation(GameLocation location)
    {
        if (location == null)
            return;
        ModEntry.Log($"SETUP {location.NameOrUniqueName}");
        foreach (NPC npc in location.characters)
            OnMonsterAdded(npc);
        location.characters.OnValueAdded += OnMonsterAdded;
        location.characters.OnValueRemoved += OnMonsterRemoved;
    }

    private static void OnMonsterRemoved(NPC value)
    {
        if (value is Monster monster && monsterLightWatchers.TryGetValue(monster, out MonsterLightWatcher? mlw))
        {
            mlw.Deactivate();
            monsterLightWatchers.Remove(monster);
        }
    }

    private static void OnMonsterAdded(NPC value)
    {
        if (value is Monster monster)
            ApplyMonsterVariety(monster);
    }

    private static bool IsVanillaSprite(string? currTextureName)
    {
        if (currTextureName == null)
            return true;
        string[] nameParts = currTextureName.Split(['\\', '/']);
        return nameParts.Length == 3
            && nameParts[0].EqualsIgnoreCase("Characters")
            && nameParts[1].EqualsIgnoreCase("Monsters")
            && (ModEntry.VanillaCharacterMonster?.Contains(nameParts[2].ToLower()) ?? false);
    }

    private static bool IsValidVariety(
        Monster monster,
        VarietyData variety,
        bool isCustomMonsterClass,
        bool isCustomTexture,
        GameStateQueryContext gameStateQueryContext
    )
    {
        if (!variety.AlwaysOverride.CustomMonsterClass && isCustomMonsterClass)
            return false;
        if (!variety.AlwaysOverride.CustomTextures && isCustomTexture)
            return false;
        if (
            monster.Sprite?.textureName?.Value is string currTextureName
            && (variety.AlwaysOverride.SpecificTextureName?.EqualsIgnoreCase(currTextureName ?? string.Empty) ?? false)
        )
            return false;
        if (variety.Sprite == null)
            return false;
        if (!Game1.content.DoesAssetExist<Texture2D>(variety.Sprite))
            return false;
        if (variety.Season != null && variety.Season != Game1.GetSeasonForLocation(monster.currentLocation))
            return false;
        if (variety.Condition != null && !GameStateQuery.CheckConditions(variety.Condition, gameStateQueryContext))
            return false;
        return true;
    }

    private static void AddExtraDrops(
        Monster monster,
        IEnumerable<GenericSpawnItemDataWithCondition>? dropsQueries,
        GameStateQueryContext gsqContext,
        ItemQueryContext iqContext
    )
    {
        if (dropsQueries == null)
            return;
        foreach (GenericSpawnItemDataWithCondition spawnData in dropsQueries)
        {
            if (!GameStateQuery.CheckConditions(spawnData.Condition, gsqContext))
                continue;
            var results = ItemQueryResolver.TryResolve(spawnData, iqContext, filter: ItemQuerySearchMode.AllOfTypeItem);
            foreach (var res in results)
            {
                if (res.Item is Item item)
                {
                    for (int i = 0; i < item.Stack; i++)
                        monster.objectsToDrop.Add(item.QualifiedItemId);
                }
            }
        }
    }

    private static void ApplyMonsterVariety(Monster monster)
    {
        Type monsterType = monster.GetType();
        string monsterName = monster.Name;
        // special cases
        if (monster is Bug bug && bug.isArmoredBug.Value)
        {
            // Armored Bug
            monsterName = "Armored Bug";
        }
        else if (monster is Skeleton skeleton && skeleton.isMage.Value)
        {
            // Skeleton Mage
            monsterName = "Skeleton Mage";
        }
        else if (monster.Sprite?.textureName?.Value == "Characters\\Monsters\\Assassin Bug")
        {
            // Assassin Bug
            monsterName = "Assassin Bug";
        }

        ModEntry.LogOnce(
            $"Try ApplyMonsterVariety on '{monsterName}' ({monsterType.Namespace} : {monsterType.Name} '{monster.Sprite?.textureName?.Value}' HardMode:{monster.isHardModeMonster.Value})"
        );
        if (!AssetManager.VarietyData.TryGetValue(monsterName, out MonsterVarietyData? data))
        {
            // special case Green Slime
            if (monster is not GreenSlime || !AssetManager.VarietyData.TryGetValue("Green Slime", out data))
            {
                return;
            }
        }

        if (!monster.modData.TryGetValue(ModData_AppliedVariety, out string textureName))
        {
            Dictionary<string, VarietyData> varieties;
            GameStateQueryContext gameStateQueryContext = new(monster.currentLocation, Game1.player, null, null, null);
            ItemQueryContext itemQueryContext = new(
                monster.currentLocation,
                Game1.player,
                null,
                $"{ModEntry.ModId}:{monsterName}"
            );
            if (monster.isHardModeMonster.Value)
            {
                varieties = data.DangerousVarieties;
                AddExtraDrops(monster, data.DangerousSharedExtraDrops?.Values, gameStateQueryContext, itemQueryContext);
            }
            else
            {
                varieties = data.Varieties;
                AddExtraDrops(monster, data.SharedExtraDrops?.Values, gameStateQueryContext, itemQueryContext);
            }

            bool isCustomMonsterClass = monsterType.Namespace != "StardewValley.Monsters";
            bool isCustomTexture = !IsVanillaSprite(monster.Sprite?.textureName?.Value);
            List<VarietyData> validVariety = varieties
                .Values.Where(variety =>
                    IsValidVariety(monster, variety, isCustomMonsterClass, isCustomTexture, gameStateQueryContext)
                )
                .ToList();
            if (validVariety.Count > 0)
            {
                int minPrecedence = validVariety.Min(variety => variety.Precedence);
                List<VarietyData> validVarietyList = validVariety
                    .Where(variety => variety.Precedence == minPrecedence)
                    .ToList();
                VarietyData chosenVariety = Random.Shared.ChooseFrom(validVarietyList);
                textureName = chosenVariety.Sprite!;
                monster.modData[ModData_AppliedVariety] = textureName;
                if (chosenVariety.LightProps != null)
                    monster.modData[ModData_AppliedVarietyLight] = chosenVariety.LightProps;
                AddExtraDrops(monster, chosenVariety.ExtraDrops?.Values, gameStateQueryContext, itemQueryContext);
                if (!string.IsNullOrEmpty(chosenVariety.HUDNotif))
                {
                    if (
                        !string.IsNullOrEmpty(chosenVariety.HUDNotifIconItem)
                        && ItemRegistry.Create(chosenVariety.HUDNotifIconItem) is Item icon
                    )
                    {
                        Game1.addHUDMessage(new HUDMessage(chosenVariety.HUDNotif) { messageSubject = icon });
                    }
                    else
                    {
                        Game1.addHUDMessage(HUDMessage.ForCornerTextbox(chosenVariety.HUDNotif));
                    }
                }
            }
            else
            {
                return;
            }
        }

        monster.Sprite ??= new AnimatedSprite(textureName);
        if (monster.Sprite.textureName.Value != textureName)
        {
            monster.Sprite.textureName.Value = textureName;
        }

        if (monster.modData.TryGetValue(ModData_AppliedVarietyLight, out string lightProps))
        {
            MonsterLightWatcher watcher = monsterLightWatchers.GetValue(monster, MonsterLightWatcher.Create);
            watcher.Activate(lightProps);
        }
    }
}

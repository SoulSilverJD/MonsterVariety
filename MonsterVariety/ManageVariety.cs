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
    
    private static readonly Dictionary<string, int> monsterFieldAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Health"] = Monster.index_health,
        ["Damage"] = Monster.index_damageToFarmer,
        ["DamageToFarmer"] = Monster.index_damageToFarmer,
        ["IsGlider"] = Monster.index_isGlider,
        ["Glider"] = Monster.index_isGlider,
        ["Resilience"] = Monster.index_resilience,
        ["Jitteriness"] = Monster.index_jitteriness,
        ["MoveTowardPlayerThreshold"] = Monster.index_distanceThresholdToMoveTowardsPlayer,
        ["DistanceThresholdToMoveTowardsPlayer"] = Monster.index_distanceThresholdToMoveTowardsPlayer,
        ["Speed"] = Monster.index_speed,
        ["MissChance"] = Monster.index_missChance,
        ["MineMonster"] = Monster.index_isMineMonster,
        ["IsMineMonster"] = Monster.index_isMineMonster,
        ["ExperienceGained"] = Monster.index_experiencePoints,
        ["ExperiencePoints"] = Monster.index_experiencePoints,
        ["DisplayName"] = Monster.index_displayName,
    };

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
    
    private static bool TryGetMonsterFieldIndex(string key, out int index)
    {
        // Match Content Patcher's Fields style: numeric keys use Data/Monsters indexes, string keys use readable aliases.
        if (int.TryParse(key, out index))
            return true;
        return monsterFieldAliases.TryGetValue(key, out index);
    }

    private static bool TryGetMonsterFieldIntValue(string fieldName, object? value, out int result)
    {
        result = 0;
        if (value == null)
        {
            ModEntry.Log($"Skipped monster field '{fieldName}' because its value is null", LogLevel.Warn);
            return false;
        }
        try
        {
            result = Convert.ToInt32(value);
            return true;
        }
        catch (Exception ex)
        {
            ModEntry.Log($"Skipped monster field '{fieldName}' because value '{value}' is not an integer: {ex.Message}", LogLevel.Warn);
            return false;
        }
    }

    private static bool TryGetMonsterFieldBoolValue(string fieldName, object? value, out bool result)
    {
        result = false;
        if (value == null)
        {
            ModEntry.Log($"Skipped monster field '{fieldName}' because its value is null", LogLevel.Warn);
            return false;
        }
        try
        {
            result = Convert.ToBoolean(value);
            return true;
        }
        catch (Exception ex)
        {
            ModEntry.Log($"Skipped monster field '{fieldName}' because value '{value}' is not true/false: {ex.Message}", LogLevel.Warn);
            return false;
        }
    }

    private static bool TryGetMonsterFieldDoubleValue(string fieldName, object? value, out double result)
    {
        result = 0;
        if (value == null)
        {
            ModEntry.Log($"Skipped monster field '{fieldName}' because its value is null", LogLevel.Warn);
            return false;
        }
        try
        {
            result = Convert.ToDouble(value);
            return true;
        }
        catch (Exception ex)
        {
            ModEntry.Log($"Skipped monster field '{fieldName}' because value '{value}' is not a number: {ex.Message}", LogLevel.Warn);
            return false;
        }
    }

    private static void ApplyMonsterFields(Monster monster, Dictionary<string, object?>? fields)
    {
        if (fields == null)
            return;
        foreach ((string key, object? value) in fields)
        {
            if (!TryGetMonsterFieldIndex(key, out int index))
            {
                ModEntry.Log($"Skipped unknown monster field '{key}'", LogLevel.Warn);
                continue;
            }
            switch (index)
            {
                case Monster.index_health:
                    if (TryGetMonsterFieldIntValue(key, value, out int health))
                    {
                        // Match vanilla parseMonsterInfo by setting current and max health together.
                        monster.Health = health;
                        monster.MaxHealth = health;
                    }
                    break;
                case Monster.index_damageToFarmer:
                    if (TryGetMonsterFieldIntValue(key, value, out int damage))
                        monster.DamageToFarmer = damage;
                    break;
                case Monster.index_isGlider:
                    if (TryGetMonsterFieldBoolValue(key, value, out bool isGlider))
                        monster.isGlider.Value = isGlider;
                    break;
                case Monster.index_resilience:
                    if (TryGetMonsterFieldIntValue(key, value, out int resilience))
                        monster.resilience.Value = resilience;
                    break;
                case Monster.index_jitteriness:
                    if (TryGetMonsterFieldDoubleValue(key, value, out double jitteriness))
                        monster.jitteriness.Value = jitteriness;
                    break;
                case Monster.index_distanceThresholdToMoveTowardsPlayer:
                    if (TryGetMonsterFieldIntValue(key, value, out int distanceThreshold))
                        monster.moveTowardPlayer(distanceThreshold);
                    break;
                case Monster.index_speed:
                    if (TryGetMonsterFieldIntValue(key, value, out int speed))
                        monster.speed = speed;
                    break;
                case Monster.index_missChance:
                    if (TryGetMonsterFieldDoubleValue(key, value, out double missChance))
                        monster.missChance.Value = missChance;
                    break;
                case Monster.index_isMineMonster:
                    if (TryGetMonsterFieldBoolValue(key, value, out bool mineMonster))
                        monster.mineMonster.Value = mineMonster;
                    break;
                case Monster.index_experiencePoints:
                    if (TryGetMonsterFieldIntValue(key, value, out int experienceGained))
                        monster.ExperienceGained = experienceGained;
                    break;
                case Monster.index_displayName:
                    monster.displayName = value?.ToString() ?? string.Empty;
                    break;
                default:
                    ModEntry.Log($"Skipped unsupported monster field '{key}' (index {index})", LogLevel.Warn);
                    break;
            }
        }
    }
    
    private static string NormalizeMonsterDropId(string itemId)
    {
        return itemId.StartsWith("(O)", StringComparison.OrdinalIgnoreCase) ? itemId[3..] : itemId;
    }

    private static bool ShouldExcludeDrop(
        string itemId,
        HashSet<string> excludeIds,
        HashSet<string> normalizedExcludeIds
    )
    {
        return excludeIds.Contains(itemId) || normalizedExcludeIds.Contains(NormalizeMonsterDropId(itemId));
    }

    private static void ExcludeDrops(
        Monster monster,
        IEnumerable<string>? excludeDrops,
        int monsterDropCount
    )
    {
        if (excludeDrops == null || monsterDropCount <= 0)
            return;

        HashSet<string> excludeIds = new(excludeDrops, StringComparer.OrdinalIgnoreCase);
        HashSet<string> normalizedExcludeIds = new(excludeIds.Select(NormalizeMonsterDropId), StringComparer.OrdinalIgnoreCase);
        int maxIndex = Math.Min(monsterDropCount, monster.objectsToDrop.Count) - 1;
        for (int i = maxIndex; i >= 0; i--)
        {
            if (ShouldExcludeDrop(
                monster.objectsToDrop[i], excludeIds, normalizedExcludeIds))
                monster.objectsToDrop.RemoveAt(i);
        }
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
                ApplyMonsterFields(monster, chosenVariety.Fields);
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

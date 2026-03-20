using StardewModdingAPI;
using StardewValley;
using StardewValley.Delegates;
using StardewValley.Extensions;
using StardewValley.Internal;

namespace MonsterVariety;

internal static class GameDelegates
{
    public static bool TryGetPercent(
        string[] array,
        int index,
        out double value,
        out string? error,
        double defaultValue,
        string name
    )
    {
        value = 0;
        if (
            !ArgUtility.TryGetOptional(
                array,
                index,
                out string valueStr,
                out error,
                allowBlank: true,
                defaultValue: string.Empty,
                name: name
            ) || !double.TryParse(valueStr, out value)
        )
        {
            return false;
        }
        if (string.IsNullOrEmpty(valueStr))
        {
            value = defaultValue;
            return true;
        }
        if (valueStr[0] != '.')
        {
            value /= 100.0;
        }
        return true;
    }

    public static bool RandomImpl(Random random, string[] query, int skipArguments)
    {
        if (!ArgUtility.TryGetFloat(query, skipArguments, out float valueFlt, out string? error, "float chance"))
        {
            ModEntry.Log(error);
            return false;
        }
        double value = valueFlt;
        bool addDailyLuck = false;
        double addPlayerLuck = 0;
        for (int i = skipArguments + 1; i < query.Length; i++)
        {
            if (query[i].EqualsIgnoreCase("@addDailyLuck"))
            {
                addDailyLuck = true;
            }
            if (query[i].EqualsIgnoreCase("@addPlayerLuck"))
            {
                TryGetPercent(query, i + 1, out addPlayerLuck, out _, 0.01, "float playerLuckMod");
            }
        }
        if (addDailyLuck)
        {
            value += Game1.player.DailyLuck;
        }
        value += addPlayerLuck * Game1.player.LuckLevel;
        return random.NextDouble() < (double)value;
    }

    public static bool LUCKY_RANDOM(string[] query, GameStateQueryContext context)
    {
        return RandomImpl(context.Random, query, 1);
    }

    public static bool SYNCED_LUCKY_RANDOM(string[] query, GameStateQueryContext context)
    {
        if (
            !ArgUtility.TryGet(query, 1, out var value, out var error, allowBlank: true, "string interval")
            || !ArgUtility.TryGet(query, 2, out var value2, out error, allowBlank: true, "string key")
            || !Utility.TryCreateIntervalRandom(value, value2, out Random random, out error)
        )
        {
            ModEntry.Log(error, LogLevel.Error);
            return false;
        }
        return RandomImpl(random, query, 2);
    }

    internal static IEnumerable<ItemQueryResult> MONSTER_DROPS(
        string key,
        string arguments,
        ItemQueryContext context,
        bool avoidRepeat,
        HashSet<string>? avoidItemIds,
        Action<string, string> logError
    )
    {
        string[] args = ArgUtility.SplitBySpaceQuoteAware(arguments);
        if (
            !ArgUtility.TryGet(
                args,
                0,
                out string? monsterId,
                out string? error,
                allowBlank: false,
                name: "string monsterId"
            )
            || !ArgUtility.TryGetOptionalFloat(args, 1, out float mult, out error, defaultValue: 1, name: "float mult")
        )
        {
            ItemQueryResolver.Helpers.ErrorResult(key, arguments, logError, error);
            yield break;
        }
        if (!DataLoader.Monsters(Game1.content).TryGetValue(monsterId, out string? monsterDataStr))
        {
            ItemQueryResolver.Helpers.ErrorResult(key, arguments, logError, $"No monster with id '{monsterId}' found");
            yield break;
        }
        string[] monsterData = monsterDataStr.Split('/');
        string[] dropsData = ArgUtility.SplitBySpace(monsterData[6]);

        Random random = context.Random ?? Game1.random;
        HashSet<string> seen = [];
        for (int i = 1; i < dropsData.Length; i += 2)
        {
            if (mult < 0 || random.NextDouble() < Convert.ToDouble(dropsData[i]) * mult)
            {
                if (
                    ItemRegistry.Create(dropsData[i - 1]) is Item item
                    && !(avoidItemIds?.Contains(item.QualifiedItemId) ?? false)
                    && (!avoidRepeat || seen.Contains(item.QualifiedItemId))
                )
                {
                    seen.Add(item.QualifiedItemId);
                    yield return new(item);
                }
            }
        }
    }
}

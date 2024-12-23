using System.Linq;
using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory.MemoryObjects;
using ExileCore2.Shared.Enums;

namespace AutoMyAim;

public class TargetWeightCalculator
{
    public float CalculateWeight(AutoMyAimSettings settings, Entity entity, float distance)
    {
        if (!settings.EnableWeighting) return 0f;
        if (distance > settings.MaxTargetDistance) return 0f;

        var weight = 0f;

        var normalizedDistance = 1f - distance / settings.MaxTargetDistance;
        weight += normalizedDistance * settings.DistanceWeight;

        var life = entity.GetComponent<Life>();
        if (life != null)
        {
            var hpPercent = life.HPPercentage;
            weight += (settings.PreferHigherHP ? hpPercent : 1 - hpPercent) * settings.HPWeight;
        }

        var rarity = entity.GetComponent<ObjectMagicProperties>()?.Rarity ?? MonsterRarity.White;
        weight += GetRarityWeight(settings, rarity);

        if (settings.CustomEntityPriorities.Values.Any(path =>
                entity.Path.ToLower().Contains((string)path.ToLower())))
            weight += settings.CustomEntityWeight;

        return weight;
    }

    private float GetRarityWeight(AutoMyAimSettings settings, MonsterRarity rarity)
    {
        return rarity switch
        {
            MonsterRarity.White => settings.NormalWeight,
            MonsterRarity.Magic => settings.MagicWeight,
            MonsterRarity.Rare => settings.RareWeight,
            MonsterRarity.Unique => settings.UniqueWeight,
            _ => 0f
        };
    }
}
using System;
using System.Linq;
using AutoMyAim.Structs;
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

        // Calculate distance weight with exponential falloff
        // This makes closer targets dramatically more important
        var distanceWeight = (float)Math.Pow(1 - distance / settings.MaxTargetDistance, 2) * settings.DistanceWeight;

        // Base weight starts with distance as the primary factor
        var weight = distanceWeight;

        // Get rarity weight but scale it based on distance
        var rarityWeight = GetRarityWeight(settings,
            entity.GetComponent<ObjectMagicProperties>()?.Rarity ?? MonsterRarity.White);
        // Rarity becomes less important as distance increases
        rarityWeight *= (float)Math.Pow(1 - distance / settings.MaxTargetDistance, 0.5);
        weight += rarityWeight;

        // Add HP consideration
        var life = entity.GetComponent<Life>();
        if (life != null)
        {
            var hpPercent = life.HPPercentage + life.ESPercentage;
            var hpWeight = (settings.PreferHigherHP ? hpPercent : 1 - hpPercent) * settings.HPWeight;
            // HP weight also scales with distance
            weight += hpWeight * (float)Math.Pow(1 - distance / settings.MaxTargetDistance, 0.5);
        }

        // Custom entity priorities
        if (settings.CustomEntityPriorities.Values.Any(path =>
                entity.Path.ToLower().Contains(path.ToLower())))
        {
            var customWeight = settings.CustomEntityWeight;
            // Custom weight also scales with distance
            weight += customWeight * (float)Math.Pow(1 - distance / settings.MaxTargetDistance, 0.5);
        }

        // Additional proximity bonus for very close targets (within 20% of max distance)
        if (distance < settings.MaxTargetDistance * 0.2f)
            weight *= 1.5f + (1 - distance / (settings.MaxTargetDistance * 0.2f));

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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AutoMyAim.Structs;
using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory.MemoryObjects;
using ExileCore2.Shared.Enums;

namespace AutoMyAim;

public class TargetWeightCalculator
{
    private const float WEIGHT_SMOOTHING_FACTOR = 0.3f;

    // Keep track of previous frame's weights for smoothing
    private readonly Dictionary<Entity, float> _previousWeights = new();

    public void UpdateWeights(List<TrackedEntity> entities, Vector2 playerPosition, AutoMyAimSettings settings)
    {
        if (!settings.Targeting.Weights.EnableWeighting) return;

        // First pass - calculate base weights
        foreach (var entity in entities) entity.Weight = CalculateBaseWeight(entity, playerPosition, settings);

        // Second pass - apply contextual modifiers based on relative positions
        ApplyContextualModifiers(entities, playerPosition);

        // Third pass - apply weight smoothing
        ApplyWeightSmoothing(entities);

        // Clean up old entities from previous weights cache
        CleanupPreviousWeights(entities);
    }

    private float CalculateBaseWeight(TrackedEntity trackedEntity, Vector2 playerPosition, AutoMyAimSettings settings)
    {
        var entity = trackedEntity.Entity;
        var distance = Vector2.Distance(playerPosition, entity.GridPos);

        if (distance > settings.Targeting.MaxTargetDistance) return 0f;

        var weight = 0f;

        // Distance weight with exponential falloff
        weight += CalculateDistanceWeight(distance, settings);

        // Rarity consideration
        weight += CalculateRarityWeight(entity, distance, settings);

        // HP/ES status
        weight += CalculateHealthWeight(entity, distance, settings);

        // Custom target priorities
        weight += CalculateCustomTargetWeight(entity, distance, settings);

        // Additional proximity bonus for very close targets
        if (distance < settings.Targeting.MaxTargetDistance * 0.2f)
        {
            var proximityBonus = 1.5f + (1 - distance / (settings.Targeting.MaxTargetDistance * 0.2f));
            weight *= proximityBonus;
        }

        return weight;
    }

    private float CalculateDistanceWeight(float distance, AutoMyAimSettings settings)
    {
        var normalizedDistance = distance / settings.Targeting.MaxTargetDistance;
        return (float)Math.Pow(1 - normalizedDistance, 2) * settings.Targeting.Weights.DistanceWeight;
    }

    private float CalculateRarityWeight(Entity entity, float distance, AutoMyAimSettings settings)
    {
        var rarity = entity.GetComponent<ObjectMagicProperties>()?.Rarity ?? MonsterRarity.White;
        var baseWeight = rarity switch
        {
            MonsterRarity.White => settings.Targeting.Weights.Rarity.Normal,
            MonsterRarity.Magic => settings.Targeting.Weights.Rarity.Magic,
            MonsterRarity.Rare => settings.Targeting.Weights.Rarity.Rare,
            MonsterRarity.Unique => settings.Targeting.Weights.Rarity.Unique,
            _ => 0f
        };

        // Rarity becomes less important at greater distances
        return baseWeight * (float)Math.Pow(1 - distance / settings.Targeting.MaxTargetDistance, 0.5);
    }

    private float CalculateHealthWeight(Entity entity, float distance, AutoMyAimSettings settings)
    {
        var life = entity.GetComponent<Life>();
        if (life == null) return 0f;

        var hpPercent = life.HPPercentage + life.ESPercentage;
        var baseWeight = (settings.Targeting.Weights.HP.PreferHigherHP ? hpPercent : 1 - hpPercent) *
                         settings.Targeting.Weights.HP.Weight;

        // Health weight scales with distance
        return baseWeight * (float)Math.Pow(1 - distance / settings.Targeting.MaxTargetDistance, 0.5);
    }

    private float CalculateCustomTargetWeight(Entity entity, float distance, AutoMyAimSettings settings)
    {
        if (!settings.Targeting.CustomTargets.Priorities.Values.Any(path =>
                entity.Path.ToLower().Contains(path.ToLower())))
            return 0f;

        // Custom weight scales with distance
        return settings.Targeting.CustomTargets.Weight *
               (float)Math.Pow(1 - distance / settings.Targeting.MaxTargetDistance, 0.5);
    }

    private void ApplyContextualModifiers(List<TrackedEntity> entities, Vector2 playerPosition)
    {
        // Group entities into clusters
        var clusters = FindEntityClusters(entities, playerPosition);

        foreach (var cluster in clusters)
            // Increase weights for entities in dense clusters
            if (cluster.Count > 3)
            {
                var clusterBonus = Math.Min(1.3f + (cluster.Count - 3) * 0.1f, 2.0f);
                foreach (var entity in cluster) entity.Weight *= clusterBonus;
            }
    }

    private List<List<TrackedEntity>> FindEntityClusters(List<TrackedEntity> entities, Vector2 playerPosition)
    {
        const float CLUSTER_RADIUS = 15f; // Adjust based on your game's scale
        var clusters = new List<List<TrackedEntity>>();
        var processedEntities = new HashSet<TrackedEntity>();

        foreach (var entity in entities)
        {
            if (processedEntities.Contains(entity)) continue;

            var cluster = new List<TrackedEntity> { entity };
            processedEntities.Add(entity);

            foreach (var other in from other in entities
                     where !processedEntities.Contains(other)
                     let distance = Vector2.Distance(entity.Entity.GridPos, other.Entity.GridPos)
                     where distance <= CLUSTER_RADIUS
                     select other)
            {
                cluster.Add(other);
                processedEntities.Add(other);
            }

            clusters.Add(cluster);
        }

        return clusters;
    }

    private void ApplyWeightSmoothing(List<TrackedEntity> entities)
    {
        foreach (var entity in entities)
        {
            if (_previousWeights.TryGetValue(entity.Entity, out var previousWeight))
                // Lerp between previous and current weight
                entity.Weight = previousWeight + (entity.Weight - previousWeight) * WEIGHT_SMOOTHING_FACTOR;
            _previousWeights[entity.Entity] = entity.Weight;
        }
    }

    private void CleanupPreviousWeights(List<TrackedEntity> currentEntities)
    {
        var currentEntitySet = new HashSet<Entity>(currentEntities.Select(x => x.Entity));
        var keysToRemove = _previousWeights.Keys.Where(x => !currentEntitySet.Contains(x)).ToList();
        foreach (var key in keysToRemove) _previousWeights.Remove(key);
    }
}
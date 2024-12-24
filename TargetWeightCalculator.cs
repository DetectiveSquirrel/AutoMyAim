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
    private const float CLUSTER_RADIUS_SQ = 625f; // 25^2, square of CLUSTER_RADIUS for faster distance checks
    private const int MIN_CLUSTER_SIZE = 3;
    private const float ISOLATED_PENALTY = 0.8f;
    private const float CORE_BONUS = 1.2f;
    private readonly Dictionary<Entity, Life> _cachedLife = new();
    private readonly Dictionary<Entity, MonsterRarity> _cachedRarities = new();
    private readonly Dictionary<Entity, float> _previousWeights = new();

    public void UpdateWeights(List<TrackedEntity> entities, Vector2 playerPosition, AutoMyAimSettings settings)
    {
        if (!settings.Targeting.Weights.EnableWeighting) return;

        var maxDistanceSq = settings.Targeting.MaxTargetDistance * settings.Targeting.MaxTargetDistance;
        var entityCount = entities.Count;

        // Precalculate distances and update base weights
        for (var i = 0; i < entityCount; i++)
        {
            var entity = entities[i];
            var distanceSq = Vector2.DistanceSquared(playerPosition, entity.Entity.GridPos);
            entity.Distance = MathF.Sqrt(distanceSq); // Store actual distance for other uses

            if (distanceSq > maxDistanceSq)
            {
                entity.Weight = 0f;
                continue;
            }

            entity.Weight = FastCalculateBaseWeight(entity, entity.Distance, settings);
        }

        // Fast cluster detection using grid-based approach
        var clusters = FastIdentifyClusters(entities);

        // Apply cluster weights
        var entitiesInClusters = new HashSet<Entity>();
        foreach (var cluster in clusters)
        {
            // Calculate cluster center
            var center = Vector2.Zero;
            foreach (var entity in cluster) center += entity.Entity.GridPos;
            center /= cluster.Count;

            // Apply weights including core bonus for entities near center
            foreach (var entity in cluster)
            {
                var baseClusterBonus = 1.0f + (cluster.Count - MIN_CLUSTER_SIZE) * 0.1f;
                entity.Weight *= baseClusterBonus;

                // Add core bonus for entities close to cluster center
                var distanceToCenter = Vector2.DistanceSquared(entity.Entity.GridPos, center);
                if (distanceToCenter <= CLUSTER_RADIUS_SQ * 0.25f) // Entities within 50% radius of center
                    entity.Weight *= CORE_BONUS;

                entitiesInClusters.Add(entity.Entity);
            }
        }

        // Apply penalty to isolated entities (except rares and uniques)
        for (var i = 0; i < entityCount; i++)
        {
            var entity = entities[i];
            if (!entitiesInClusters.Contains(entity.Entity))
            {
                var rarity = _cachedRarities.TryGetValue(entity.Entity, out var r)
                    ? r
                    : entity.Entity.GetComponent<ObjectMagicProperties>()?.Rarity ?? MonsterRarity.White;

                // Only apply isolation penalty to white and magic monsters
                if (rarity == MonsterRarity.White || rarity == MonsterRarity.Magic) entity.Weight *= ISOLATED_PENALTY;
            }
        }

        // Apply weight smoothing
        ApplyWeightSmoothing(entities);

        if (Environment.TickCount % 100 == 0) CleanupCaches(entities);
    }

    private float FastCalculateBaseWeight(TrackedEntity trackedEntity, float distance, AutoMyAimSettings settings)
    {
        var weight = 0f;
        var distanceFactor = 1f - distance / settings.Targeting.MaxTargetDistance;

        // Distance weight (fastest calculation)
        weight += distanceFactor * distanceFactor * settings.Targeting.Weights.DistanceWeight;

        // Rarity weight (cached)
        if (!_cachedRarities.TryGetValue(trackedEntity.Entity, out var rarity))
        {
            rarity = trackedEntity.Entity.GetComponent<ObjectMagicProperties>()?.Rarity ?? MonsterRarity.White;
            _cachedRarities[trackedEntity.Entity] = rarity;
        }

        weight += GetRarityBaseWeight(rarity, settings) * distanceFactor;

        // HP consideration (cached)
        if (settings.Targeting.Weights.HP.Weight > 0)
        {
            if (!_cachedLife.TryGetValue(trackedEntity.Entity, out var life))
            {
                life = trackedEntity.Entity.GetComponent<Life>();
                _cachedLife[trackedEntity.Entity] = life;
            }

            if (life != null)
            {
                var hpPercent = life.HPPercentage + life.ESPercentage;
                var hpWeight = (settings.Targeting.Weights.HP.PreferHigherHP ? hpPercent : 1 - hpPercent) *
                               settings.Targeting.Weights.HP.Weight;
                weight += hpWeight * distanceFactor;
            }
        }

        // Custom priorities (only if configured)
        if (settings.Targeting.CustomTargets.Weight > 0 &&
            settings.Targeting.CustomTargets.Priorities.Values.Any(path =>
                trackedEntity.Entity.Path.ToLower().Contains(path.ToLower())))
            weight += settings.Targeting.CustomTargets.Weight * distanceFactor;

        return weight;
    }

    private List<List<TrackedEntity>> FastIdentifyClusters(List<TrackedEntity> entities)
    {
        var clusters = new List<List<TrackedEntity>>();
        var processed = new HashSet<TrackedEntity>();

        foreach (var entity in entities)
        {
            if (processed.Contains(entity)) continue;

            var cluster = new List<TrackedEntity> { entity };
            processed.Add(entity);

            // Check only remaining unprocessed entities
            foreach (var other in entities)
            {
                if (processed.Contains(other)) continue;

                if (Vector2.DistanceSquared(entity.Entity.GridPos, other.Entity.GridPos) <= CLUSTER_RADIUS_SQ)
                {
                    cluster.Add(other);
                    processed.Add(other);
                }
            }

            if (cluster.Count >= MIN_CLUSTER_SIZE) clusters.Add(cluster);
        }

        return clusters;
    }

    private float GetRarityBaseWeight(MonsterRarity rarity, AutoMyAimSettings settings)
    {
        return rarity switch
        {
            MonsterRarity.White => settings.Targeting.Weights.Rarity.Normal,
            MonsterRarity.Magic => settings.Targeting.Weights.Rarity.Magic,
            MonsterRarity.Rare => settings.Targeting.Weights.Rarity.Rare,
            MonsterRarity.Unique => settings.Targeting.Weights.Rarity.Unique,
            _ => 0f
        };
    }

    private void ApplyWeightSmoothing(List<TrackedEntity> entities)
    {
        foreach (var entity in entities)
        {
            if (_previousWeights.TryGetValue(entity.Entity, out var previousWeight))
                entity.Weight = previousWeight + (entity.Weight - previousWeight) * WEIGHT_SMOOTHING_FACTOR;
            _previousWeights[entity.Entity] = entity.Weight;
        }
    }

    private void CleanupCaches(List<TrackedEntity> currentEntities)
    {
        var currentEntitySet = new HashSet<Entity>(currentEntities.Select(x => x.Entity));

        // Cleanup previous weights
        foreach (var key in _previousWeights.Keys.ToList())
            if (!currentEntitySet.Contains(key))
                _previousWeights.Remove(key);

        // Cleanup rarity cache
        foreach (var key in _cachedRarities.Keys.ToList())
            if (!currentEntitySet.Contains(key))
                _cachedRarities.Remove(key);

        // Cleanup life cache
        foreach (var key in _cachedLife.Keys.ToList())
            if (!currentEntitySet.Contains(key))
                _cachedLife.Remove(key);
    }
}
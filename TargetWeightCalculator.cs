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
    private readonly Dictionary<Entity, Life> _cachedLife = new();
    private readonly Dictionary<Entity, MonsterRarity> _cachedRarities = new();
    private readonly Dictionary<Entity, float> _previousWeights = new();

    public void UpdateWeights(List<TrackedEntity> entities, Vector2 playerPosition, AutoMyAimSettings settings)
    {
        if (!settings.Targeting.Weights.EnableWeighting) return;

        var entityCount = entities.Count;
        var clusterSettings = settings.Targeting.Weights.Cluster;

        // Calculate cluster radius squared once
        var clusterRadiusSq = clusterSettings.ClusterRadius.Value * clusterSettings.ClusterRadius.Value;

        // Precalculate distances and update base weights
        for (var i = 0; i < entityCount; i++)
        {
            var entity = entities[i];
            entity.Distance = entity.Entity.DistancePlayer;

            if (entity.Distance > settings.Targeting.MaxTargetDistance)
            {
                entity.Weight = 0f;
                continue;
            }

            entity.Weight = FastCalculateBaseWeight(entity, entity.Distance, settings);
        }

        // Only process clustering if enabled
        if (clusterSettings.EnableClustering)
        {
            var clusters = FastIdentifyClusters(entities, clusterRadiusSq, clusterSettings.MinClusterSize.Value);
            var entitiesInClusters = new HashSet<Entity>();

            foreach (var cluster in clusters)
            {
                var center = cluster.Aggregate(Vector2.Zero, (current, entity) => current + entity.Entity.GridPos);
                center /= cluster.Count;

                var clusterBonus = Math.Min(
                    1.0f + (cluster.Count - clusterSettings.MinClusterSize.Value) *
                    clusterSettings.BaseClusterBonus.Value,
                    clusterSettings.MaxClusterBonus.Value
                );

                foreach (var entity in cluster)
                {
                    entity.Weight *= clusterBonus;

                    // Apply core bonus if enabled
                    if (clusterSettings.EnableCoreBonus)
                    {
                        var distanceToCenter = Vector2.DistanceSquared(entity.Entity.GridPos, center);
                        var coreRadiusSq = clusterRadiusSq * clusterSettings.CoreRadiusPercent.Value *
                                           clusterSettings.CoreRadiusPercent.Value;

                        if (distanceToCenter <= coreRadiusSq)
                            entity.Weight *= clusterSettings.CoreBonusMultiplier.Value;
                    }

                    entitiesInClusters.Add(entity.Entity);
                }
            }

            // Apply isolation penalty if enabled
            if (clusterSettings.EnableIsolationPenalty)
                for (var i = 0; i < entityCount; i++)
                {
                    var entity = entities[i];
                    if (!entitiesInClusters.Contains(entity.Entity))
                    {
                        var rarity = _cachedRarities.TryGetValue(entity.Entity, out var r)
                            ? r
                            : entity.Entity.GetComponent<ObjectMagicProperties>()?.Rarity ?? MonsterRarity.White;

                        // Only apply isolation penalty to white and magic monsters
                        if (rarity == MonsterRarity.White || rarity == MonsterRarity.Magic)
                            entity.Weight *= clusterSettings.IsolationPenaltyMultiplier.Value;
                    }
                }
        }

        // Apply weight smoothing if enabled
        if (settings.Targeting.Weights.Smoothing.EnableSmoothing)
            ApplyWeightSmoothing(entities, settings.Targeting.Weights.Smoothing.SmoothingFactor.Value);

        // Cleanup old cache entries every 100 frames
        if (Environment.TickCount % 100 == 0) CleanupCaches(entities);
    }

    private float FastCalculateBaseWeight(TrackedEntity trackedEntity, float distance, AutoMyAimSettings settings)
    {
        var weight = 0f;
        var distanceFactor = 1f - distance / settings.Targeting.MaxTargetDistance;

        // Distance weight
        weight += distanceFactor * distanceFactor * settings.Targeting.Weights.DistanceWeight;

        // Rarity weight
        if (settings.Targeting.Weights.Rarity.EnableRarityWeighting)
        {
            if (!_cachedRarities.TryGetValue(trackedEntity.Entity, out var rarity))
            {
                rarity = trackedEntity.Entity.GetComponent<ObjectMagicProperties>()?.Rarity ?? MonsterRarity.White;
                _cachedRarities[trackedEntity.Entity] = rarity;
            }

            weight += GetRarityBaseWeight(rarity, settings) * distanceFactor;
        }

        // HP consideration
        if (settings.Targeting.Weights.HP.EnableHPWeighting)
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

        // Custom priorities
        if (settings.Targeting.CustomTargets.Weight > 0 &&
            settings.Targeting.CustomTargets.Priorities.Values.Any(path =>
                trackedEntity.Entity.Path.ToLower().Contains(path.ToLower())))
            weight += settings.Targeting.CustomTargets.Weight * distanceFactor;

        return weight;
    }

    private List<List<TrackedEntity>> FastIdentifyClusters(List<TrackedEntity> entities, float clusterRadiusSq,
        int minClusterSize)
    {
        var clusters = new List<List<TrackedEntity>>();
        var processed = new HashSet<TrackedEntity>();

        foreach (var entity in entities)
        {
            if (processed.Contains(entity)) continue;

            var cluster = new List<TrackedEntity> { entity };
            processed.Add(entity);

            foreach (var other in entities.Where(other => !processed.Contains(other)).Where(other =>
                         Vector2.DistanceSquared(entity.Entity.GridPos, other.Entity.GridPos) <= clusterRadiusSq))
            {
                cluster.Add(other);
                processed.Add(other);
            }

            if (cluster.Count >= minClusterSize) clusters.Add(cluster);
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

    private void ApplyWeightSmoothing(List<TrackedEntity> entities, float smoothingFactor)
    {
        foreach (var entity in entities)
        {
            if (_previousWeights.TryGetValue(entity.Entity, out var previousWeight))
                entity.Weight = previousWeight + (entity.Weight - previousWeight) * smoothingFactor;
            _previousWeights[entity.Entity] = entity.Weight;
        }
    }

    private void CleanupCaches(List<TrackedEntity> currentEntities)
    {
        var currentEntitySet = new HashSet<Entity>(currentEntities.Select(x => x.Entity));

        foreach (var key in _previousWeights.Keys.ToList().Where(key => !currentEntitySet.Contains(key)))
            _previousWeights.Remove(key);

        foreach (var key in _cachedRarities.Keys.ToList().Where(key => !currentEntitySet.Contains(key)))
            _cachedRarities.Remove(key);

        foreach (var key in _cachedLife.Keys.ToList().Where(key => !currentEntitySet.Contains(key)))
            _cachedLife.Remove(key);
    }
}
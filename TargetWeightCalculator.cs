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

    public void UpdateWeights(List<TrackedEntity> entities, Vector2 playerPosition, AutoMyAimSettings settings,
        ClusterManager clusterManager)
    {
        if (!settings.Targeting.Weights.EnableWeighting) return;

        var clusterSettings = settings.Targeting.Weights.Cluster;

        // Update base weights
        foreach (var entity in entities)
        {
            entity.Distance = entity.Entity.DistancePlayer;

            if (entity.Distance > settings.Targeting.MaxTargetDistance)
            {
                entity.Weight = 0f;
                continue;
            }

            entity.Weight = FastCalculateBaseWeight(entity, entity.Distance, settings);
        }

        // Process clustering if enabled
        if (clusterSettings.EnableClustering)
        {
            clusterManager.UpdateClusters(entities, clusterSettings.ClusterRadius.Value,
                clusterSettings.MinClusterSize.Value);
            var entitiesInClusters = new HashSet<Entity>();

            foreach (var cluster in clusterManager.CurrentClusters)
                ApplyClusterWeights(cluster, settings, entitiesInClusters);

            // Apply isolation penalty
            if (clusterSettings.EnableIsolationPenalty)
                ApplyIsolationPenalties(entities, entitiesInClusters, settings);
        }

        // Apply weight smoothing if enabled
        if (settings.Targeting.Weights.Smoothing.EnableSmoothing)
            ApplyWeightSmoothing(entities, settings.Targeting.Weights.Smoothing.SmoothingFactor.Value);

        // Cleanup old cache entries every 100 frames
        if (Environment.TickCount % 100 == 0)
            CleanupCaches(entities);
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

        return weight;
    }

    private void ApplyClusterWeights(ClusterInfo cluster, AutoMyAimSettings settings,
        HashSet<Entity> entitiesInClusters)
    {
        var clusterSettings = settings.Targeting.Weights.Cluster;
        var clusterBonus = Math.Min(
            1.0f + (cluster.Entities.Count - clusterSettings.MinClusterSize.Value) *
            clusterSettings.BaseClusterBonus.Value,
            clusterSettings.MaxClusterBonus.Value
        );

        foreach (var entity in cluster.Entities)
        {
            entity.Weight *= clusterBonus;

            if (clusterSettings.EnableCoreBonus)
                ApplyCoreBonus(entity, cluster, settings);

            entitiesInClusters.Add(entity.Entity);
        }
    }

    private void ApplyCoreBonus(TrackedEntity entity, ClusterInfo cluster, AutoMyAimSettings settings)
    {
        var clusterSettings = settings.Targeting.Weights.Cluster;
        var distanceToCenter = Vector2.DistanceSquared(entity.Entity.GridPos, cluster.Center);
        var coreRadiusSq = Math.Pow(cluster.Radius * clusterSettings.CoreRadiusPercent.Value, 2);

        if (distanceToCenter <= coreRadiusSq)
            entity.Weight *= clusterSettings.CoreBonusMultiplier.Value;
    }

    private void ApplyIsolationPenalties(List<TrackedEntity> entities, HashSet<Entity> entitiesInClusters,
        AutoMyAimSettings settings)
    {
        foreach (var entity in entities.Where(entity => !entitiesInClusters.Contains(entity.Entity)))
        {
            var rarity = _cachedRarities.TryGetValue(entity.Entity, out var r)
                ? r
                : entity.Entity.GetComponent<ObjectMagicProperties>()?.Rarity ?? MonsterRarity.White;

            if (rarity is MonsterRarity.White or MonsterRarity.Magic)
                entity.Weight *= settings.Targeting.Weights.Cluster.IsolationPenaltyMultiplier.Value;
        }
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
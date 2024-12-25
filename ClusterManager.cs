using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AutoMyAim.Structs;
using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory.MemoryObjects;
using ExileCore2.Shared.Enums;
using ExileCore2.Shared.Helpers;
using Graphics = ExileCore2.Graphics;

namespace AutoMyAim;

public class ClusterManager
{
    private bool _hasValidRenderState;
    public List<ClusterInfo> CurrentClusters { get; private set; } = new();

    public void UpdateClusters(List<TrackedEntity> entities, float clusterRadius, int minClusterSize)
    {
        var clusterRadiusSq = clusterRadius * clusterRadius;
        CurrentClusters = IdentifyClusters(entities, clusterRadiusSq, minClusterSize);
        _hasValidRenderState = true;
    }

    public void Render(Graphics graphics, Entity player)
    {
        if (!_hasValidRenderState || player == null || !AutoMyAim.Main.Settings.Render.ClusterVisuals.ShowClusters)
            return;


        var borderThickness = AutoMyAim.Main.Settings.Render.ClusterVisuals.BorderThickness.Value;
        var fillColor = AutoMyAim.Main.Settings.Render.ClusterVisuals.ClusterFillColor.Value;
        var borderColor = AutoMyAim.Main.Settings.Render.ClusterVisuals.ClusterBorderColor.Value;
        var showInfo = AutoMyAim.Main.Settings.Render.ClusterVisuals.ShowClusterInfo;
        var textColor = AutoMyAim.Main.Settings.Render.ClusterVisuals.ClusterInfoTextColor.Value;

        foreach (var cluster in CurrentClusters)
        {
            if (cluster.Entities.Count == 0) continue;

            var worldPos = new Vector3(cluster.Center.GridToWorld(), player.Pos.Z);
            var radius = cluster.Radius * PoeMapExtension.TileToGridConversion / 2;

            graphics.DrawFilledCircleInWorld(worldPos, radius, fillColor);

            if (borderThickness > 0) graphics.DrawCircleInWorld(worldPos, radius, borderColor, borderThickness);

            if (showInfo)
            {
                var screenPos = AutoMyAim.Main.GameController.IngameState.Camera.WorldToScreen(worldPos);
                if (screenPos != Vector2.Zero)
                {
                    var stats = GetClusterStats(cluster);
                    var text = $"Size: {cluster.Entities.Count}\n{stats}";
                    AutoMyAim.Main.Graphics.DrawText(text, screenPos, textColor);
                }
            }
        }
    }

    private string GetClusterStats(ClusterInfo cluster)
    {
        var avgWeight = cluster.Entities.Average(e => e.Weight);
        var rareCounts = cluster.Entities
            .GroupBy(e => e.Entity.GetComponent<ObjectMagicProperties>()?.Rarity ?? MonsterRarity.White)
            .ToDictionary(g => g.Key, g => g.Count());

        var normal = rareCounts.GetValueOrDefault(MonsterRarity.White, 0);
        var magic = rareCounts.GetValueOrDefault(MonsterRarity.Magic, 0);
        var rare = rareCounts.GetValueOrDefault(MonsterRarity.Rare, 0);
        var unique = rareCounts.GetValueOrDefault(MonsterRarity.Unique, 0);

        return $"Avg Weight: {avgWeight:F1}\nNormal: {normal}\nMagic: {magic}\nRare: {rare}\nUnique: {unique}";
    }

    public void ClearRenderState()
    {
        _hasValidRenderState = false;
        CurrentClusters.Clear();
    }

    private List<ClusterInfo> IdentifyClusters(List<TrackedEntity> entities, float clusterRadiusSq, int minClusterSize)
    {
        var clusters = new List<ClusterInfo>();
        var processed = new HashSet<TrackedEntity>();

        // First pass: Create initial clusters
        foreach (var entity in entities)
        {
            if (processed.Contains(entity)) continue;

            var newClusterEntities = new List<TrackedEntity> { entity };
            processed.Add(entity);

            // Find immediate neighbors
            foreach (var other in entities.Where(other => !processed.Contains(other)))
                if (Vector2.DistanceSquared(entity.Entity.GridPos, other.Entity.GridPos) <= clusterRadiusSq)
                {
                    newClusterEntities.Add(other);
                    processed.Add(other);
                }

            if (newClusterEntities.Count >= minClusterSize)
            {
                var clusterInfo = CalculateClusterInfo(newClusterEntities);
                clusters.Add(clusterInfo);
            }
        }

        // Second pass: Merge clusters
        return MergeClusters(clusters, clusterRadiusSq);
    }

    private List<ClusterInfo> MergeClusters(List<ClusterInfo> clusters, float baseClusterRadiusSq)
    {
        bool merged;
        do
        {
            merged = false;
            for (var i = 0; i < clusters.Count; i++)
            {
                for (var j = i + 1; j < clusters.Count; j++)
                    if (ShouldMergeClusters(clusters[i], clusters[j], baseClusterRadiusSq))
                    {
                        var mergedEntities = clusters[i].Entities.Concat(clusters[j].Entities).ToList();
                        clusters[i] = CalculateClusterInfo(mergedEntities);
                        clusters.RemoveAt(j);
                        merged = true;
                        break;
                    }

                if (merged) break;
            }
        } while (merged);

        return clusters;
    }

    private ClusterInfo CalculateClusterInfo(List<TrackedEntity> entities)
    {
        var center = entities.Aggregate(Vector2.Zero, (current, entity) => current + entity.Entity.GridPos) /
                     entities.Count;

        var maxDistSq = entities.Max(entity => Vector2.DistanceSquared(center, entity.Entity.GridPos));
        var baseRadius = MathF.Sqrt(maxDistSq);

        return new ClusterInfo
        {
            Entities = entities,
            Center = center,
            Radius = Math.Min(baseRadius, baseRadius + 5f) // Cap growth
        };
    }

    private bool ShouldMergeClusters(ClusterInfo cluster1, ClusterInfo cluster2, float baseClusterRadiusSq)
    {
        var distanceSq = Vector2.DistanceSquared(cluster1.Center, cluster2.Center);
        var maxMergeDistanceSq = baseClusterRadiusSq * 2f;

        if (distanceSq > maxMergeDistanceSq)
            return false;

        var combinedRadii = (cluster1.Radius + cluster2.Radius) * 0.75f;
        return distanceSq <= combinedRadii * combinedRadii;
    }
}
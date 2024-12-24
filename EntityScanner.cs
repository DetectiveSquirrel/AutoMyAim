using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AutoMyAim.Structs;
using ExileCore2;
using ExileCore2.PoEMemory.MemoryObjects;
using ExileCore2.Shared.Enums;

namespace AutoMyAim;

public class EntityScanner
{
    private readonly List<TrackedEntity> _trackedEntities = [];
    private readonly TargetWeightCalculator _weightCalculator;

    public EntityScanner()
    {
        _weightCalculator = new TargetWeightCalculator();
    }

    public List<TrackedEntity> GetTrackedEntities()
    {
        return _trackedEntities;
    }

    public void ClearEntities()
    {
        _trackedEntities.Clear();
    }

    public void ScanForEntities(Vector2 playerPos, GameController gameController)
    {
        _trackedEntities.Clear();
        var scanDistance = AutoMyAim.Main.Settings.Targeting.EntityScanDistance.Value;

        foreach (var entity in gameController.EntityListWrapper.ValidEntitiesByType[EntityType.Monster]
                     .Where(x => !ShouldExcludeEntity(x)))
        {
            if (!IsEntityValid(entity)) continue;

            var distance = Vector2.Distance(playerPos, entity.GridPos);
            if (distance <= scanDistance && AutoMyAim.Main._rayCaster.IsPositionVisible(entity.GridPos))
                _trackedEntities.Add(new TrackedEntity
                {
                    Entity = entity,
                    Distance = distance,
                    Weight = 0f
                });
        }
    }

    public void UpdateEntityWeights(Vector2 playerPos)
    {
        _trackedEntities.RemoveAll(tracked => !tracked.Entity?.IsValid == true || !tracked.Entity.IsAlive);

        foreach (var tracked in _trackedEntities)
            tracked.Distance = Vector2.Distance(playerPos, tracked.Entity.GridPos);

        _weightCalculator.UpdateWeights(_trackedEntities, playerPos, AutoMyAim.Main.Settings);

        if (AutoMyAim.Main.Settings.Targeting.Weights.EnableWeighting)
            _trackedEntities.Sort((a, b) => b.Weight.CompareTo(a.Weight));
    }

    private bool ShouldExcludeEntity(Entity entity)
    {
        return entity?.Path?.StartsWith("Metadata/Monsters/MonsterMods/") == true;
    }

    private bool IsEntityValid(Entity entity)
    {
        if (entity == null) return false;

        if (!entity.IsValid ||
            !entity.IsAlive ||
            entity.IsDead ||
            !entity.IsTargetable ||
            entity.IsHidden ||
            !entity.IsHostile)
            return false;

        return !entity.Stats.TryGetValue(GameStat.CannotBeDamaged, out var value) || value != 1;
    }
}
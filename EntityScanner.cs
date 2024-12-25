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
    private readonly List<Entity> _inRangeEntities = []; // Store entities in range before visibility check
    private readonly List<TrackedEntity> _trackedEntities = [];
    private readonly TargetWeightCalculator _weightCalculator = new();

    public List<TrackedEntity> GetTrackedEntities()
    {
        return _trackedEntities;
    }

    public void ClearEntities()
    {
        _trackedEntities.Clear();
        _inRangeEntities.Clear();
    }

    // collect entities within range
    public List<Vector2> ScanForInRangeEntities(Vector2 playerPos, GameController gameController)
    {
        _inRangeEntities.Clear();
        var positions = new List<Vector2>();
        var scanDistance = AutoMyAim.Main.Settings.Targeting.EntityScanDistance.Value;

        foreach (var entity in gameController.EntityListWrapper.ValidEntitiesByType[EntityType.Monster]
                     .Where(x => x.DistancePlayer <= scanDistance)
                     .Where(x => !ShouldExcludeEntity(x)))
        {
            if (!IsEntityValid(entity)) continue;

            _inRangeEntities.Add(entity);
            positions.Add(entity.GridPos);
        }

        return positions;
    }

    // process entities after the rays have been cast flipping tile visibility
    public void ProcessVisibleEntities(Vector2 playerPos)
    {
        _trackedEntities.Clear();

        foreach (var entity in _inRangeEntities)
        {
            // probably dont need this here, again in the same tick cycle but it works fine so meh.
            if (!IsEntityValid(entity)) continue;

            if (AutoMyAim.Main._rayCaster.IsPositionVisible(entity.GridPos))
            {
                var distance = Vector2.Distance(playerPos, entity.GridPos);
                _trackedEntities.Add(new TrackedEntity
                {
                    Entity = entity,
                    Distance = distance,
                    Weight = 0f
                });
            }
        }
    }

    // finally update the weights of the entities
    public void UpdateEntityWeights(Vector2 playerPos)
    {
        _trackedEntities.RemoveAll(tracked => !tracked.Entity?.IsValid == true || !tracked.Entity.IsAlive);

        foreach (var tracked in _trackedEntities)
            tracked.Distance = Vector2.Distance(playerPos, tracked.Entity.GridPos);

        _weightCalculator.UpdateWeights(_trackedEntities, playerPos, AutoMyAim.Main.Settings);

        if (AutoMyAim.Main.Settings.Targeting.Weights.EnableWeighting)
            _trackedEntities.Sort((a, b) => b.Weight.CompareTo(a.Weight));
    }

    // do any entity exclusions here
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

        // monsters sitting in essences, maybe a boss in a phase (havnt looked at that part, lazy)
        return !entity.Stats.TryGetValue(GameStat.CannotBeDamaged, out var value) || value != 1;
    }
}
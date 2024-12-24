using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ExileCore2;
using ExileCore2.PoEMemory.MemoryObjects;
using ExileCore2.Shared.Helpers;
using ImGuiNET;

namespace AutoMyAim;

public class RayCaster
{
    private readonly List<(Vector2 Pos, int Value)> _gridPointsCache = [];
    private readonly List<(Vector2 Start, Vector2 End)> _rayLines = [];
    private readonly HashSet<Vector2> _visiblePoints = [];

    private Vector2 _areaDimensions;
    private RaycastRenderConfig _currentConfig;
    private Vector2 _observerPos;
    private float _observerZ;
    private int[][] _pathfindingCache;

    public void InitializeConfig(RaycastRenderConfig config)
    {
        _currentConfig = config;
    }

    public void UpdateArea(GameController gameController)
    {
        var rawData = gameController.IngameState.Data.RawTerrainTargetingData;
        _areaDimensions = gameController.IngameState.Data.AreaDimensions;

        _pathfindingCache = new int[rawData.Length][];
        for (var y = 0; y < rawData.Length; y++)
        {
            _pathfindingCache[y] = new int[rawData[y].Length];
            Array.Copy(rawData[y], _pathfindingCache[y], rawData[y].Length);
        }
    }

    public void UpdateObserverAndEntities(Vector2 position, IEnumerable<Entity> entities)
    {
        _observerPos = position;
        GeneratePoints(); // Still generate grid points for visualization
        CastRaysToEntities(entities); // New targeted raycasting
    }

    private void GeneratePoints()
    {
        if (_currentConfig == null) return;

        _gridPointsCache.Clear();
        var size = _currentConfig.GridSize.Value;

        for (var y = -size; y <= size; y++)
        for (var x = -size; x <= size; x++)
        {
            if (x * x + y * y > size * size) continue;

            var pos = new Vector2(_observerPos.X + x, _observerPos.Y + y);
            var value = GetPathfindingValue(pos);
            if (value >= 0) _gridPointsCache.Add((pos, value));
        }
    }

    private void CastRaysToEntities(IEnumerable<Entity> entities)
    {
        try
        {
            _visiblePoints.Clear();
            _rayLines.Clear();

            // Always add observer position as visible
            _visiblePoints.Add(_observerPos);

            // First validate all entities and their positions
            var validEntities = entities
                .Where(e => e != null && e.IsValid && !e.IsHidden)
                .Where(e => IsValidPosition(e.GridPos))
                .ToList();

            foreach (var entity in validEntities)
                try
                {
                    CastRay(entity.GridPos);
                }
                catch (Exception)
                {
                    // If casting to a specific entity fails, continue with others
                }
        }
        catch (Exception)
        {
            // If something goes wrong, clear everything to prevent hanging
            _visiblePoints.Clear();
            _rayLines.Clear();
            _visiblePoints.Add(_observerPos);
        }
    }

    private void CastRay(Vector2 targetPos)
    {
        var direction = Vector2.Normalize(targetPos - _observerPos);
        var currentPos = new Vector2((int)_observerPos.X, (int)_observerPos.Y);
        var rayEnd = currentPos;

        var deltaDist = new Vector2(
            Math.Abs(1f / direction.X),
            Math.Abs(1f / direction.Y)
        );

        var step = new Vector2(
            direction.X < 0 ? -1 : 1,
            direction.Y < 0 ? -1 : 1
        );

        var sideDist = new Vector2(
            (direction.X < 0 ? _observerPos.X - currentPos.X : currentPos.X + 1f - _observerPos.X) * deltaDist.X,
            (direction.Y < 0 ? _observerPos.Y - currentPos.Y : currentPos.Y + 1f - _observerPos.Y) * deltaDist.Y
        );

        // Cast until we hit the target or a wall
        while (currentPos != targetPos)
        {
            if (sideDist.X < sideDist.Y)
            {
                sideDist.X += deltaDist.X;
                currentPos.X += step.X;
            }
            else
            {
                sideDist.Y += deltaDist.Y;
                currentPos.Y += step.Y;
            }

            if (!IsValidPosition(currentPos)) break;

            rayEnd = currentPos;
            var terrainValue = GetPathfindingValue(currentPos);

            if (terrainValue < _currentConfig.TargetLayerValue.Value)
                continue;

            _visiblePoints.Add(new Vector2((int)currentPos.X, (int)currentPos.Y));

            // Stop if we hit a wall
            if (terrainValue <= _currentConfig.TargetLayerValue.Value)
                break;
        }

        _rayLines.Add((_observerPos, rayEnd));
    }

    private bool IsValidPosition(Vector2 pos)
    {
        try
        {
            var x = (int)pos.X;
            var y = (int)pos.Y;
            return x >= 0 && x < _areaDimensions.X &&
                   y >= 0 && y < _areaDimensions.Y;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public bool IsPositionVisible(Vector2 position)
    {
        try
        {
            if (!IsValidPosition(position)) return false;
            return _visiblePoints.Contains(position);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private int GetPathfindingValue(Vector2 position)
    {
        try
        {
            var x = (int)position.X;
            var y = (int)position.Y;

            return x >= 0 && x < _areaDimensions.X && y >= 0 && y < _areaDimensions.Y
                ? _pathfindingCache[y][x]
                : -1;
        }
        catch
        {
            return -1;
        }
    }

    public void Render(ImDrawListPtr drawList, GameController gameController, RaycastRenderConfig config)
    {
        try
        {
            _currentConfig.ShowRayLines = config.ShowRayLines;
            _currentConfig.ShowTerrainValues = config.ShowTerrainValues;
            _currentConfig.RayLineThickness = config.RayLineThickness;
            _currentConfig.VisibleColor = config.VisibleColor;
            _currentConfig.ShadowColor = config.ShadowColor;
            _currentConfig.RayLineColor = config.RayLineColor;
            _currentConfig.DrawAtPlayerPlane = config.DrawAtPlayerPlane;
            _observerZ = gameController.IngameState.Data.GetTerrainHeightAt(_observerPos);

            if (config.ShowRayLines)
                foreach (var (start, end) in _rayLines)
                {
                    var startWorld = new Vector3(start.GridToWorld(), _observerZ);
                    var endWorld = new Vector3(end.GridToWorld(), _observerZ);

                    var startScreen = gameController.IngameState.Camera.WorldToScreen(startWorld);
                    var endScreen = gameController.IngameState.Camera.WorldToScreen(endWorld);

                    if (startScreen != Vector2.Zero && endScreen != Vector2.Zero)
                        drawList.AddLine(
                            startScreen,
                            endScreen,
                            config.RayLineColor.Value.ToImgui(),
                            config.RayLineThickness
                        );
                }

            if (config.ShowTerrainValues)
                foreach (var (pos, value) in _gridPointsCache)
                {
                    var z = config.DrawAtPlayerPlane
                        ? _observerZ
                        : gameController.IngameState.Data.GetTerrainHeightAt(pos);
                    var worldPos = new Vector3(pos.GridToWorld(), z);
                    var screenPos = gameController.IngameState.Camera.WorldToScreen(worldPos);

                    if (screenPos != Vector2.Zero)
                    {
                        var color = _visiblePoints.Contains(pos)
                            ? config.VisibleColor.Value.ToImgui()
                            : config.ShadowColor.Value.ToImgui();

                        drawList.AddText(screenPos, color, value.ToString());
                    }
                }
        }
        catch (Exception)
        {
            // Ignore render errors
        }
    }
}
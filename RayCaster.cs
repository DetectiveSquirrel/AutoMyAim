using System;
using System.Collections.Generic;
using System.Numerics;
using ExileCore2;
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

    public void UpdateObserver(Vector2 position)
    {
        _observerPos = position;
        GeneratePoints();
        CastRays();
    }

    public bool IsPositionVisible(Vector2 position)
    {
        return _visiblePoints.Contains(position);
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

    private void CastRays()
    {
        if (_currentConfig == null) return;

        _visiblePoints.Clear();
        _rayLines.Clear();
        _visiblePoints.Add(_observerPos);

        var angleStep = 360f / _currentConfig.RayCount.Value;
        for (var angle = 0f; angle < 360; angle += angleStep)
        {
            var radians = angle * (float)Math.PI / 180f;
            var direction = new Vector2((float)Math.Cos(radians), (float)Math.Sin(radians));
            CastRay(_observerPos, Vector2.Normalize(direction));
        }
    }

    private void CastRay(Vector2 start, Vector2 direction)
    {
        var mapPos = new Vector2((int)start.X, (int)start.Y);
        var deltaDist = new Vector2(Math.Abs(1f / direction.X), Math.Abs(1f / direction.Y));
        var step = new Vector2(direction.X < 0 ? -1 : 1, direction.Y < 0 ? -1 : 1);
        var sideDist = new Vector2(
            (direction.X < 0 ? start.X - mapPos.X : mapPos.X + 1f - start.X) * deltaDist.X,
            (direction.Y < 0 ? start.Y - mapPos.Y : mapPos.Y + 1f - start.Y) * deltaDist.Y
        );

        var maxSteps = _currentConfig.GridSize.Value * 2;
        var rayEnd = start;

        for (var steps = 0; steps < maxSteps; steps++)
        {
            if (sideDist.X < sideDist.Y)
            {
                sideDist.X += deltaDist.X;
                mapPos.X += step.X;
            }
            else
            {
                sideDist.Y += deltaDist.Y;
                mapPos.Y += step.Y;
            }

            rayEnd = mapPos;
            var terrainValue = GetPathfindingValue(mapPos);
            if (terrainValue < _currentConfig.TargetLayerValue.Value) continue;
            _visiblePoints.Add(new Vector2((int)mapPos.X, (int)mapPos.Y));
            if (terrainValue <= _currentConfig.TargetLayerValue.Value) break;
        }

        _rayLines.Add((start, rayEnd));
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
                var z = config.DrawAtPlayerPlane ? _observerZ : gameController.IngameState.Data.GetTerrainHeightAt(pos);
                var worldPos = new Vector3(pos.GridToWorld(), z);
                var screenPos = gameController.IngameState.Camera.WorldToScreen(worldPos);
                var color = _visiblePoints.Contains(pos)
                    ? config.VisibleColor.Value.ToImgui()
                    : config.ShadowColor.Value.ToImgui();

                drawList.AddText(screenPos, color, value.ToString());
            }
    }
}
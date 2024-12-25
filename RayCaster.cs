using System;
using System.Collections.Generic;
using System.Numerics;
using AutoMyAim.Structs;
using ExileCore2;
using ExileCore2.Shared.Helpers;
using ImGuiNET;

namespace AutoMyAim;

public class RayCaster
{
    private readonly List<(Vector2 Pos, int Value)> _gridPointsCache = [];
    private readonly List<(Vector2 Start, Vector2 End, bool IsVisible)> _targetRays = [];
    private readonly HashSet<Vector2> _visiblePoints = []; // For visualization only
    private readonly HashSet<Vector2> _visibleTargets = [];

    private Vector2 _areaDimensions;
    private RaycastRenderConfig _currentConfig;
    private Vector2 _observerPos;
    private float _observerZ;
    private int[][] _terrainData;

    public void InitializeConfig(RaycastRenderConfig config)
    {
        _currentConfig = config;
    }

    public void UpdateArea(GameController gameController)
    {
        _areaDimensions = gameController.IngameState.Data.AreaDimensions;

        var rawData = AutoMyAim.Main.Settings.UseWalkableTerrainInsteadOfTargetTerrain
            ? gameController.IngameState.Data.RawPathfindingData
            : gameController.IngameState.Data.RawTerrainTargetingData;
        _terrainData = new int[rawData.Length][];
        for (var y = 0; y < rawData.Length; y++)
        {
            _terrainData[y] = new int[rawData[y].Length];
            Array.Copy(rawData[y], _terrainData[y], rawData[y].Length);
        }
    }

    public void UpdateObserver(Vector2 position, List<Vector2> targetPositions = null)
    {
        _observerPos = position;
        _visibleTargets.Clear();
        _targetRays.Clear();
        _visiblePoints.Clear();

        // Generate grid points for visualization
        GenerateGridPoints();

        // Process actual target checks and collect points along the way
        if (targetPositions != null)
            foreach (var targetPos in targetPositions)
            {
                var isVisible = HasLineOfSightAndCollectPoints(_observerPos, targetPos);
                _targetRays.Add((_observerPos, targetPos, isVisible));
                if (isVisible) _visibleTargets.Add(targetPos);
            }
    }

    private void GenerateGridPoints()
    {
        if (_currentConfig == null) return;

        _gridPointsCache.Clear();
        var size = _currentConfig.GridSize.Value;

        for (var y = -size; y <= size; y++)
        for (var x = -size; x <= size; x++)
        {
            if (x * x + y * y > size * size) continue;

            var pos = new Vector2(_observerPos.X + x, _observerPos.Y + y);
            var value = GetTerrainValue(pos);
            if (value >= 0) _gridPointsCache.Add((pos, value));
        }
    }

    public bool IsPositionVisible(Vector2 position)
    {
        return _visibleTargets.Contains(position) || HasLineOfSight(_observerPos, position);
    }

    private bool HasLineOfSightAndCollectPoints(Vector2 start, Vector2 end)
    {
        var startX = (int)start.X;
        var startY = (int)start.Y;
        var endX = (int)end.X;
        var endY = (int)end.Y;

        var dx = Math.Abs(endX - startX);
        var dy = Math.Abs(endY - startY);

        var x = startX;
        var y = startY;

        var stepX = startX < endX ? 1 : -1;
        var stepY = startY < endY ? 1 : -1;

        // Handle straight lines efficiently
        if (dx == 0)
        {
            // Vertical line
            var step = stepY;
            for (var i = 0; i < dy; i++)
            {
                y += step;
                var pos = new Vector2(x, y);
                var terrainValue = GetTerrainValue(pos);
                _visiblePoints.Add(pos); // Add point for visualization
                if (terrainValue < _currentConfig.TargetLayerValue.Value) continue;
                if (terrainValue <= _currentConfig.TargetLayerValue.Value) return false;
            }

            return true;
        }

        if (dy == 0)
        {
            // Horizontal line
            var step = stepX;
            for (var i = 0; i < dx; i++)
            {
                x += step;
                var pos = new Vector2(x, y);
                var terrainValue = GetTerrainValue(pos);
                _visiblePoints.Add(pos); // Add point for visualization
                if (terrainValue < _currentConfig.TargetLayerValue.Value) continue;
                if (terrainValue <= _currentConfig.TargetLayerValue.Value) return false;
            }

            return true;
        }

        // DDA for diagonal lines
        var deltaErr = Math.Abs((float)dy / dx);
        var error = 0.0f;

        if (dx >= dy)
        {
            // Drive by X
            for (var i = 0; i < dx; i++)
            {
                x += stepX;
                error += deltaErr;

                if (error >= 0.5f)
                {
                    y += stepY;
                    error -= 1.0f;
                }

                var pos = new Vector2(x, y);
                var terrainValue = GetTerrainValue(pos);
                _visiblePoints.Add(pos); // Add point for visualization
                if (terrainValue < _currentConfig.TargetLayerValue.Value) continue;
                if (terrainValue <= _currentConfig.TargetLayerValue.Value) return false;
            }
        }
        else
        {
            // Drive by Y
            deltaErr = Math.Abs((float)dx / dy);
            for (var i = 0; i < dy; i++)
            {
                y += stepY;
                error += deltaErr;

                if (error >= 0.5f)
                {
                    x += stepX;
                    error -= 1.0f;
                }

                var pos = new Vector2(x, y);
                var terrainValue = GetTerrainValue(pos);
                _visiblePoints.Add(pos); // Add point for visualization
                if (terrainValue < _currentConfig.TargetLayerValue.Value) continue;
                if (terrainValue <= _currentConfig.TargetLayerValue.Value) return false;
            }
        }

        return true;
    }

    private bool HasLineOfSight(Vector2 start, Vector2 end)
    {
        return
            HasLineOfSightAndCollectPoints(start,
                end); // For now reuse the collecting version because i cant be bothered changing it
    }

    private int GetTerrainValue(Vector2 position)
    {
        var x = (int)position.X;
        var y = (int)position.Y;

        return x >= 0 && x < _areaDimensions.X && y >= 0 && y < _areaDimensions.Y
            ? _terrainData[y][x]
            : -1;
    }

    public void Render(ImDrawListPtr drawList, GameController gameController, RaycastRenderConfig config)
    {
        _currentConfig.ShowRayLines = config.ShowRayLines;
        _currentConfig.ShowTerrainValues = config.ShowTerrainValues;
        _currentConfig.VisibleColor = config.VisibleColor;
        _currentConfig.ShadowColor = config.ShadowColor;
        _currentConfig.RayLineColor = config.RayLineColor;
        _currentConfig.DrawAtPlayerPlane = config.DrawAtPlayerPlane;
        _observerZ = gameController.IngameState.Data.GetTerrainHeightAt(_observerPos);

        // Draw terrain grid
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

        // Draw ray lines to targets
        if (config.ShowRayLines)
            foreach (var (start, end, isVisible) in _targetRays)
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

                var pointColor = isVisible ? config.VisibleColor.Value.ToImgui() : config.ShadowColor.Value.ToImgui();
                drawList.AddCircleFilled(endScreen, 5f, pointColor);
            }
    }
}
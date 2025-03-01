using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using ExileCore2;
using ExileCore2.Shared.Enums;
using ExileCore2.Shared.Helpers;

namespace AutoMyAim;

public class RayCaster
{
    private readonly List<(Vector2 Pos, int Value)> _gridPointsCache = [];
    private readonly List<(Vector2 Start, Vector2 End, bool IsVisible)> _targetRays = [];
    private readonly HashSet<Vector2> _visiblePoints = []; // For visualization only
    private readonly HashSet<Vector2> _visibleTargets = [];

    private Vector2 _areaDimensions;
    private Vector2 _observerPos;
    private float _observerZ;
    private int[][] _terrainData;

    public void UpdateArea()
    {
        _areaDimensions = AutoMyAim.Main.GameController.IngameState.Data.AreaDimensions;

        var rawData = AutoMyAim.Main.Settings.UseWalkableTerrainInsteadOfTargetTerrain
            ? AutoMyAim.Main.GameController.IngameState.Data.RawPathfindingData
            : AutoMyAim.Main.GameController.IngameState.Data.RawTerrainTargetingData;

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

        GenerateGridPoints();

        if (targetPositions == null) return;

        foreach (var targetPos in targetPositions)
        {
            var isVisible = HasLineOfSightAndCollectPoints(_observerPos, targetPos);
            _targetRays.Add((_observerPos, targetPos, isVisible));
            if (isVisible) _visibleTargets.Add(targetPos);
        }
    }

    private void GenerateGridPoints()
    {
        _gridPointsCache.Clear();
        var size = AutoMyAim.Main.Settings.Raycast.Visuals.GridSize.Value;

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

        var targetLayerValue = AutoMyAim.Main.Settings.Raycast.TargetLayerValue.Value;

        // Handle straight lines efficiently
        if (dx == 0)
        {
            // Vertical line
            for (var i = 0; i < dy; i++)
            {
                y += stepY;
                var pos = new Vector2(x, y);
                var terrainValue = GetTerrainValue(pos);
                if (terrainValue <= targetLayerValue) return false;
                _visiblePoints.Add(pos);
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
                if (terrainValue <= targetLayerValue) return false;
                _visiblePoints.Add(pos);
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
                if (terrainValue <= targetLayerValue) return false;
                _visiblePoints.Add(pos);
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
                if (terrainValue <= targetLayerValue) return false;
                _visiblePoints.Add(pos);
            }
        }

        return true;
    }

    private bool HasLineOfSight(Vector2 start, Vector2 end)
    {
        return HasLineOfSightAndCollectPoints(start, end);
    }

    private int GetTerrainValue(Vector2 position)
    {
        var x = (int)position.X;
        var y = (int)position.Y;

        return x >= 0 && x < _areaDimensions.X && y >= 0 && y < _areaDimensions.Y
            ? _terrainData[y][x]
            : -1;
    }

    public void Render(GameController gameController)
    {
        _observerZ = gameController.IngameState.Data.GetTerrainHeightAt(_observerPos);

        if (AutoMyAim.Main.Settings.Raycast.Visuals.ShowTerrainValues)
            RenderTerrainGrid(gameController);

        if (AutoMyAim.Main.Settings.Raycast.Visuals.ShowRayLines)
            RenderRayLines(gameController);
    }

    private void RenderTerrainGrid(GameController gameController)
    {
        foreach (var (pos, value) in _gridPointsCache)
        {
            var z = AutoMyAim.Main.Settings.Raycast.Visuals.DrawAtPlayerPlane
                ? _observerZ
                : gameController.IngameState.Data.GetTerrainHeightAt(pos);
            var worldPos = new Vector3(pos.GridToWorld(), z);
            var screenPos = gameController.IngameState.Camera.WorldToScreen(worldPos);

            Color color;
            if (_visiblePoints.Contains(pos))
                color = AutoMyAim.Main.Settings.Raycast.Visuals.EntityColors.Visible.Value;
            else if (AutoMyAim.Main.Settings.Raycast.Visuals.TerrainColors.EnableTerrainColorization &&
                     value is >= 0 and <= 5)
                // Get the appropriate terrain color based on value
                color = value switch
                {
                    0 => AutoMyAim.Main.Settings.Raycast.Visuals.TerrainColors.Tile0.Value,
                    1 => AutoMyAim.Main.Settings.Raycast.Visuals.TerrainColors.Tile1.Value,
                    2 => AutoMyAim.Main.Settings.Raycast.Visuals.TerrainColors.Tile2.Value,
                    3 => AutoMyAim.Main.Settings.Raycast.Visuals.TerrainColors.Tile3.Value,
                    4 => AutoMyAim.Main.Settings.Raycast.Visuals.TerrainColors.Tile4.Value,
                    5 => AutoMyAim.Main.Settings.Raycast.Visuals.TerrainColors.Tile5.Value,
                    _ => AutoMyAim.Main.Settings.Raycast.Visuals.EntityColors.Shadow.Value
                };
            else
                color = AutoMyAim.Main.Settings.Raycast.Visuals.EntityColors.Shadow.Value;

            AutoMyAim.Main.Graphics.DrawText(value.ToString(), screenPos, color,
                FontAlign.VerticalCenter | FontAlign.Center);
        }
    }

    private void RenderRayLines(GameController gameController)
    {
        foreach (var (start, end, isVisible) in _targetRays)
        {
            var startWorld = new Vector3(start.GridToWorld(), _observerZ);
            var endWorld = new Vector3(end.GridToWorld(),
                AutoMyAim.Main.Settings.Raycast.Visuals.DrawAtPlayerPlane
                    ? _observerZ
                    : gameController.IngameState.Data.GetTerrainHeightAt(end));

            var startScreen = gameController.IngameState.Camera.WorldToScreen(startWorld);
            var endScreen = gameController.IngameState.Camera.WorldToScreen(endWorld);

            AutoMyAim.Main.Graphics.DrawLine(
                startScreen,
                endScreen,
                AutoMyAim.Main.Settings.Raycast.Visuals.RayLineThickness,
                AutoMyAim.Main.Settings.Raycast.Visuals.EntityColors.RayLine.Value
            );

            var pointColor = isVisible
                ? AutoMyAim.Main.Settings.Raycast.Visuals.EntityColors.Visible.Value
                : AutoMyAim.Main.Settings.Raycast.Visuals.EntityColors.Shadow.Value;
            AutoMyAim.Main.Graphics.DrawCircleFilled(endScreen, 5f, pointColor, 25);
        }
    }
}
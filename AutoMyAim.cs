using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ExileCore2;
using ExileCore2.Shared;
using ExileCore2.Shared.Enums;
using ExileCore2.Shared.Helpers;
using ImGuiNET;

namespace AutoMyAim;

public class AutoMyAim : BaseSettingsPlugin<AutoMyAimSettings>
{
    private readonly Random _random = new();
    private readonly List<TrackedEntity> _trackedEntities = new();
    private TrackedEntity _currentTarget;
    private ImDrawListPtr _drawList;
    private Vector2 _lastPlayerPos;
    private RayCaster _rayCaster;
    private TargetWeightCalculator _weightCalculator;

    public override bool Initialise()
    {
        Name = "Auto My Aim";
        _rayCaster = new RayCaster();
        _weightCalculator = new TargetWeightCalculator();

        Input.RegisterKey(Settings.AimKey);
        Settings.AimKey.OnValueChanged += () => { Input.RegisterKey(Settings.AimKey); };
        return true;
    }

    public override void AreaChange(AreaInstance area)
    {
        _rayCaster.UpdateArea(GameController);
        _trackedEntities.Clear();
        _currentTarget = null;
    }

    public override void Tick()
    {
        if (!Settings.Enable || !Input.GetKeyState(Settings.AimKey.Value)) return;

        var player = GameController?.Player;
        if (player == null) return;

        var currentPos = player.GridPos;

        if (currentPos != _lastPlayerPos)
        {
            _lastPlayerPos = currentPos;
            _rayCaster.UpdateObserver(currentPos);
            ScanForEntities(currentPos);
        }

        UpdateEntityWeights(currentPos);

        var sortedEntities = _trackedEntities.OrderByDescending(x => x.Weight).ToList();
        if (!sortedEntities.Any()) return;

        TrackedEntity targetEntity = null;
        var rawPosToAim = Vector2.Zero;

        if (!Settings.PointToOffscreenTargetsOtherwiseFindNextTargetInBounds)
        {
            foreach (var entity in sortedEntities)
            {
                var pos = GameController.IngameState.Camera.WorldToScreen(entity.Entity.Pos);
                if (pos != Vector2.Zero && IsValidClickPosition(pos))
                {
                    targetEntity = entity;
                    rawPosToAim = pos;
                    break;
                }
            }

            if (targetEntity == null) return;
        }
        else
        {
            targetEntity = sortedEntities.First();
            rawPosToAim = GameController.IngameState.Camera.WorldToScreen(targetEntity.Entity.Pos);
            if (rawPosToAim == Vector2.Zero) return;
        }

        _currentTarget = targetEntity;

        if (ShouldWork() && _currentTarget != null)
        {
            var safePosToAim = GetSafeAimPosition(rawPosToAim);

            if (Settings.ConfineCursorToCircle)
            {
                var screenCenter = new Vector2(
                    GameController.Window.GetWindowRectangle().Width / 2,
                    GameController.Window.GetWindowRectangle().Height / 2
                );

                var vectorToTarget = safePosToAim - screenCenter;
                var distanceToTarget = vectorToTarget.Length();

                if (distanceToTarget > Settings.CursorCircleRadius)
                {
                    vectorToTarget = Vector2.Normalize(vectorToTarget) * Settings.CursorCircleRadius;
                    safePosToAim = screenCenter + vectorToTarget;
                }
            }

            if (IsValidClickPosition(safePosToAim))
            {
                var randomizedPos = GetRandomizedAimPosition(safePosToAim);
                if (IsValidClickPosition(randomizedPos)) Input.SetCursorPos(randomizedPos);
            }
        }
    }

    private void ScanForEntities(Vector2 playerPos)
    {
        _trackedEntities.Clear();
        var scanDistance = Settings.EntityScanDistance.Value;

        foreach (var entity in GameController.EntityListWrapper.ValidEntitiesByType[EntityType.Monster])
        {
            if (entity?.IsValid != true || !entity.IsAlive || !entity.IsTargetable || entity.IsHidden) continue;

            var distance = Vector2.Distance(playerPos, entity.GridPos);
            if (distance <= scanDistance && _rayCaster.IsPositionVisible(entity.GridPos))
            {
                var weight = Settings.EnableWeighting
                    ? _weightCalculator.CalculateWeight(Settings, entity, distance)
                    : 0f;

                _trackedEntities.Add(new TrackedEntity
                {
                    Entity = entity,
                    Distance = distance,
                    Weight = weight
                });
            }
        }
    }

    private void UpdateEntityWeights(Vector2 playerPos)
    {
        _trackedEntities.RemoveAll(tracked => !tracked.Entity?.IsValid == true || !tracked.Entity.IsAlive);

        foreach (var tracked in _trackedEntities)
        {
            tracked.Distance = Vector2.Distance(playerPos, tracked.Entity.GridPos);
            tracked.Weight = Settings.EnableWeighting
                ? _weightCalculator.CalculateWeight(Settings, tracked.Entity, tracked.Distance)
                : 0f;
        }

        if (Settings.EnableWeighting)
            _trackedEntities.Sort((a, b) => b.Weight.CompareTo(a.Weight));
    }

    private bool IsValidClickPosition(Vector2 pos)
    {
        var window = GameController.Window.GetWindowRectangle();

        var safeZone = new RectangleF(
            window.X + Settings.LeftPadding.Value,
            window.Y + Settings.TopPadding.Value,
            window.Width - (Settings.LeftPadding.Value + Settings.RightPadding.Value),
            window.Height - (Settings.TopPadding.Value + Settings.BottomPadding.Value)
        );

        return pos.X >= safeZone.Left &&
               pos.X <= safeZone.Right &&
               pos.Y >= safeZone.Top &&
               pos.Y <= safeZone.Bottom;
    }

    private Vector2 GetSafeAimPosition(Vector2 targetPos)
    {
        var window = GameController.Window.GetWindowRectangle();
        var screenCenter = new Vector2(
            window.X + window.Width / 2,
            window.Y + window.Height / 2
        );

        var safeZone = new RectangleF(
            window.X + Settings.LeftPadding.Value,
            window.Y + Settings.TopPadding.Value,
            window.Width - (Settings.LeftPadding.Value + Settings.RightPadding.Value),
            window.Height - (Settings.TopPadding.Value + Settings.BottomPadding.Value)
        );

        if (!(targetPos.X < safeZone.Left) && !(targetPos.X > safeZone.Right) &&
            !(targetPos.Y < safeZone.Top) && !(targetPos.Y > safeZone.Bottom)) return targetPos;
        var vectorToTarget = targetPos - screenCenter;

        var normalizedVector = Vector2.Normalize(vectorToTarget);

        float scaleX = float.MaxValue, scaleY = float.MaxValue;

        if (normalizedVector.X != 0)
        {
            if (vectorToTarget.X > 0)
                scaleX = (safeZone.Right - screenCenter.X) / normalizedVector.X;
            else
                scaleX = (safeZone.Left - screenCenter.X) / normalizedVector.X;
        }

        if (normalizedVector.Y != 0)
        {
            if (vectorToTarget.Y > 0)
                scaleY = (safeZone.Bottom - screenCenter.Y) / normalizedVector.Y;
            else
                scaleY = (safeZone.Top - screenCenter.Y) / normalizedVector.Y;
        }

        var scale = Math.Min(scaleX, scaleY);
        return screenCenter + normalizedVector * scale;
    }

    private Vector2 GetRandomizedAimPosition(Vector2 targetPos)
    {
        if (!Settings.RandomizeInRadius)
            return targetPos;

        var attempts = 0;
        const int maxAttempts = 10;

        while (attempts < maxAttempts)
        {
            var angle = _random.NextDouble() * Math.PI * 2;
            var distance = _random.NextDouble() * Settings.AcceptableRadius;

            var randomPos = new Vector2(
                targetPos.X + (float)(Math.Cos(angle) * distance),
                targetPos.Y + (float)(Math.Sin(angle) * distance)
            );

            if (IsValidClickPosition(randomPos))
                return randomPos;

            attempts++;
        }

        return targetPos;
    }

    public override void Render()
    {
        if (!Settings.EnableDrawing) return;
        if (!ShouldWork()) return;

        var rect = GameController.Window.GetWindowRectangle() with { Location = Vector2.Zero };
        SetupImGuiWindow(rect);

        var raycastConfig = new RaycastRenderConfig
        {
            ShowRayLines = Settings.ShowRayLines,
            ShowTerrainValues = Settings.ShowTerrainValues,
            TargetLayerValue = Settings.TargetLayerValue,
            GridSize = Settings.GridSize,
            RayCount = Settings.RayCount,
            RayLineThickness = Settings.RayLineThickness,
            VisibleColor = Settings.VisibleColor,
            ShadowColor = Settings.ShadowColor,
            RayLineColor = Settings.RayLineColor,
            DrawAtPlayerPlane = Settings.DrawAtPlayerPlane
        };

        _rayCaster.Render(_drawList, GameController, raycastConfig);
        RenderEntityWeights();
        RenderAimVisualization();
        ImGui.End();
    }

    private bool ShouldWork()
    {
        var ingameUi = GameController?.IngameState.IngameUi;

        if (!Settings.Enable) return false;
        if (GameController is not { InGame: true, Player: not null }) return false;
        if (GameController.Settings.CoreSettings.Enable) return false;
        if (!Settings.RenderOnFullPanels && ingameUi.FullscreenPanels.Any(x => x.IsVisible)) return false;
        if (!Settings.RenderOnleftPanels && ingameUi.OpenLeftPanel.IsVisible) return false;
        if (!ingameUi.OpenRightPanel.IsVisible) return true;
        return Settings.RenderOnRightPanels;
    }

    private void SetupImGuiWindow(RectangleF rect)
    {
        ImGui.SetNextWindowSize(new Vector2(rect.Width, rect.Height));
        ImGui.SetNextWindowPos(new Vector2(rect.Left, rect.Top));

        const ImGuiWindowFlags flags =
            ImGuiWindowFlags.NoDecoration |
            ImGuiWindowFlags.NoInputs |
            ImGuiWindowFlags.NoMove |
            ImGuiWindowFlags.NoScrollWithMouse |
            ImGuiWindowFlags.NoSavedSettings |
            ImGuiWindowFlags.NoFocusOnAppearing |
            ImGuiWindowFlags.NoBringToFrontOnFocus |
            ImGuiWindowFlags.NoBackground;

        ImGui.Begin("VisibilitySystem_DrawRegion", flags);
        _drawList = ImGui.GetWindowDrawList();
    }

    private void RenderEntityWeights()
    {
        if (!Settings.EnableWeighting || !Settings.ShowWeights) return;

        foreach (var trackedEntity in _trackedEntities)
        {
            var screenPos = GameController.IngameState.Camera.WorldToScreen(trackedEntity.Entity.Pos);
            if (screenPos != Vector2.Zero)
            {
                var text = $"({trackedEntity.Weight:F1})";
                _drawList.AddText(screenPos, Settings.WeightTextColor.Value.ToImgui(), text);
            }
        }
    }

    private void RenderAimVisualization()
    {
        if (Settings.ConfineCursorToCircle)
        {
            var screenCenter = new Vector2(
                GameController.Window.GetWindowRectangle().Width / 2,
                GameController.Window.GetWindowRectangle().Height / 2
            );
            _drawList.AddCircle(screenCenter, Settings.CursorCircleRadius,
                ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 0, 0.5f)), 64, 1f);
        }

        if (_currentTarget == null) return;

        var rawPosToAim = GameController.IngameState.Camera.WorldToScreen(_currentTarget.Entity.Pos);
        if (rawPosToAim == Vector2.Zero) return;

        var safePosToAim = GetSafeAimPosition(rawPosToAim);
        if (!IsValidClickPosition(safePosToAim)) return;

        _drawList.AddCircle(safePosToAim, Settings.AcceptableRadius,
            Settings.WeightTextColor.Value.ToImgui(), 32, 1f);

        if (Settings.ShowDebug)
        {
            var window = GameController.Window.GetWindowRectangle();
            var safeZone = new RectangleF(
                window.X + Settings.LeftPadding.Value,
                window.Y + Settings.TopPadding.Value,
                window.Width - (Settings.LeftPadding.Value + Settings.RightPadding.Value),
                window.Height - (Settings.TopPadding.Value + Settings.BottomPadding.Value)
            );

            _drawList.AddRect(
                new Vector2(safeZone.Left, safeZone.Top),
                new Vector2(safeZone.Right, safeZone.Bottom),
                ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 0, 0.5f))
            );
        }
    }
}
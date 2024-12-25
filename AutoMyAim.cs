using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AutoMyAim.Structs;
using ExileCore2;
using ExileCore2.PoEMemory.MemoryObjects;

namespace AutoMyAim;

public class AutoMyAim : BaseSettingsPlugin<AutoMyAimSettings>
{
    public static AutoMyAim Main;
    private readonly EntityScanner _entityScanner;
    private readonly InputHandler _inputHandler;
    internal readonly RayCaster _rayCaster;
    private readonly AimRenderer _renderer;
    private TrackedEntity _currentTarget;
    private bool _isAimToggled;

    public AutoMyAim()
    {
        Name = "Auto My Aim";
        _rayCaster = new RayCaster();
        _entityScanner = new EntityScanner();
        _inputHandler = new InputHandler();
        _renderer = new AimRenderer();
    }

    public override bool Initialise()
    {
        Main = this;

        var raycastConfig = new RaycastRenderConfig
        {
            ShowRayLines = Settings.Raycast.Visuals.ShowRayLines,
            ShowTerrainValues = Settings.Raycast.Visuals.ShowTerrainValues,
            TargetLayerValue = Settings.Raycast.TargetLayerValue,
            GridSize = Settings.Raycast.Visuals.GridSize,
            RayLineThickness = Settings.Raycast.Visuals.RayLineThickness,
            VisibleColor = Settings.Raycast.Visuals.Colors.Visible,
            ShadowColor = Settings.Raycast.Visuals.Colors.Shadow,
            RayLineColor = Settings.Raycast.Visuals.Colors.RayLine,
            DrawAtPlayerPlane = Settings.Raycast.Visuals.DrawAtPlayerPlane
        };
        _rayCaster.InitializeConfig(raycastConfig);

        Input.RegisterKey(Settings.AimKey);
        Input.RegisterKey(Settings.AimToggleKey);
        Settings.AimKey.OnValueChanged += () => { Input.RegisterKey(Settings.AimKey); };
        Settings.AimToggleKey.OnValueChanged += () =>
        {
            Input.RegisterKey(Settings.AimToggleKey);
            _isAimToggled = false;
        };
        return true;
    }

    public override void AreaChange(AreaInstance area)
    {
        _rayCaster.UpdateArea(GameController);
        _entityScanner.ClearEntities();
        _currentTarget = null;
        _isAimToggled = false;
    }

    public override void Tick()
    {
        if (Settings.AimToggleKey.PressedOnce()) _isAimToggled = !_isAimToggled;

        if (!ShouldProcess()) return;
        if (!_isAimToggled && !Input.GetKeyState(Settings.AimKey.Value)) return;

        var player = GameController?.Player;
        if (player == null) return;

        ProcessAiming(player);
    }

    private void ProcessAiming(Entity player)
    {
        var currentPos = player.GridPos;

        var potentialTargets = _entityScanner.ScanForInRangeEntities(currentPos, GameController);

        _rayCaster.UpdateObserver(currentPos, potentialTargets);

        _entityScanner.ProcessVisibleEntities(currentPos);
        _entityScanner.UpdateEntityWeights(currentPos);

        var sortedEntities = _entityScanner.GetTrackedEntities().OrderByDescending(x => x.Weight).ToList();
        if (!sortedEntities.Any()) return;

        var (targetEntity, rawPosToAim) = GetTargetEntityAndPosition(sortedEntities);
        if (targetEntity == null) return;

        _currentTarget = targetEntity;
        UpdateCursorPosition(rawPosToAim);
    }

    private (TrackedEntity entity, Vector2 position) GetTargetEntityAndPosition(List<TrackedEntity> sortedEntities)
    {
        if (!Settings.Targeting.PointToOffscreenTargetsOtherwiseFindNextTargetInBounds)
        {
            foreach (var entity in sortedEntities)
            {
                var pos = GameController.IngameState.Camera.WorldToScreen(entity.Entity.Pos);
                if (pos != Vector2.Zero &&
                    _inputHandler.IsValidClickPosition(pos, GameController.Window.GetWindowRectangle()))
                    return (entity, pos);
            }

            return (null, Vector2.Zero);
        }

        var targetEntity = sortedEntities.First();
        var rawPosToAim = GameController.IngameState.Camera.WorldToScreen(targetEntity.Entity.Pos);
        return rawPosToAim == Vector2.Zero ? (null, Vector2.Zero) : (targetEntity, rawPosToAim);
    }

    private void UpdateCursorPosition(Vector2 rawPosToAim)
    {
        if (_currentTarget == null) return;

        var window = GameController.Window.GetWindowRectangle();
        var safePosToAim = _inputHandler.GetSafeAimPosition(rawPosToAim, window);

        if (Settings.Render.Cursor.ConfineCursorToCircle)
        {
            var screenCenter = new Vector2(window.Width / 2, window.Height / 2);
            var vectorToTarget = safePosToAim - screenCenter;
            var distanceToTarget = vectorToTarget.Length();

            if (distanceToTarget > Settings.Render.Cursor.CursorCircleRadius)
            {
                vectorToTarget = Vector2.Normalize(vectorToTarget) * Settings.Render.Cursor.CursorCircleRadius;
                safePosToAim = screenCenter + vectorToTarget;
            }
        }

        if (_inputHandler.IsValidClickPosition(safePosToAim, window))
        {
            var randomizedPos = _inputHandler.GetRandomizedAimPosition(safePosToAim, window);
            if (_inputHandler.IsValidClickPosition(randomizedPos, window)) Input.SetCursorPos(randomizedPos);
        }
    }

    public override void Render()
    {
        _renderer.Render(GameController, _currentTarget, _entityScanner.GetTrackedEntities());
    }

    private bool ShouldProcess()
    {
        if (!Settings.Enable) return false;
        if (GameController is not { InGame: true, Player: not null }) return false;
        return !GameController.Settings.CoreSettings.Enable &&
               AreUiElementsVisible(GameController?.IngameState.IngameUi);
    }

    private bool AreUiElementsVisible(IngameUIElements ingameUi)
    {
        if (ingameUi == null) return false;
        if (!Settings.Render.Panels.RenderAndWorkOnFullPanels &&
            ingameUi.FullscreenPanels.Any(x => x.IsVisible)) return false;
        if (!Settings.Render.Panels.RenderAndWorkOnleftPanels && ingameUi.OpenLeftPanel.IsVisible) return false;
        return Settings.Render.Panels.RenderAndWorkOnRightPanels || !ingameUi.OpenRightPanel.IsVisible;
    }
}
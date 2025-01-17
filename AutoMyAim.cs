using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AutoMyAim.Structs;
using ExileCore2;
using ExileCore2.PoEMemory.MemoryObjects;
using ExileCore2.Shared;

namespace AutoMyAim;

public class AutoMyAim : BaseSettingsPlugin<AutoMyAimSettings>
{
    public static AutoMyAim Main;
    private readonly ClusterManager _clusterManager;
    private readonly EntityScanner _entityScanner;
    private readonly InputHandler _inputHandler;
    internal readonly RayCaster RayCaster;
    private readonly AimRenderer _renderer;
    private readonly TargetWeightCalculator _weightCalculator;
    private TrackedEntity _currentTarget;
    private bool _isAimToggled;
    private Func<bool> _pickitIsActive;
    public Vector2 TopLeftScreen;
    public RectangleF GetWindowRectangleNormalized;

    public AutoMyAim()
    {
        Name = "Auto My Aim";
        RayCaster = new RayCaster();
        _clusterManager = new ClusterManager();
        _weightCalculator = new TargetWeightCalculator();
        _entityScanner = new EntityScanner(_weightCalculator, _clusterManager);
        _inputHandler = new InputHandler();
        _renderer = new AimRenderer(_clusterManager);
    }

    public override bool Initialise()
    {
        Main = this;

        // Register input handlers
        Input.RegisterKey(Settings.AimKey);
        Input.RegisterKey(Settings.AimToggleKey);
        Settings.AimKey.OnValueChanged += () => { Input.RegisterKey(Settings.AimKey); };
        Settings.AimToggleKey.OnValueChanged += () =>
        {
            Input.RegisterKey(Settings.AimToggleKey);
            _isAimToggled = false;
        };

        // Register terrain update handler
        Settings.UseWalkableTerrainInsteadOfTargetTerrain.OnValueChanged += (_, _) => { RayCaster.UpdateArea(); };

        return true;
    }

    public override void AreaChange(AreaInstance area)
    {
        RayCaster.UpdateArea();
        _entityScanner.ClearEntities();
        _clusterManager.ClearRenderState();
        _currentTarget = null;
        _isAimToggled = false;
        _pickitIsActive = GameController.PluginBridge.GetMethod<Func<bool>>("PickIt.IsActive");
    }

    public override void Tick()
    {
        if (Settings.AimToggleKey.PressedOnce()) _isAimToggled = !_isAimToggled;
        TopLeftScreen = GameController.Window.GetWindowRectangleTimeCache.TopLeft;

        var windowRect = GameController.Window.GetWindowRectangleReal();
        GetWindowRectangleNormalized = new RectangleF(
            windowRect.X - TopLeftScreen.X,
            windowRect.Y - TopLeftScreen.Y,
            windowRect.Width,
            windowRect.Height);

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

        RayCaster.UpdateObserver(currentPos, potentialTargets);

        _entityScanner.ProcessVisibleEntities(currentPos);
        _entityScanner.UpdateEntityWeights(currentPos);

        var sortedEntities = _entityScanner.GetTrackedEntities();
        if (!sortedEntities.Any()) return;

        var (targetEntity, rawPosToAim) = GetTargetEntityAndPosition(sortedEntities);
        if (targetEntity == null) return;

        _currentTarget = targetEntity;
        if (Settings.UsePluginBridgeWarnings)
        {
            if (_pickitIsActive?.Invoke() ?? false)
                return;
        }
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
                    _inputHandler.IsValidClickPosition(pos, GetWindowRectangleNormalized))
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
        var safePosToAim = _inputHandler.GetSafeAimPosition(rawPosToAim, GetWindowRectangleNormalized);

        if (Settings.Render.Cursor.ConfineCursorToCircle)
        {
            var playerScreenPos = GameController.IngameState.Camera.WorldToScreen(GameController.Player.Pos);
            var circleRadius = Settings.Render.Cursor.CursorCircleRadius;

            var toTarget = safePosToAim - playerScreenPos;
            var distance = toTarget.Length();

            if (distance > circleRadius) safePosToAim = playerScreenPos + Vector2.Normalize(toTarget) * circleRadius;
        }

        if (_inputHandler.IsValidClickPosition(safePosToAim, GetWindowRectangleNormalized))
        {
            var randomizedPos = _inputHandler.GetRandomizedAimPosition(safePosToAim, GetWindowRectangleNormalized);
            if (_inputHandler.IsValidClickPosition(randomizedPos, GetWindowRectangleNormalized))
                Input.SetCursorPos(randomizedPos + TopLeftScreen);
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
            ingameUi.FullscreenPanels.Any(x => x.IsVisible))
            return false;
        if (!Settings.Render.Panels.RenderAndWorkOnleftPanels && ingameUi.OpenLeftPanel.IsVisible)
            return false;
        return Settings.Render.Panels.RenderAndWorkOnRightPanels || !ingameUi.OpenRightPanel.IsVisible;
    }
}
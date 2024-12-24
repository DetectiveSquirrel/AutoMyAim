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
    private Vector2 _lastPlayerPos;

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
            ShowRayLines = Settings.ShowRayLines,
            ShowTerrainValues = Settings.ShowTerrainValues,
            TargetLayerValue = Settings.TargetLayerValue,
            GridSize = Settings.GridSize,
            RayLength = Settings.RayLength,
            RayCount = Settings.RayCount,
            RayLineThickness = Settings.RayLineThickness,
            VisibleColor = Settings.VisibleColor,
            ShadowColor = Settings.ShadowColor,
            RayLineColor = Settings.RayLineColor,
            DrawAtPlayerPlane = Settings.DrawAtPlayerPlane
        };
        _rayCaster.InitializeConfig(raycastConfig);

        Input.RegisterKey(Settings.AimKey);
        Settings.AimKey.OnValueChanged += () => { Input.RegisterKey(Settings.AimKey); };
        return true;
    }

    public override void AreaChange(AreaInstance area)
    {
        _rayCaster.UpdateArea(GameController);
        _entityScanner.ClearEntities();
        _currentTarget = null;
    }

    public override void Tick()
    {
        if (!ShouldProcess() || !Input.GetKeyState(Settings.AimKey.Value)) return;

        var player = GameController?.Player;
        if (player == null) return;

        var currentPos = player.GridPos;
        _lastPlayerPos = currentPos;
        _rayCaster.UpdateObserver(currentPos);

        _entityScanner.ScanForEntities(currentPos, GameController);
        _entityScanner.UpdateEntityWeights(currentPos);

        var sortedEntities = _entityScanner.GetTrackedEntities().OrderByDescending(x => x.Weight).ToList();
        if (!sortedEntities.Any()) return;

        TrackedEntity targetEntity = null;
        var rawPosToAim = Vector2.Zero;

        if (!Settings.PointToOffscreenTargetsOtherwiseFindNextTargetInBounds)
        {
            foreach (var entity in sortedEntities)
            {
                var pos = GameController.IngameState.Camera.WorldToScreen(entity.Entity.Pos);
                if (pos != Vector2.Zero &&
                    _inputHandler.IsValidClickPosition(pos, GameController.Window.GetWindowRectangle()))
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

        if (_currentTarget != null)
        {
            var window = GameController.Window.GetWindowRectangle();
            var safePosToAim = _inputHandler.GetSafeAimPosition(rawPosToAim, window);

            if (Settings.ConfineCursorToCircle)
            {
                var screenCenter = new Vector2(
                    window.Width / 2,
                    window.Height / 2
                );

                var vectorToTarget = safePosToAim - screenCenter;
                var distanceToTarget = vectorToTarget.Length();

                if (distanceToTarget > Settings.CursorCircleRadius)
                {
                    vectorToTarget = Vector2.Normalize(vectorToTarget) * Settings.CursorCircleRadius;
                    safePosToAim = screenCenter + vectorToTarget;
                }
            }

            if (_inputHandler.IsValidClickPosition(safePosToAim, window))
            {
                var randomizedPos = _inputHandler.GetRandomizedAimPosition(safePosToAim, window);
                if (_inputHandler.IsValidClickPosition(randomizedPos, window)) Input.SetCursorPos(randomizedPos);
            }
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
        if (!Settings.RenderOnFullPanels && ingameUi.FullscreenPanels.Any(x => x.IsVisible)) return false;
        if (!Settings.RenderOnleftPanels && ingameUi.OpenLeftPanel.IsVisible) return false;
        return Settings.RenderOnRightPanels || !ingameUi.OpenRightPanel.IsVisible;
    }
}
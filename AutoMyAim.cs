using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Windows.Forms;
using AutoMyAim.Structs;
using ExileCore2;
using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory.Elements;
using ExileCore2.PoEMemory.MemoryObjects;
using Shortcut = GameOffsets2.Shortcut;

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
    public bool _isAimToggled;
    public Vector2 TopLeftScreen;
    public ExileCore2.Shared.RectangleF GetWindowRectangleNormalized;
    private List<Shortcut> skills_shortcuts;
    private List<SkillElement> skillbar;

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
    }

    public override void Tick()
    {
        if (GameController.IsLoading) return;
        var validPl = GameController.Player.TryGetComponent<Player>(out Player pl);

        if (validPl && !Settings.ShortCutBools.ContainsKey(pl.PlayerName) || Settings.ShortCutBools[pl.PlayerName] == null)
        {
            Settings.ShortCutBools.Add(pl.PlayerName, new bool[13]);

        }
        if (Settings.AimToggleKey.PressedOnce()) _isAimToggled = !_isAimToggled;
        TopLeftScreen = GameController.Window.GetWindowRectangleTimeCache.TopLeft;

        var windowRect = GameController.Window.GetWindowRectangleReal();
        GetWindowRectangleNormalized = new ExileCore2.Shared.RectangleF(
            windowRect.X - TopLeftScreen.X,
            windowRect.Y - TopLeftScreen.Y,
            windowRect.Width,
            windowRect.Height);

         skills_shortcuts = GameController.IngameState?.ShortcutSettings?.Shortcuts?.Skip(5).Take(13).ToList();
         skillbar = GameController.IngameState?.IngameUi?.SkillBar?.Skills.ToList();


        if (!Settings.UseAimKey && !_isAimToggled && Settings.ToggleSkillHotKey.PressedOnce())
        {
            if (skills_shortcuts != null)
            {
                if (skillbar.Any(skEl => skEl.GetClientRect().Contains(Input.MousePosition) && skEl.Skill != null && skEl.Skill.Id > 0))
                {
                    var element = skillbar.FirstOrDefault(sc => sc.IsVisibleLocal && sc.GetClientRect().Contains(Input.MousePosition) && sc.Skill != null && sc.Skill.Id > 0);
                    if (element != null)
                    {
                        var i = skillbar.IndexOf(element);
                        Settings.ShortCutBools[pl.PlayerName][i] = !Settings.ShortCutBools[pl.PlayerName][i];
                    }
                }
            }
        }

        if (!ShouldProcess()) return;
        if (_isAimToggled) return;
        if (!Settings.UseAimKey && !skillbar.Any(skEl => Settings.ShortCutBools[pl.PlayerName][skillbar.IndexOf(skEl)] && skEl.Skill != null && skEl.Skill.Id > 0 && skills_shortcuts[skillbar.IndexOf(skEl)].IsShortCutPressed()))
            return;   
        if (Settings.UseAimKey && !Input.GetKeyState(Settings.AimKey.Value)) return;

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
        if (!GameController.InGame || !GameController.IngameState.InGame) return;

        _renderer.Render(GameController, _currentTarget, _entityScanner.GetTrackedEntities());
        if (!Settings.UseAimKey && !_isAimToggled && !GameController.IsLoading)
        {
            var skillbar = GameController.IngameState.IngameUi.SkillBar.Skills.ToList();
            for (int i = 0; i < skillbar.Count; i++)
            {
                var element = skillbar[i];
                var playername = GameController.Player.GetComponent<Player>().PlayerName;
                if (Settings.ShortCutBools[playername][i] && element.IsVisibleLocal && element.Skill != null && element.Skill.Id > 0)
                {
                    var pos = element.GetClientRect();
                    Graphics.DrawFrame(pos, Color.Green, 4);
                }
            }
        }
        var pastelColor = !_isAimToggled && !MenuWindow.IsOpened ? Color.FromArgb(125, 0, 255, 0) : Color.FromArgb(125, 255, 0, 0);

        var brNormalized = Main.GetWindowRectangleNormalized.BottomRight;
        Graphics.DrawCircleFilled(new Vector2(brNormalized.X - 15, brNormalized.Y - 15), 10, pastelColor, 10);
    }

    private bool ShouldProcess()
    {
        if (!Settings.Enable && _isAimToggled) return false;
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

public static class ShortcutExtensions
{
    public static bool IsShortCutPressed(this Shortcut shortcut)
    {
        return shortcut.MainKey != ConsoleKey.None &&
               (shortcut.Modifier != GameOffsets2.ShortcutModifier.None ?
               Input.IsKeyDown((Keys)shortcut.MainKey) && Input.IsKeyDown((Keys)shortcut.Modifier) :
               Input.IsKeyDown((Keys)shortcut.MainKey));
    }
}

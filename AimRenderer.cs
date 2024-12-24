using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AutoMyAim.Structs;
using ExileCore2;
using ExileCore2.PoEMemory.MemoryObjects;
using ExileCore2.Shared;
using ExileCore2.Shared.Helpers;
using ImGuiNET;

namespace AutoMyAim;

public class AimRenderer
{
    private readonly InputHandler _inputHandler = new();
    private ImDrawListPtr _drawList;

    public void Render(GameController gameController, TrackedEntity currentTarget, List<TrackedEntity> trackedEntities)
    {
        if (!ShouldDraw(gameController)) return;

        var rect = gameController.Window.GetWindowRectangle() with { Location = Vector2.Zero };
        SetupImGuiWindow(rect);

        var raycastConfig = new RaycastRenderConfig
        {
            ShowRayLines = AutoMyAim.Main.Settings.ShowRayLines,
            ShowTerrainValues = AutoMyAim.Main.Settings.ShowTerrainValues,
            TargetLayerValue = AutoMyAim.Main.Settings.TargetLayerValue,
            GridSize = AutoMyAim.Main.Settings.GridSize,
            RayCount = AutoMyAim.Main.Settings.RayCount,
            RayLineThickness = AutoMyAim.Main.Settings.RayLineThickness,
            VisibleColor = AutoMyAim.Main.Settings.VisibleColor,
            ShadowColor = AutoMyAim.Main.Settings.ShadowColor,
            RayLineColor = AutoMyAim.Main.Settings.RayLineColor,
            DrawAtPlayerPlane = AutoMyAim.Main.Settings.DrawAtPlayerPlane
        };

        AutoMyAim.Main._rayCaster.Render(_drawList, gameController, raycastConfig);
        RenderEntityWeights(gameController, trackedEntities);
        RenderAimVisualization(gameController, currentTarget);
        ImGui.End();
    }

    private bool ShouldDraw(GameController gameController)
    {
        return AutoMyAim.Main.Settings.EnableDrawing && AreUiElementsVisible(gameController?.IngameState.IngameUi);
    }

    private bool AreUiElementsVisible(IngameUIElements ingameUi)
    {
        if (ingameUi == null) return false;
        if (!AutoMyAim.Main.Settings.RenderOnFullPanels && ingameUi.FullscreenPanels.Any(x => x.IsVisible))
            return false;
        if (!AutoMyAim.Main.Settings.RenderOnleftPanels && ingameUi.OpenLeftPanel.IsVisible) return false;
        return AutoMyAim.Main.Settings.RenderOnRightPanels || !ingameUi.OpenRightPanel.IsVisible;
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

    private void RenderEntityWeights(GameController gameController, List<TrackedEntity> trackedEntities)
    {
        if (!AutoMyAim.Main.Settings.EnableWeighting || !AutoMyAim.Main.Settings.ShowWeights) return;

        foreach (var trackedEntity in trackedEntities)
        {
            var screenPos = gameController.IngameState.Camera.WorldToScreen(trackedEntity.Entity.Pos);
            if (screenPos != Vector2.Zero)
            {
                var text = $"({trackedEntity.Weight:F1})";
                _drawList.AddText(screenPos, AutoMyAim.Main.Settings.WeightTextColor.Value.ToImgui(), text);
            }
        }
    }

    private void RenderAimVisualization(GameController gameController, TrackedEntity currentTarget)
    {
        if (AutoMyAim.Main.Settings.ConfineCursorToCircle)
        {
            var screenCenter = new Vector2(
                gameController.Window.GetWindowRectangle().Width / 2,
                gameController.Window.GetWindowRectangle().Height / 2
            );
            _drawList.AddCircle(screenCenter, AutoMyAim.Main.Settings.CursorCircleRadius,
                ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 0, 0.5f)), 64, 1f);
        }

        if (currentTarget == null) return;

        var rawPosToAim = gameController.IngameState.Camera.WorldToScreen(currentTarget.Entity.Pos);
        if (rawPosToAim == Vector2.Zero) return;

        var window = gameController.Window.GetWindowRectangle();
        var safePosToAim = _inputHandler.GetSafeAimPosition(rawPosToAim, window);
        if (!_inputHandler.IsValidClickPosition(safePosToAim, window)) return;

        _drawList.AddCircle(safePosToAim, AutoMyAim.Main.Settings.AcceptableRadius,
            AutoMyAim.Main.Settings.WeightTextColor.Value.ToImgui(), 32, 1f);

        if (AutoMyAim.Main.Settings.ShowDebug)
        {
            var safeZone = new RectangleF(
                window.X + AutoMyAim.Main.Settings.LeftPadding.Value,
                window.Y + AutoMyAim.Main.Settings.TopPadding.Value,
                window.Width - (AutoMyAim.Main.Settings.LeftPadding.Value + AutoMyAim.Main.Settings.RightPadding.Value),
                window.Height - (AutoMyAim.Main.Settings.TopPadding.Value + AutoMyAim.Main.Settings.BottomPadding.Value)
            );

            _drawList.AddRect(
                new Vector2(safeZone.Left, safeZone.Top),
                new Vector2(safeZone.Right, safeZone.Bottom),
                ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 0, 0.5f))
            );
        }
    }
}
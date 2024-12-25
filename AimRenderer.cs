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
            ShowRayLines = AutoMyAim.Main.Settings.Raycast.Visuals.ShowRayLines,
            ShowTerrainValues = AutoMyAim.Main.Settings.Raycast.Visuals.ShowTerrainValues,
            TargetLayerValue = AutoMyAim.Main.Settings.Raycast.TargetLayerValue,
            GridSize = AutoMyAim.Main.Settings.Raycast.Visuals.GridSize,
            RayLineThickness = AutoMyAim.Main.Settings.Raycast.Visuals.RayLineThickness,
            VisibleColor = AutoMyAim.Main.Settings.Raycast.Visuals.Colors.Visible,
            ShadowColor = AutoMyAim.Main.Settings.Raycast.Visuals.Colors.Shadow,
            RayLineColor = AutoMyAim.Main.Settings.Raycast.Visuals.Colors.RayLine,
            DrawAtPlayerPlane = AutoMyAim.Main.Settings.Raycast.Visuals.DrawAtPlayerPlane
        };

        AutoMyAim.Main._rayCaster.Render(_drawList, gameController, raycastConfig);
        RenderEntityWeights(gameController, trackedEntities);
        RenderAimVisualization(gameController, currentTarget);
        ImGui.End();
    }

    private bool ShouldDraw(GameController gameController)
    {
        return AutoMyAim.Main.Settings.Render.EnableDrawing &&
               AreUiElementsVisible(gameController?.IngameState.IngameUi);
    }

    private bool AreUiElementsVisible(IngameUIElements ingameUi)
    {
        if (ingameUi == null) return false;
        if (!AutoMyAim.Main.Settings.Render.Panels.RenderAndWorkOnFullPanels &&
            ingameUi.FullscreenPanels.Any(x => x.IsVisible))
            return false;
        if (!AutoMyAim.Main.Settings.Render.Panels.RenderAndWorkOnleftPanels && ingameUi.OpenLeftPanel.IsVisible) return false;
        return AutoMyAim.Main.Settings.Render.Panels.RenderAndWorkOnRightPanels || !ingameUi.OpenRightPanel.IsVisible;
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
        if (!AutoMyAim.Main.Settings.Targeting.Weights.EnableWeighting ||
            !AutoMyAim.Main.Settings.Render.WeightVisuals.ShowWeights) return;

        foreach (var trackedEntity in trackedEntities)
        {
            var screenPos = gameController.IngameState.Camera.WorldToScreen(trackedEntity.Entity.Pos);
            if (screenPos != Vector2.Zero)
            {
                var text = $"({trackedEntity.Weight:F1})";
                _drawList.AddText(screenPos,
                    AutoMyAim.Main.Settings.Render.WeightVisuals.WeightTextColor.Value.ToImgui(), text);
            }
        }
    }

    private void RenderAimVisualization(GameController gameController, TrackedEntity currentTarget)
    {
        if (AutoMyAim.Main.Settings.Render.Cursor.ConfineCursorToCircle)
        {
            var screenCenter = new Vector2(
                gameController.Window.GetWindowRectangle().Width / 2,
                gameController.Window.GetWindowRectangle().Height / 2
            );
            _drawList.AddCircle(screenCenter, AutoMyAim.Main.Settings.Render.Cursor.CursorCircleRadius,
                ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 0, 0.5f)), 64, 1f);
        }

        if (currentTarget == null) return;

        var rawPosToAim = gameController.IngameState.Camera.WorldToScreen(currentTarget.Entity.Pos);
        if (rawPosToAim == Vector2.Zero) return;

        var window = gameController.Window.GetWindowRectangle();
        var safePosToAim = _inputHandler.GetSafeAimPosition(rawPosToAim, window);
        if (!_inputHandler.IsValidClickPosition(safePosToAim, window)) return;

        _drawList.AddCircle(safePosToAim, AutoMyAim.Main.Settings.Render.Cursor.AcceptableRadius,
            AutoMyAim.Main.Settings.Render.WeightVisuals.WeightTextColor.Value.ToImgui(), 32, 1f);

        if (AutoMyAim.Main.Settings.Render.ShowDebug)
        {
            var safeZone = new RectangleF(
                window.X + AutoMyAim.Main.Settings.Render.Panels.Padding.Left.Value,
                window.Y + AutoMyAim.Main.Settings.Render.Panels.Padding.Top.Value,
                window.Width - (AutoMyAim.Main.Settings.Render.Panels.Padding.Left.Value +
                                AutoMyAim.Main.Settings.Render.Panels.Padding.Right.Value),
                window.Height - (AutoMyAim.Main.Settings.Render.Panels.Padding.Top.Value +
                                 AutoMyAim.Main.Settings.Render.Panels.Padding.Bottom.Value)
            );

            _drawList.AddRect(
                new Vector2(safeZone.Left, safeZone.Top),
                new Vector2(safeZone.Right, safeZone.Bottom),
                ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 0, 0.5f))
            );
        }
    }
}
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using AutoMyAim.Structs;
using ExileCore2;
using ExileCore2.PoEMemory.MemoryObjects;

namespace AutoMyAim;

public class AimRenderer(ClusterManager clusterManager)
{
    private readonly InputHandler _inputHandler = new();

    public void Render(GameController gameController, TrackedEntity currentTarget, List<TrackedEntity> trackedEntities)
    {
        if (!ShouldDraw(gameController)) return;

        AutoMyAim.Main.RayCaster.Render(gameController);
        RenderEntityWeights(gameController, trackedEntities);
        RenderClusters(gameController);
        RenderAimVisualization(gameController, currentTarget);
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
        if (!AutoMyAim.Main.Settings.Render.Panels.RenderAndWorkOnleftPanels &&
            ingameUi.OpenLeftPanel.IsVisible) return false;
        return AutoMyAim.Main.Settings.Render.Panels.RenderAndWorkOnRightPanels || !ingameUi.OpenRightPanel.IsVisible;
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
                AutoMyAim.Main.Graphics.DrawText(text, screenPos,
                    AutoMyAim.Main.Settings.Render.WeightVisuals.WeightTextColor.Value);
            }
        }
    }

    private void RenderClusters(GameController gameController)
    {
        if (!AutoMyAim.Main.Settings.Render.ShowDebug.Value ||
            !AutoMyAim.Main.Settings.Targeting.Weights.Cluster.EnableClustering)
            return;

        clusterManager.Render(AutoMyAim.Main.Graphics, gameController);
    }

    private void RenderAimVisualization(GameController gameController, TrackedEntity currentTarget)
    {
        var windowRect = AutoMyAim.Main.GetWindowRectangleNormalized;

        if (AutoMyAim.Main.Settings.Render.ShowDebug)
        {
            // Draw cursor confinement circle if enabled
            if (AutoMyAim.Main.Settings.Render.Cursor.ConfineCursorToCircle)
                AutoMyAim.Main.Graphics.DrawCircle(
                    AutoMyAim.Main.GetWindowRectangleNormalized.Center,
                    AutoMyAim.Main.Settings.Render.Cursor.CursorCircleRadius,
                    Color.FromArgb(125, 255, 255, 0),
                    1,
                    50
                );

            // Draw inner safe zone with padding
            var padding = AutoMyAim.Main.Settings.Render.Panels.PaddingPercentToCenter;
            AutoMyAim.Main.Graphics.DrawFrame(
                new Vector2(
                    windowRect.Width * (padding.Left.Value / 100f),
                    windowRect.Height * (padding.Top.Value / 100f)
                ),
                new Vector2(
                    windowRect.Width * (1 - padding.Right.Value / 100f),
                    windowRect.Height * (1 - padding.Bottom.Value / 100f)
                ),
                Color.FromArgb(125, 0, 255, 255),
                1
            );
        }

        if (currentTarget == null) return;

        var rawPosToAim = gameController.IngameState.Camera.WorldToScreen(currentTarget.Entity.Pos);
        if (rawPosToAim == Vector2.Zero) return;

        var safePosToAim = _inputHandler.GetSafeAimPosition(rawPosToAim, windowRect);
        if (!_inputHandler.IsValidClickPosition(safePosToAim, windowRect)) return;

        // Draw cursor target circle directly at screen coordinates
        AutoMyAim.Main.Graphics.DrawCircle(
            safePosToAim,
            AutoMyAim.Main.Settings.Render.Cursor.AcceptableRadius,
            AutoMyAim.Main.Settings.Render.WeightVisuals.WeightTextColor.Value,
            1,
            25
        );
    }
}
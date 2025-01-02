using System.Drawing;
using System.Windows.Forms;
using ExileCore2.Shared.Attributes;
using ExileCore2.Shared.Interfaces;
using ExileCore2.Shared.Nodes;

namespace AutoMyAim.Structs;

public class AutoMyAimSettings : ISettings
{
    public ToggleNode Enable { get; set; } = new(false);
    public ToggleNode UseWalkableTerrainInsteadOfTargetTerrain { get; set; } = new(false);
    public HotkeyNode AimKey { get; set; } = new(Keys.None);
    public HotkeyNode AimToggleKey { get; set; } = new(Keys.None);
    public RenderSettings Render { get; set; } = new();
    public TargetingSettings Targeting { get; set; } = new();
    public RaycastSettings Raycast { get; set; } = new();
}

[Submenu(CollapsedByDefault = true)]
public class RenderSettings
{
    public ToggleNode EnableDrawing { get; set; } = new(true);
    public ToggleNode ShowDebug { get; set; } = new(true);

    public WeightVisualsSettings WeightVisuals { get; set; } = new();
    public ClusterVisualsSettings ClusterVisuals { get; set; } = new();
    public UIPanelSettings Panels { get; set; } = new();
    public CursorSettings Cursor { get; set; } = new();

    [Submenu(CollapsedByDefault = false)]
    public class WeightVisualsSettings
    {
        public ToggleNode ShowWeights { get; set; } = new(true);
        public ColorNode WeightTextColor { get; set; } = new(Color.FromArgb(255, 255, 255, 255));
    }

    [Submenu(CollapsedByDefault = false)]
    public class ClusterVisualsSettings
    {
        public ToggleNode ShowClusters { get; set; } = new(true);
        public ColorNode ClusterFillColor { get; set; } = new(Color.FromArgb(128, 255, 255, 0)); // Semi-transparent yellow
        public ColorNode ClusterBorderColor { get; set; } = new(Color.FromArgb(192, 255, 200, 0)); // More opaque yellow-orange
        public RangeNode<float> BorderThickness { get; set; } = new(2.0f, 0f, 5.0f);
        public ToggleNode ShowClusterInfo { get; set; } = new(true);
        public ColorNode ClusterInfoTextColor { get; set; } = new(Color.FromArgb(255, 255, 220, 130));
    }

    [Submenu(CollapsedByDefault = true)]
    public class UIPanelSettings
    {
        public ToggleNode RenderAndWorkOnFullPanels { get; set; } = new(false);
        public ToggleNode RenderAndWorkOnleftPanels { get; set; } = new(false);
        public ToggleNode RenderAndWorkOnRightPanels { get; set; } = new(false);

        public PaddingSettings PaddingPercentToCenter { get; set; } = new();

        [Submenu(CollapsedByDefault = true)]
        public class PaddingSettings
        {
            public RangeNode<float> Left { get; set; } = new(2, 0, 50);
            public RangeNode<float> Right { get; set; } = new(3, 0, 50);
            public RangeNode<float> Top { get; set; } = new(8, 0, 50);
            public RangeNode<float> Bottom { get; set; } = new(15, 0, 50);
        }
    }

    [Submenu(CollapsedByDefault = true)]
    public class CursorSettings
    {
        public RangeNode<int> AcceptableRadius { get; set; } = new(9, 1, 200);
        public ToggleNode RandomizeInRadius { get; set; } = new(false);
        public ToggleNode ConfineCursorToCircle { get; set; } = new(false);
        public RangeNode<int> CursorCircleRadius { get; set; } = new(300, 50, 1000);
    }
}

[Submenu(CollapsedByDefault = true)]
public class TargetingSettings
{
    public RangeNode<int> EntityScanDistance { get; set; } = new(100, 1, 500);
    public RangeNode<float> MaxTargetDistance { get; set; } = new(100f, 0f, 200f);
    public ToggleNode PointToOffscreenTargetsOtherwiseFindNextTargetInBounds { get; set; } = new(false);

    public WeightSettings Weights { get; set; } = new();

    [Submenu(CollapsedByDefault = true)]
    public class WeightSettings
    {
        public ToggleNode EnableWeighting { get; set; } = new(true);
        public RangeNode<float> DistanceWeight { get; set; } = new(2.0f, 0f, 5f);

        public HPSettings HP { get; set; } = new();
        public RaritySettings Rarity { get; set; } = new();
        public ClusterSettings Cluster { get; set; } = new();
        public SmoothingSettings Smoothing { get; set; } = new();

        [Submenu(CollapsedByDefault = false)]
        public class HPSettings
        {
            public ToggleNode EnableHPWeighting { get; set; } = new(true);
            public ToggleNode PreferHigherHP { get; set; } = new(false);
            public RangeNode<float> Weight { get; set; } = new(1.0f, 0f, 5f);
        }

        [Submenu(CollapsedByDefault = false)]
        public class RaritySettings
        {
            public ToggleNode EnableRarityWeighting { get; set; } = new(true);
            public RangeNode<float> Normal { get; set; } = new(1.0f, 0f, 10f);
            public RangeNode<float> Magic { get; set; } = new(2.0f, 0f, 10f);
            public RangeNode<float> Rare { get; set; } = new(3.0f, 0f, 10f);
            public RangeNode<float> Unique { get; set; } = new(4.0f, 0f, 10f);
        }

        [Submenu(CollapsedByDefault = false)]
        public class ClusterSettings
        {
            public ToggleNode EnableClustering { get; set; } = new(true);
            public RangeNode<float> ClusterRadius { get; set; } = new(25f, 10f, 100f);
            public RangeNode<int> MinClusterSize { get; set; } = new(3, 2, 10);
            public RangeNode<float> BaseClusterBonus { get; set; } = new(0.1f, 0f, 1f);
            public RangeNode<float> MaxClusterBonus { get; set; } = new(2.0f, 1f, 5f);

            public ToggleNode EnableCoreBonus { get; set; } = new(true);
            public RangeNode<float> CoreBonusMultiplier { get; set; } = new(1.2f, 1f, 2f);
            public RangeNode<float> CoreRadiusPercent { get; set; } = new(0.5f, 0.1f, 1f);

            public ToggleNode EnableIsolationPenalty { get; set; } = new(true);
            public RangeNode<float> IsolationPenaltyMultiplier { get; set; } = new(0.8f, 0.1f, 1f);
        }

        [Submenu(CollapsedByDefault = false)]
        public class SmoothingSettings
        {
            public ToggleNode EnableSmoothing { get; set; } = new(true);
            public RangeNode<float> SmoothingFactor { get; set; } = new(0.3f, 0.1f, 1f);
        }
    }
}
[Submenu(CollapsedByDefault = true)]
public class RaycastSettings
{
    public RangeNode<int> TargetLayerValue { get; set; } = new(2, 0, 5);
    public VisualsSettings Visuals { get; set; } = new();

    [Submenu(CollapsedByDefault = false)]
    public class VisualsSettings
    {
        public RangeNode<int> GridSize { get; set; } = new(80, 1, 1000);
        public ToggleNode DrawAtPlayerPlane { get; set; } = new(true);
        public ToggleNode ShowRayLines { get; set; } = new(true);
        public ToggleNode ShowTerrainValues { get; set; } = new(true);
        public RangeNode<float> RayLineThickness { get; set; } = new(2.0f, 1.0f, 5.0f);

        public EntityVisibilityColors EntityColors { get; set; } = new();
        public TerrainColor TerrainColors { get; set; } = new();

        [Submenu(CollapsedByDefault = false)]
        public class EntityVisibilityColors
        {
            public ColorNode Visible { get; set; } = new(Color.FromArgb(200, 35, 245, 0));
            public ColorNode Shadow { get; set; } = new(Color.FromArgb(200, 255, 0, 0));
            public ColorNode RayLine { get; set; } = new(Color.FromArgb(200, 35, 245, 0));
        }

        [Submenu(CollapsedByDefault = false)]
        public class TerrainColor
        {
            public ToggleNode EnableTerrainColorization { get; set; } = new(true);
            public ColorNode Tile5 { get; set; } = new(Color.FromArgb(150, 50, 200, 50));
            public ColorNode Tile4 { get; set; } = new(Color.FromArgb(150, 100, 180, 50));
            public ColorNode Tile3 { get; set; } = new(Color.FromArgb(150, 150, 160, 50));
            public ColorNode Tile2 { get; set; } = new(Color.FromArgb(150, 180, 140, 50));
            public ColorNode Tile1 { get; set; } = new(Color.FromArgb(150, 200, 120, 50));
            public ColorNode Tile0 { get; set; } = new(Color.FromArgb(150, 220, 100, 50));
        }
    }
}

using System.Drawing;
using System.Windows.Forms;
using ExileCore2.Shared.Attributes;
using ExileCore2.Shared.Interfaces;
using ExileCore2.Shared.Nodes;

namespace AutoMyAim.Structs;

public class AutoMyAimSettings : ISettings
{
    public ToggleNode Enable { get; set; } = new(false);
    public HotkeyNode AimKey { get; set; } = new(Keys.None);
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
    public UIPanelSettings Panels { get; set; } = new();
    public CursorSettings Cursor { get; set; } = new();

    [Submenu(CollapsedByDefault = false)]
    public class WeightVisualsSettings
    {
        public ToggleNode ShowWeights { get; set; } = new(true);
        public ColorNode WeightTextColor { get; set; } = new(Color.FromArgb(255, 255, 255, 255));
    }

    [Submenu(CollapsedByDefault = true)]
    public class UIPanelSettings
    {
        public ToggleNode RenderAndWorkOnFullPanels { get; set; } = new(false);
        public ToggleNode RenderAndWorkOnleftPanels { get; set; } = new(false);
        public ToggleNode RenderAndWorkOnRightPanels { get; set; } = new(false);

        public PaddingSettings Padding { get; set; } = new();

        [Submenu(CollapsedByDefault = true)]
        public class PaddingSettings
        {
            public RangeNode<int> Left { get; set; } = new(72, 0, 2000);
            public RangeNode<int> Right { get; set; } = new(62, 0, 2000);
            public RangeNode<int> Top { get; set; } = new(85, 0, 2000);
            public RangeNode<int> Bottom { get; set; } = new(235, 0, 2000);
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
    public CustomTargetSettings CustomTargets { get; set; } = new();

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

    [Submenu(CollapsedByDefault = true)]
    public class CustomTargetSettings
    {
        public ListNode Priorities { get; set; } = new();
        public RangeNode<float> Weight { get; set; } = new(2.0f, 0f, 10f);
    }
}

[Submenu(CollapsedByDefault = true)]
public class RaycastSettings
{
    public RangeNode<int> Length { get; set; } = new(140, 1, 1000);
    public RangeNode<int> Count { get; set; } = new(600, 8, 720);
    public RangeNode<int> GridSize { get; set; } = new(80, 1, 1000);
    public RangeNode<int> TargetLayerValue { get; set; } = new(2, 0, 5);

    public VisualsSettings Visuals { get; set; } = new();

    [Submenu(CollapsedByDefault = false)]
    public class VisualsSettings
    {
        public ToggleNode DrawAtPlayerPlane { get; set; } = new(true);
        public ToggleNode ShowRayLines { get; set; } = new(true);
        public ToggleNode ShowTerrainValues { get; set; } = new(true);
        public RangeNode<float> RayLineThickness { get; set; } = new(1.0f, 1.0f, 5.0f);

        public ColorSettings Colors { get; set; } = new();

        [Submenu(CollapsedByDefault = true)]
        public class ColorSettings
        {
            public ColorNode Visible { get; set; } = new(Color.FromArgb(92, 255, 245, 0));
            public ColorNode Shadow { get; set; } = new(Color.FromArgb(156, 255, 0, 0));
            public ColorNode RayLine { get; set; } = new(Color.FromArgb(94, 35, 245, 0));
        }
    }
}
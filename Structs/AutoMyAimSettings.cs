using System.Drawing;
using System.Windows.Forms;
using ExileCore2.Shared.Interfaces;
using ExileCore2.Shared.Nodes;

namespace AutoMyAim.Structs;

public class AutoMyAimSettings : ISettings
{
    public ToggleNode EnableDrawing { get; set; } = new(true);
    public ToggleNode RenderOnFullPanels { get; set; } = new(true);
    public ToggleNode RenderOnleftPanels { get; set; } = new(true);
    public ToggleNode RenderOnRightPanels { get; set; } = new(true);
    public HotkeyNode AimKey { get; set; } = new(Keys.None);
    public RangeNode<int> EntityScanDistance { get; set; } = new(100, 1, 500);
    public RangeNode<int> TargetLayerValue { get; set; } = new(0, 0, 5);
    public ToggleNode DrawAtPlayerPlane { get; set; } = new(true);
    public ToggleNode ShowRayLines { get; set; } = new(false);
    public ToggleNode ShowTerrainValues { get; set; } = new(true);
    public RangeNode<int> GridSize { get; set; } = new(60, 1, 1000);
    public RangeNode<int> RayLength { get; set; } = new(150, 1, 1000);
    public RangeNode<int> RayCount { get; set; } = new(360, 8, 720);
    public RangeNode<float> RayLineThickness { get; set; } = new(1.0f, 1.0f, 5.0f);
    public ColorNode VisibleColor { get; set; } = new(Color.White);
    public ColorNode ShadowColor { get; set; } = new(Color.FromArgb(30, 30, 30));
    public ColorNode RayLineColor { get; set; } = new(Color.FromArgb(255, 255, 0));
    public ToggleNode EnableWeighting { get; set; } = new(true);
    public RangeNode<float> DistanceWeight { get; set; } = new(1f, 0f, 5f);
    public RangeNode<float> MaxTargetDistance { get; set; } = new(100f, 0f, 200f);
    public ToggleNode PreferHigherHP { get; set; } = new(false);
    public RangeNode<float> HPWeight { get; set; } = new(1f, 0f, 5f);
    public RangeNode<float> NormalWeight { get; set; } = new(1f, 0f, 10f);
    public RangeNode<float> MagicWeight { get; set; } = new(2f, 0f, 10f);
    public RangeNode<float> RareWeight { get; set; } = new(3f, 0f, 10f);
    public RangeNode<float> UniqueWeight { get; set; } = new(4f, 0f, 10f);
    public ListNode CustomEntityPriorities { get; set; } = new();
    public RangeNode<float> CustomEntityWeight { get; set; } = new(2f, 0f, 10f);
    public ToggleNode ShowWeights { get; set; } = new(false);
    public ColorNode WeightTextColor { get; set; } = new(Color.Yellow);
    public RangeNode<int> AcceptableRadius { get; set; } = new(50, 1, 200);
    public ToggleNode RandomizeInRadius { get; set; } = new(true);
    public RangeNode<int> LeftPadding { get; set; } = new(20, 0, 1000);
    public RangeNode<int> RightPadding { get; set; } = new(20, 0, 1000);
    public RangeNode<int> TopPadding { get; set; } = new(20, 0, 1000);
    public RangeNode<int> BottomPadding { get; set; } = new(20, 0, 1000);
    public ToggleNode ShowDebug { get; set; } = new(false);
    public ToggleNode ConfineCursorToCircle { get; set; } = new(false);
    public RangeNode<int> CursorCircleRadius { get; set; } = new(300, 50, 1000);
    public ToggleNode PointToOffscreenTargetsOtherwiseFindNextTargetInBounds { get; set; } = new(false);
    public ToggleNode Enable { get; set; } = new(true);
}
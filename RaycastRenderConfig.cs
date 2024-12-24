using ExileCore2.Shared.Nodes;

namespace AutoMyAim;

public class RaycastRenderConfig
{
    public ToggleNode ShowRayLines { get; set; }
    public ToggleNode ShowTerrainValues { get; set; }
    public RangeNode<int> TargetLayerValue { get; set; }
    public RangeNode<int> GridSize { get; set; }
    public RangeNode<int> RayLength { get; set; }
    public RangeNode<int> RayCount { get; set; }
    public RangeNode<float> RayLineThickness { get; set; }
    public ColorNode VisibleColor { get; set; }
    public ColorNode ShadowColor { get; set; }
    public ColorNode RayLineColor { get; set; }
    public ToggleNode DrawAtPlayerPlane { get; set; }
}
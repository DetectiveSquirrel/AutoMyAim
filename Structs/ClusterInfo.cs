using System.Collections.Generic;
using System.Numerics;

namespace AutoMyAim.Structs;

public class ClusterInfo
{
    public List<TrackedEntity> Entities { get; set; }
    public Vector2 Center { get; set; }
    public float Radius { get; set; }
}
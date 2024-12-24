using ExileCore2.PoEMemory.MemoryObjects;

namespace AutoMyAim.Structs;

public class TrackedEntity
{
    public Entity Entity { get; set; }
    public float Weight { get; set; }
    public float Distance { get; set; }
}
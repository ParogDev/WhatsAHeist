using ExileCore.Shared.Attributes;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using SharpDX;

namespace WhatsAHeist;

public class WhatsAHeistSettings : ISettings
{
    public ToggleNode Enable { get; set; } = new(true);

    public RouteSettings Route { get; set; } = new();
    public DoorSettings Doors { get; set; } = new();
    public EndpointSettings Endpoints { get; set; } = new();
    public EscapeSettings Escape { get; set; } = new();
    public HeistGeneralSettings General { get; set; } = new();
}

[Submenu(CollapsedByDefault = true)]
public class RouteSettings
{
    public ToggleNode ShowWorldPath { get; set; } = new(true);
    public ToggleNode ShowMinimapPath { get; set; } = new(true);
    public ColorNode WorldPathColor { get; set; } = new Color(0, 255, 255, 255);
    public RangeNode<int> WorldPathThickness { get; set; } = new(3, 1, 5);
    public RangeNode<int> DrawEveryNthSegment { get; set; } = new(2, 1, 10);
    public ToggleNode ShowDirectionArrow { get; set; } = new(true);
    public RangeNode<int> ArrowSize { get; set; } = new(40, 20, 80);
    public RangeNode<int> ArrowDistance { get; set; } = new(120, 60, 300);
}

[Submenu(CollapsedByDefault = true)]
public class DoorSettings
{
    public ToggleNode ShowDoors { get; set; } = new(true);
    public ColorNode ClosedDoorColor { get; set; } = new Color(255, 60, 60, 255);
    public ColorNode OpenDoorColor { get; set; } = new Color(60, 255, 60, 255);
    public ToggleNode ShowJobLabel { get; set; } = new(true);
    public RangeNode<float> DoorWeightMultiplier { get; set; } = new(2.0f, 1.0f, 5.0f);
}

[Submenu(CollapsedByDefault = true)]
public class EndpointSettings
{
    public ToggleNode ShowEndpoints { get; set; } = new(true);
    public ColorNode EndpointColor { get; set; } = new Color(255, 215, 0, 255);
    public ToggleNode ShowRewardLabel { get; set; } = new(true);
    public ToggleNode ShowOnMinimap { get; set; } = new(true);
    public RangeNode<int> MinimapIconSize { get; set; } = new(15, 5, 30);
}

[Submenu(CollapsedByDefault = true)]
public class EscapeSettings
{
    public ToggleNode ShowEscapeRoute { get; set; } = new(true);
    public ColorNode EscapePathColor { get; set; } = new Color(255, 100, 50, 255);
    public ToggleNode FlashOnAlarmMax { get; set; } = new(true);
}

[Submenu(CollapsedByDefault = true)]
public class HeistGeneralSettings
{
    public RangeNode<int> MaxDrawDistance { get; set; } = new(200, 50, 500);
    public RangeNode<int> GraphConnectionDistance { get; set; } = new(150, 50, 300);
}

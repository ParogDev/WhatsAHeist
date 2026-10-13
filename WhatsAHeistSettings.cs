using ExileCore.Shared.Attributes;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using SharpDX;

namespace WhatsAHeist;

// Drawn by SettingsMenu (see SettingsMenu.cs and HeistSettingsUi.cs): every root [Submenu] is a tab with its card open,
// [Menu] gives the label and the description, [MenuTab] merges General onto Route, [MenuUnit] suffixes a slider's
// readout, and [Menu(..., index, parentIndex)] hangs a row under the toggle it belongs to.
public class WhatsAHeistSettings : ISettings
{
    [Menu("Enable", "Turn the whole plugin on or off")]
    public ToggleNode Enable { get; set; } = new(true);

    [Menu("Route", "Path to the next objective: world line, map line and direction arrow")]
    public RouteSettings Route { get; set; } = new();

    [Menu("Doors", "Door markers and how much the route avoids closed doors")]
    public DoorSettings Doors { get; set; } = new();

    [Menu("Endpoints", "Objective and endpoint markers, labels and map dots")]
    public EndpointSettings Endpoints { get; set; } = new();

    [Menu("Escape", "Escape-phase path color and alarm flash")]
    public EscapeSettings Escape { get; set; } = new();

    [MenuTab("Route")]
    [Menu("General", "Draw distance and route graph tuning")]
    public HeistGeneralSettings General { get; set; } = new();
}

[Submenu]
public class RouteSettings
{
    [Menu("Show World Path", "Draw the route as lines on the ground", 1)]
    public ToggleNode ShowWorldPath { get; set; } = new(true);

    [Menu("Show Map Path", "Draw the route on the large map")]
    public ToggleNode ShowMinimapPath { get; set; } = new(true);

    [Menu("Path Color", "Color of the route line and arrow outside the escape phase")]
    public ColorNode WorldPathColor { get; set; } = new Color(0, 255, 255, 255);

    [MenuUnit("px")]
    [Menu("Thickness", "Route line thickness, in px (1-5)", 2, 1)]
    public RangeNode<int> WorldPathThickness { get; set; } = new(3, 1, 5);

    [Menu("Draw Every Nth", "Draw only every Nth path segment to save time (1 = all, up to 10)", 3, 1)]
    public RangeNode<int> DrawEveryNthSegment { get; set; } = new(2, 1, 10);

    [Menu("Show Arrow", "Compass arrow pointing toward the next route waypoint", 4)]
    public ToggleNode ShowDirectionArrow { get; set; } = new(true);

    [MenuUnit("px")]
    [Menu("Arrow Size", "Size of the arrow triangle, in px (20-80)", 5, 4)]
    public RangeNode<int> ArrowSize { get; set; } = new(40, 20, 80);

    [MenuUnit("px")]
    [Menu("Arrow Distance", "Arrow distance from the player's screen position, in px (60-300)", 6, 4)]
    public RangeNode<int> ArrowDistance { get; set; } = new(120, 60, 300);
}

[Submenu]
public class DoorSettings
{
    [Menu("Show Doors", "Draw colored circles at door positions", 1)]
    public ToggleNode ShowDoors { get; set; } = new(true);

    [Menu("Closed Color", "Color for locked/closed doors")]
    public ColorNode ClosedDoorColor { get; set; } = new Color(255, 60, 60, 255);

    [Menu("Open Color", "Color for opened doors")]
    public ColorNode OpenDoorColor { get; set; } = new Color(60, 255, 60, 255);

    [Menu("Job Labels", "Show the crew job a door needs above it", 2, 1)]
    public ToggleNode ShowJobLabel { get; set; } = new(true);

    [MenuUnit("x")]
    [Menu("Door Weight", "Route cost multiplier for closed doors (1-5, higher = avoid more)")]
    public RangeNode<float> DoorWeightMultiplier { get; set; } = new(2.0f, 1.0f, 5.0f);
}

[Submenu]
public class EndpointSettings
{
    [Menu("Show Endpoints", "Draw markers at endpoint locations")]
    public ToggleNode ShowEndpoints { get; set; } = new(true);

    [Menu("Endpoint Color", "Color for the objective endpoint (others are dimmed)")]
    public ColorNode EndpointColor { get; set; } = new Color(255, 215, 0, 255);

    [Menu("Endpoint Labels", "Show the door job or Objective label at endpoints")]
    public ToggleNode ShowRewardLabel { get; set; } = new(true);

    [Menu("Show on Map", "Draw endpoint dots on the large map", 1)]
    public ToggleNode ShowOnMinimap { get; set; } = new(true);

    [MenuUnit("px")]
    [Menu("Map Dot Size", "Size of the endpoint map dot, in px (5-30)", 2, 1)]
    public RangeNode<int> MinimapIconSize { get; set; } = new(15, 5, 30);
}

[Submenu]
public class EscapeSettings
{
    [Menu("Show Escape Route", "Reserved for hiding the escape path; not read by the plugin yet (the escape path always draws)")]
    public ToggleNode ShowEscapeRoute { get; set; } = new(true);

    [Menu("Escape Color", "Route line and arrow color once the alarm starts the escape phase")]
    public ColorNode EscapePathColor { get; set; } = new Color(255, 100, 50, 255);

    [Menu("Flash on Alarm", "Pulse a red screen border during the escape phase")]
    public ToggleNode FlashOnAlarmMax { get; set; } = new(true);
}

[Submenu]
public class HeistGeneralSettings
{
    [MenuUnit("grid")]
    [Menu("Max Draw Distance", "Hide overlays beyond this distance from the player, in grid units (50-500)")]
    public RangeNode<int> MaxDrawDistance { get; set; } = new(200, 50, 500);

    [MenuUnit("grid")]
    [Menu("Graph Distance", "Max distance between path nodes linked into the route graph, in grid units (50-300)")]
    public RangeNode<int> GraphConnectionDistance { get; set; } = new(150, 50, 300);
}

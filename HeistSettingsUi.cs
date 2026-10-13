#nullable enable
using System.Numerics;
using ImGuiNET;

namespace WhatsAHeist;

public record HeistUiState(
    int NodeCount,
    int EndpointCount,
    int DoorCount,
    int ClosedDoorCount,
    int ChestCount,
    HeistPhase Phase,
    bool HasGraph,
    bool HasRoute
);

// Settings panel: the generated SettingsMenu (see SettingsMenu.cs) draws every setting from its [Menu] label and
// tooltip; tabs come from the [Submenu] properties (Route / Doors / Endpoints / Escape, General joins Route via
// [MenuTab]). Hand-drawn on top: the live status strip (phase + counts) above the tabs, and per tab an explanation of
// the mechanic, live status lines, the door colour legend and the escape-phase banner.
public sealed class HeistSettingsUi
{
    private readonly SettingsMenu _menu;
    private HeistUiState? _state;
    private WhatsAHeistSettings? _settings;

    // Dark teal/vault accent (#1A936F)
    private static readonly Vector4 Accent = new(0.102f, 0.576f, 0.435f, 1f);
    private static readonly Vector4 StatusGreen = new(0.2f, 0.85f, 0.3f, 1f);
    private static readonly Vector4 StatusRed = new(0.85f, 0.2f, 0.2f, 1f);
    private static readonly Vector4 StatusDim = new(0.3f, 0.3f, 0.35f, 1f);
    private static readonly Vector4 StatusOrange = new(0.95f, 0.55f, 0.15f, 1f);

    public HeistSettingsUi()
    {
        _menu = new SettingsMenu { Accent = Accent, RootTabName = "Route" }
            .Banner(DrawStatusBar)
            .Tab("Route", DrawRouteIntro, first: true)
            .Tab("Doors", DrawDoorsIntro, first: true)
            .Tab("Endpoints", DrawEndpointsIntro, first: true)
            .Tab("Escape", DrawEscapeIntro, first: true);
    }

    public void Draw(WhatsAHeistSettings s, HeistUiState state)
    {
        _settings = s;
        _state = state;
        _menu.Draw(s);
    }

    private void DrawStatusBar()
    {
        var state = _state;
        if (state == null) return;
        bool escape = state.Phase == HeistPhase.Escape;
        Dot(escape ? StatusOrange : Accent, escape ? "ESCAPE" : "INFILTRATION", escape ? StatusOrange : Accent);
        CountDot(state.NodeCount, "Nodes");
        CountDot(state.EndpointCount, "Endpoints");
        CountDot(state.DoorCount, "Doors");
        if (state.ClosedDoorCount > 0)
        {
            ImGui.SameLine(0, 14f);
            SettingsMenu.Text(StatusRed, $"({state.ClosedDoorCount} closed)");
        }
        ImGui.Spacing();
    }

    private static void CountDot(int count, string label)
    {
        ImGui.SameLine(0, 18f);
        var c = count > 0 ? Accent : StatusDim;
        Dot(c, $"{count} {label}", count > 0 ? ImGui.GetStyle().Colors[(int)ImGuiCol.Text] : StatusDim);
    }

    private void DrawRouteIntro()
    {
        InfoBlock("Heist Route",
            "Connects HeistPathNode entities into a navigation graph. Dijkstra pathfinding routes to the objective "
            + "(infiltration) or back to the entrance (escape phase).");
        var state = _state;
        if (state == null) return;
        StatusLine(state.HasRoute ? 1 : 0, "active route", "computed");
        StatusLine(state.HasGraph ? state.NodeCount : 0, "graph node", "connected");
        ImGui.Spacing();
    }

    private void DrawDoorsIntro()
    {
        InfoBlock("NPC Skill Doors",
            "Heist doors require specific NPC jobs to open. Closed doors add weight to route pathfinding, "
            + "steering you toward paths with fewer obstacles.");
        var d = _settings?.Doors;
        if (d != null)
        {
            Legend(ToVec(d.ClosedDoorColor.Value), "Closed", "Requires an NPC skill - adds route weight");
            Legend(ToVec(d.OpenDoorColor.Value), "Open", "Already opened - no route penalty");
        }
        var state = _state;
        if (state == null) return;
        StatusLine(state.DoorCount, "door", "tracked");
        StatusLine(state.ClosedDoorCount, "closed door", "blocking");
        ImGui.Spacing();
    }

    private void DrawEndpointsIntro()
    {
        InfoBlock("Heist Endpoints",
            "HeistPathEndpoint entities mark locked door locations. The farthest endpoint from the entrance is the "
            + "objective; other endpoints show their NPC door job.");
        var state = _state;
        if (state == null) return;
        StatusLine(state.EndpointCount, "endpoint", "found");
        StatusLine(state.ChestCount, "reward chest", "visible");
        ImGui.Spacing();
    }

    private void DrawEscapeIntro()
    {
        var state = _state;
        if (state != null)
        {
            bool escape = state.Phase == HeistPhase.Escape;
            PhaseBanner(escape ? "ESCAPE PHASE ACTIVE" : "Infiltration Phase", escape ? StatusOrange : Accent);
        }
        InfoBlock("Escape Route",
            "When reinforcements spawn (alarm maxed), escape activates. The route switches to guide you back to "
            + "the entrance, drawn in the escape color.");
    }

    // ---- small cursor-flow widgets (they sit above the generated cards, so they scroll and filter with the tab) ----

    private static void Dot(Vector4 dot, string text, Vector4 textColor)
    {
        var p = ImGui.GetCursorScreenPos();
        float h = ImGui.GetTextLineHeight();
        ImGui.GetWindowDrawList().AddCircleFilled(new Vector2(p.X + 4f, p.Y + h * 0.5f), 4f, ImGui.GetColorU32(dot));
        ImGui.Dummy(new Vector2(10f, h));
        ImGui.SameLine(0, 4f);
        SettingsMenu.Text(textColor, text);
    }

    private static void StatusLine(int count, string singular, string suffix)
    {
        string noun = count == 1 ? singular : singular + "s";
        Dot(count > 0 ? StatusGreen : StatusDim, $"{count} {noun} {suffix}",
            count > 0 ? ImGui.GetStyle().Colors[(int)ImGuiCol.Text] : StatusDim);
    }

    private static void Legend(Vector4 color, string name, string desc)
    {
        Dot(color, name, ImGui.GetStyle().Colors[(int)ImGuiCol.Text]);
        ImGui.SameLine(0, 6f);
        SettingsMenu.Dim("- " + desc);
    }

    private static void InfoBlock(string title, string body)
    {
        var dl = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        ImGui.Indent(12f);
        SettingsMenu.Text(Accent, title);
        ImGui.PushTextWrapPos(0f);
        SettingsMenu.Dim(body);
        ImGui.PopTextWrapPos();
        ImGui.Unindent(12f);
        var end = ImGui.GetCursorScreenPos();
        dl.AddRectFilled(start, new Vector2(start.X + 3f, end.Y - 2f), ImGui.GetColorU32(Accent with { W = 0.4f }), 1f);
        ImGui.Spacing();
    }

    private static void PhaseBanner(string text, Vector4 color)
    {
        var dl = ImGui.GetWindowDrawList();
        var min = ImGui.GetCursorScreenPos();
        var max = min + new Vector2(ImGui.GetContentRegionAvail().X, 24f);
        dl.AddRectFilled(min, max, ImGui.GetColorU32(color with { W = 0.15f }), 4f);
        dl.AddRect(min, max, ImGui.GetColorU32(color with { W = 0.4f }), 4f);
        var sz = ImGui.CalcTextSize(text);
        dl.AddText((min + max) * 0.5f - sz * 0.5f, ImGui.GetColorU32(color), text);
        ImGui.Dummy(new Vector2(1f, 28f));
    }

    private static Vector4 ToVec(SharpDX.Color c) => new(c.R / 255f, c.G / 255f, c.B / 255f, 1f);
}

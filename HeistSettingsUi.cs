using System;
using System.Collections.Generic;
using System.Numerics;
using ExileCore.Shared.Nodes;
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

public sealed class HeistSettingsUi
{
    private int _activeTab;
    private readonly Dictionary<string, float> _anims = new();

    private static readonly string[] Tabs = { "Route", "Doors", "Endpoints", "Escape" };

    // Dark teal/vault accent (#1A936F)
    private static uint Accent => Col(0.102f, 0.576f, 0.435f);
    private static uint AccentDim => Col(0.07f, 0.38f, 0.29f);
    private static uint Label => Col(0.88f, 0.88f, 0.88f);
    private static uint Desc => Col(0.38f, 0.40f, 0.44f);
    private static uint TabOff => Col(0.48f, 0.48f, 0.52f);
    private static uint CardBg => Col(0.05f, 0.05f, 0.07f, 1f);

    private static uint StatusGreen => Col(0.2f, 0.85f, 0.3f);
    private static uint StatusRed => Col(0.85f, 0.2f, 0.2f);
    private static uint StatusDim => Col(0.3f, 0.3f, 0.35f);
    private static uint StatusOrange => Col(0.95f, 0.55f, 0.15f);

    private const float Row = 34f;

    public void Draw(WhatsAHeistSettings s, HeistUiState state)
    {
        var contentMin = ImGui.GetCursorScreenPos();
        float contentW = ImGui.GetContentRegionAvail().X;
        var dl = ImGui.GetWindowDrawList();

        // Tab bar
        float tabH = 26f;
        float tabW = contentW / Tabs.Length;

        for (int i = 0; i < Tabs.Length; i++)
        {
            var tMin = new Vector2(contentMin.X + i * tabW, contentMin.Y);
            var tMax = new Vector2(contentMin.X + (i + 1) * tabW, contentMin.Y + tabH);
            bool active = i == _activeTab;

            if (active)
                dl.AddRectFilled(tMin, tMax, WithAlpha(Accent, 0.12f));

            ImGui.SetCursorScreenPos(tMin);
            ImGui.InvisibleButton($"##ht_tab_{i}", tMax - tMin);
            bool hov = ImGui.IsItemHovered();
            if (ImGui.IsItemClicked()) _activeTab = i;

            if (hov && !active)
                dl.AddRectFilled(tMin, tMax, WithAlpha(Accent, 0.06f));
            if (active)
                dl.AddLine(tMin with { Y = tMax.Y - 1 } + new Vector2(3, 0),
                    tMax - new Vector2(3, 1), Accent, 2f);

            uint tc = active ? Accent : (hov ? AccentDim : TabOff);
            CenterText(dl, Tabs[i], (tMin + tMax) * 0.5f, tc);
        }

        float sepY = contentMin.Y + tabH + 1;
        dl.AddLine(new Vector2(contentMin.X, sepY),
            new Vector2(contentMin.X + contentW, sepY), WithAlpha(Accent, 0.10f), 1f);

        // Status bar
        float statusY = sepY + 4;
        DrawStatusBar(dl, contentMin.X, contentW, statusY, state);
        float statusH = 22f;

        // Content area
        ImGui.SetCursorScreenPos(new Vector2(contentMin.X, sepY + statusH + 6));
        var avail = new Vector2(contentW, ImGui.GetContentRegionAvail().Y);
        ImGui.BeginChild("##ht_content", avail, ImGuiChildFlags.None, ImGuiWindowFlags.None);

        var cdl = ImGui.GetWindowDrawList();
        var cMin = ImGui.GetWindowPos();
        var cSz = ImGui.GetWindowSize();

        cdl.AddRectFilled(cMin, cMin + cSz, CardBg, 3f);
        float pulse = (float)(0.4 + 0.3 * Math.Sin(ImGui.GetTime() * 1.8));
        cdl.AddRect(cMin, cMin + cSz, WithAlpha(Accent, pulse * 0.18f), 3f, ImDrawFlags.None, 1f);

        float scrollY = ImGui.GetScrollY();
        float y = cMin.Y + 10 - scrollY;
        float x = cMin.X + 12;
        float cx = cMin.X + cSz.X * 0.50f;
        float sw = cSz.X * 0.40f;

        switch (_activeTab)
        {
            case 0: TabRoute(cdl, s.Route, s.General, x, cx, ref y, sw, cSz.X, state); break;
            case 1: TabDoors(cdl, s.Doors, x, cx, ref y, sw, state); break;
            case 2: TabRewards(cdl, s.Endpoints, x, cx, ref y, sw, state); break;
            case 3: TabEscape(cdl, s.Escape, x, cx, ref y, sw, state); break;
        }

        ImGui.SetCursorScreenPos(new Vector2(x, y));
        ImGui.Dummy(new Vector2(1, 4));
        ImGui.EndChild();
    }

    private static void DrawStatusBar(ImDrawListPtr dl, float startX, float width, float y, HeistUiState state)
    {
        float x = startX + 12;

        // Phase indicator
        string phaseText = state.Phase == HeistPhase.Escape ? "ESCAPE" : "INFILTRATION";
        uint phaseCol = state.Phase == HeistPhase.Escape ? StatusOrange : Accent;
        dl.AddCircleFilled(new Vector2(x + 4, y + 8), 4f, phaseCol);
        dl.AddText(new Vector2(x + 12, y + 1), phaseCol, phaseText);
        x += ImGui.CalcTextSize(phaseText).X + 26;

        var items = new (string label, int count)[]
        {
            ("Nodes", state.NodeCount),
            ("Endpoints", state.EndpointCount),
            ("Doors", state.DoorCount),
        };

        foreach (var (label, count) in items)
        {
            uint dotCol = count > 0 ? Accent : StatusDim;
            dl.AddCircleFilled(new Vector2(x + 4, y + 8), 4f, dotCol);
            string text = $"{count} {label}";
            uint textCol = count > 0 ? Label : StatusDim;
            dl.AddText(new Vector2(x + 12, y + 1), textCol, text);
            x += ImGui.CalcTextSize(text).X + 26;
        }

        // Closed doors indicator
        if (state.ClosedDoorCount > 0)
        {
            string closedText = $"({state.ClosedDoorCount} closed)";
            dl.AddText(new Vector2(x, y + 1), StatusRed, closedText);
        }
    }

    private void TabRoute(ImDrawListPtr dl, RouteSettings s, HeistGeneralSettings g,
        float x, float cx, ref float y, float sw, float cardW, HeistUiState state)
    {
        DrawInfoBlock(dl, x, ref y,
            "Heist Route",
            "Connects HeistPathNode entities into a navigation graph.\n" +
            "Dijkstra pathfinding routes to the objective (infiltration)\n" +
            "or back to the entrance (escape phase).");

        // Route status
        DrawStatusLine(dl, x, ref y,
            state.HasRoute ? 1 : 0, "active route", "computed");
        DrawStatusLine(dl, x, ref y,
            state.HasGraph ? state.NodeCount : 0, "graph node", "connected");

        SectionHeader(dl, x, ref y, "World Path");
        Toggle("ht_rt_show", s.ShowWorldPath, dl, x, cx, ref y,
            "Show World Path", "Draw path lines in 3D world space");
        ColorPicker("ht_rt_col", s.WorldPathColor, dl, x, cx, ref y,
            "Path Color", "Color of the route path overlay");
        IntSlider("ht_rt_th", s.WorldPathThickness, dl, x, cx, ref y, sw,
            "Thickness", "Path line thickness in pixels");
        IntSlider("ht_rt_nth", s.DrawEveryNthSegment, dl, x, cx, ref y, sw,
            "Draw Every Nth", "Skip segments for performance (1 = all)");

        SectionHeader(dl, x, ref y, "Minimap");
        Toggle("ht_rt_mmap", s.ShowMinimapPath, dl, x, cx, ref y,
            "Show Minimap Path", "Draw route dots on the minimap");

        SectionHeader(dl, x, ref y, "Direction Arrow");
        Toggle("ht_rt_arr", s.ShowDirectionArrow, dl, x, cx, ref y,
            "Show Arrow", "Compass arrow pointing toward next route waypoint");
        IntSlider("ht_rt_asz", s.ArrowSize, dl, x, cx, ref y, sw,
            "Arrow Size", "Size of the arrow triangle in pixels");
        IntSlider("ht_rt_adst", s.ArrowDistance, dl, x, cx, ref y, sw,
            "Arrow Distance", "Arrow distance from player screen position");

        SectionHeader(dl, x, ref y, "General");
        IntSlider("ht_ge_mdd", g.MaxDrawDistance, dl, x, cx, ref y, sw,
            "Max Draw Distance", "Hide all overlays beyond this grid distance");
        IntSlider("ht_ge_gcd", g.GraphConnectionDistance, dl, x, cx, ref y, sw,
            "Graph Distance", "Max distance to connect path nodes");
    }

    private void TabDoors(ImDrawListPtr dl, DoorSettings s,
        float x, float cx, ref float y, float sw, HeistUiState state)
    {
        DrawInfoBlock(dl, x, ref y,
            "NPC Skill Doors",
            "Heist doors require specific NPC jobs to open.\n" +
            "Closed doors add weight to route pathfinding,\n" +
            "steering you toward paths with fewer obstacles.");

        SectionHeader(dl, x, ref y, "Door Types");
        DrawTierLegendItem(dl, x, ref y, Col(0.85f, 0.2f, 0.2f), "Closed", "Requires NPC skill - adds route weight");
        DrawTierLegendItem(dl, x, ref y, Col(0.2f, 0.85f, 0.2f), "Open", "Already opened - no route penalty");
        DrawStatusLine(dl, x, ref y, state.DoorCount, "door", "tracked");
        DrawStatusLine(dl, x, ref y, state.ClosedDoorCount, "closed door", "blocking");

        SectionHeader(dl, x, ref y, "Visibility");
        Toggle("ht_dr_show", s.ShowDoors, dl, x, cx, ref y,
            "Show Doors", "Render colored circles at door positions");
        Toggle("ht_dr_job", s.ShowJobLabel, dl, x, cx, ref y,
            "Job Labels", "Display required NPC job text above doors");

        SectionHeader(dl, x, ref y, "Colors");
        ColorPicker("ht_dr_cl", s.ClosedDoorColor, dl, x, cx, ref y,
            "Closed Color", "Color for locked/closed doors");
        ColorPicker("ht_dr_op", s.OpenDoorColor, dl, x, cx, ref y,
            "Open Color", "Color for opened doors");

        SectionHeader(dl, x, ref y, "Pathfinding");
        FloatSlider("ht_dr_wt", s.DoorWeightMultiplier, dl, x, cx, ref y, sw,
            "Door Weight", "Route weight multiplier for closed doors (higher = avoid more)");
    }

    private void TabRewards(ImDrawListPtr dl, EndpointSettings s,
        float x, float cx, ref float y, float sw, HeistUiState state)
    {
        DrawInfoBlock(dl, x, ref y,
            "Heist Endpoints",
            "HeistPathEndpoint entities mark locked door locations.\n" +
            "The farthest endpoint from entrance is the objective.\n" +
            "Other endpoints show their NPC door job type.");

        DrawStatusLine(dl, x, ref y, state.EndpointCount, "endpoint", "found");
        DrawStatusLine(dl, x, ref y, state.ChestCount, "reward chest", "visible");

        SectionHeader(dl, x, ref y, "Visibility");
        Toggle("ht_ep_show", s.ShowEndpoints, dl, x, cx, ref y,
            "Show Endpoints", "Render markers at endpoint/door locations");
        Toggle("ht_ep_lbl", s.ShowRewardLabel, dl, x, cx, ref y,
            "Door Labels", "Display door job or Objective label at endpoints");

        SectionHeader(dl, x, ref y, "Minimap");
        Toggle("ht_ep_mmap", s.ShowOnMinimap, dl, x, cx, ref y,
            "Show on Minimap", "Draw endpoint dots on the minimap");
        IntSlider("ht_ep_msz", s.MinimapIconSize, dl, x, cx, ref y, sw,
            "Minimap Size", "Size of minimap dot in pixels");

        SectionHeader(dl, x, ref y, "Appearance");
        ColorPicker("ht_ep_col", s.EndpointColor, dl, x, cx, ref y,
            "Endpoint Color", "Color for objective endpoint (others are dimmed)");
    }

    private void TabEscape(ImDrawListPtr dl, EscapeSettings s,
        float x, float cx, ref float y, float sw, HeistUiState state)
    {
        // Phase banner
        {
            var bannerMin = new Vector2(x + 2, y);
            float cardW = ImGui.GetWindowSize().X - 24;
            var bannerMax = new Vector2(x + cardW, y + 24);
            if (state.Phase == HeistPhase.Escape)
            {
                dl.AddRectFilled(bannerMin, bannerMax, WithAlpha(StatusOrange, 0.15f), 4f);
                dl.AddRect(bannerMin, bannerMax, WithAlpha(StatusOrange, 0.4f), 4f, ImDrawFlags.None, 1f);
                CenterText(dl, "ESCAPE PHASE ACTIVE", (bannerMin + bannerMax) * 0.5f, StatusOrange);
            }
            else
            {
                dl.AddRectFilled(bannerMin, bannerMax, WithAlpha(Accent, 0.10f), 4f);
                dl.AddRect(bannerMin, bannerMax, WithAlpha(Accent, 0.3f), 4f, ImDrawFlags.None, 1f);
                CenterText(dl, "Infiltration Phase", (bannerMin + bannerMax) * 0.5f, Accent);
            }
            y += 30f;
        }

        DrawInfoBlock(dl, x, ref y,
            "Escape Route",
            "When reinforcements spawn (alarm maxed), escape activates.\n" +
            "The route switches to guide you back to the entrance.\n" +
            "Path color changes to the escape color for visibility.");

        SectionHeader(dl, x, ref y, "Escape Path");
        Toggle("ht_es_show", s.ShowEscapeRoute, dl, x, cx, ref y,
            "Show Escape Route", "Draw escape path when alarm triggers");
        ColorPicker("ht_es_col", s.EscapePathColor, dl, x, cx, ref y,
            "Escape Color", "Color of the escape route overlay");
        Toggle("ht_es_flash", s.FlashOnAlarmMax, dl, x, cx, ref y,
            "Flash on Alarm", "Flash screen border when escape phase begins");
    }

    // -- Domain content helpers --

    private static void DrawInfoBlock(ImDrawListPtr dl, float x, ref float y, string title, string body)
    {
        float boxX = x + 2;
        float textX = boxX + 10;
        float startY = y;

        dl.AddText(new Vector2(textX, y + 2), Accent, title);
        y += 18f;

        foreach (var line in body.Split('\n'))
        {
            dl.AddText(new Vector2(textX, y), Desc, line);
            y += 15f;
        }
        y += 4f;

        dl.AddRectFilled(new Vector2(boxX, startY), new Vector2(boxX + 3, y), WithAlpha(Accent, 0.4f), 1f);
        y += 4f;
    }

    private static void DrawStatusLine(ImDrawListPtr dl, float x, ref float y, int count, string singular, string suffix)
    {
        uint dotCol = count > 0 ? StatusGreen : StatusDim;
        dl.AddCircleFilled(new Vector2(x + 8, y + 7), 4f, dotCol);
        string noun = count == 1 ? singular : singular + "s";
        string text = $"{count} {noun} {suffix}";
        dl.AddText(new Vector2(x + 18, y), count > 0 ? Label : StatusDim, text);
        y += 20f;
    }

    private static void DrawTierLegendItem(ImDrawListPtr dl, float x, ref float y, uint dotColor, string name, string desc)
    {
        dl.AddCircleFilled(new Vector2(x + 8, y + 7), 5f, dotColor);
        dl.AddText(new Vector2(x + 20, y), Label, name);
        float nameW = ImGui.CalcTextSize(name).X;
        dl.AddText(new Vector2(x + 24 + nameW, y), Desc, $"- {desc}");
        y += 18f;
    }

    // -- Widget primitives --

    private void SectionHeader(ImDrawListPtr dl, float x, ref float y, string title)
    {
        y += 4f;
        dl.AddText(new Vector2(x, y), Accent, title);
        y += 18f;
        dl.AddLine(new Vector2(x, y - 4), new Vector2(x + ImGui.CalcTextSize(title).X + 40, y - 4),
            WithAlpha(Accent, 0.25f), 1f);
    }

    private void Toggle(string key, ToggleNode node, ImDrawListPtr dl,
        float x, float cx, ref float y, string label, string desc)
    {
        dl.AddText(new Vector2(x + 6, y + 1), Label, label);
        dl.AddText(new Vector2(x + 6, y + 16), Desc, desc);
        ImGui.SetCursorScreenPos(new Vector2(cx, y + 5));
        var v = node.Value;
        _anims.TryGetValue(key, out float a);
        if (PillToggle($"##{key}", ref v, ref a))
            node.Value = v;
        _anims[key] = a;
        y += Row;
    }

    private void IntSlider(string key, RangeNode<int> node, ImDrawListPtr dl,
        float x, float cx, ref float y, float sw, string label, string desc)
    {
        dl.AddText(new Vector2(x + 6, y + 1), Label, label);
        dl.AddText(new Vector2(x + 6, y + 16), Desc, desc);
        ImGui.SetCursorScreenPos(new Vector2(cx, y + 7));
        var v = node.Value;
        if (SliderInt($"##{key}", ref v, node.Min, node.Max, sw))
            node.Value = v;
        dl.AddText(new Vector2(cx + sw + 6, y + 7), Accent, v.ToString());
        y += Row;
    }

    private void FloatSlider(string key, RangeNode<float> node, ImDrawListPtr dl,
        float x, float cx, ref float y, float sw, string label, string desc)
    {
        dl.AddText(new Vector2(x + 6, y + 1), Label, label);
        dl.AddText(new Vector2(x + 6, y + 16), Desc, desc);
        ImGui.SetCursorScreenPos(new Vector2(cx, y + 7));
        var v = node.Value;
        if (SliderFloat($"##{key}", ref v, node.Min, node.Max, sw))
            node.Value = v;
        dl.AddText(new Vector2(cx + sw + 6, y + 7), Accent, $"{v:F1}");
        y += Row;
    }

    private void ColorPicker(string key, ColorNode node, ImDrawListPtr dl,
        float x, float cx, ref float y, string label, string desc)
    {
        dl.AddText(new Vector2(x + 6, y + 1), Label, label);
        dl.AddText(new Vector2(x + 6, y + 16), Desc, desc);

        var c = node.Value;
        ImGui.SetCursorScreenPos(new Vector2(cx, y + 3));
        ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.07f, 0.07f, 0.09f, 1f));
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0.10f, 0.58f, 0.44f, 0.6f));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 3f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f);
        var colorVec = new Vector4(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);
        ImGui.SetNextItemWidth(120);
        if (ImGui.ColorEdit4($"##{key}", ref colorVec,
            ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoLabel))
        {
            node.Value = new SharpDX.Color(
                (int)(colorVec.X * 255), (int)(colorVec.Y * 255),
                (int)(colorVec.Z * 255), (int)(colorVec.W * 255));
        }
        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(2);

        y += Row;
    }

    // -- Self-contained drawing helpers --

    private static bool PillToggle(string id, ref bool value, ref float animState)
    {
        var dl = ImGui.GetWindowDrawList();
        var cur = ImGui.GetCursorScreenPos();
        const float w = 40f, h = 20f, r = h * 0.5f;

        ImGui.InvisibleButton(id, new Vector2(w, h));
        bool changed = ImGui.IsItemClicked();
        if (changed) value = !value;

        float dt = ImGui.GetIO().DeltaTime;
        float target = value ? 1f : 0f;
        animState = Math.Clamp(animState + (target - animState) * Math.Min(dt * 10f, 1f), 0f, 1f);

        uint trackOff = ImGui.GetColorU32(new Vector4(0.2f, 0.2f, 0.2f, 1f));
        uint trackColor = LerpCol(trackOff, Accent, animState);
        dl.AddRectFilled(cur, cur + new Vector2(w, h), trackColor, r);

        float knobX = cur.X + r + animState * (w - h);
        uint knobCol = ImGui.GetColorU32(new Vector4(
            0.5f + 0.5f * animState, 0.5f + 0.5f * animState,
            0.5f + 0.5f * animState, 1f));
        dl.AddCircleFilled(new Vector2(knobX, cur.Y + r), r - 2f, knobCol);

        return changed;
    }

    private static bool SliderInt(string id, ref int value, int min, int max, float width)
    {
        var dl = ImGui.GetWindowDrawList();
        var cur = ImGui.GetCursorScreenPos();
        const float h = 16f, th = 4f, tr = 7f;

        ImGui.InvisibleButton(id, new Vector2(width, h));
        bool changed = false;
        if (ImGui.IsItemActive())
        {
            float frac = Math.Clamp((ImGui.GetMousePos().X - cur.X) / width, 0f, 1f);
            int nv = min + (int)(frac * (max - min));
            if (nv != value) { value = nv; changed = true; }
        }

        float trackY = cur.Y + (h - th) * 0.5f;
        dl.AddRectFilled(cur with { Y = trackY }, new Vector2(cur.X + width, trackY + th),
            ImGui.GetColorU32(new Vector4(0.15f, 0.15f, 0.15f, 1f)), 2f);

        float f = (max > min) ? (float)(value - min) / (max - min) : 0f;
        float fw = f * width;
        dl.AddRectFilled(cur with { Y = trackY }, new Vector2(cur.X + fw, trackY + th), Accent, 2f);

        float tx = cur.X + fw, ty = cur.Y + h * 0.5f;
        dl.AddCircleFilled(new Vector2(tx, ty), tr, Accent);
        dl.AddCircleFilled(new Vector2(tx, ty), tr - 2f,
            ImGui.GetColorU32(new Vector4(0.95f, 0.95f, 0.95f, 1f)));

        return changed;
    }

    private static bool SliderFloat(string id, ref float value, float min, float max, float width)
    {
        var dl = ImGui.GetWindowDrawList();
        var cur = ImGui.GetCursorScreenPos();
        const float h = 16f, th = 4f, tr = 7f;

        ImGui.InvisibleButton(id, new Vector2(width, h));
        bool changed = false;
        if (ImGui.IsItemActive())
        {
            float frac = Math.Clamp((ImGui.GetMousePos().X - cur.X) / width, 0f, 1f);
            float nv = min + frac * (max - min);
            // Round to 0.1
            nv = MathF.Round(nv * 10f) / 10f;
            if (Math.Abs(nv - value) > 0.001f) { value = nv; changed = true; }
        }

        float trackY = cur.Y + (h - th) * 0.5f;
        dl.AddRectFilled(cur with { Y = trackY }, new Vector2(cur.X + width, trackY + th),
            ImGui.GetColorU32(new Vector4(0.15f, 0.15f, 0.15f, 1f)), 2f);

        float f = (max > min) ? (value - min) / (max - min) : 0f;
        float fw = f * width;
        dl.AddRectFilled(cur with { Y = trackY }, new Vector2(cur.X + fw, trackY + th), Accent, 2f);

        float tx = cur.X + fw, ty = cur.Y + h * 0.5f;
        dl.AddCircleFilled(new Vector2(tx, ty), tr, Accent);
        dl.AddCircleFilled(new Vector2(tx, ty), tr - 2f,
            ImGui.GetColorU32(new Vector4(0.95f, 0.95f, 0.95f, 1f)));

        return changed;
    }

    // -- Color utilities --

    private static uint Col(float r, float g, float b, float a = 1f)
        => ImGui.GetColorU32(new Vector4(r, g, b, a));

    private static uint WithAlpha(uint color, float alpha)
    {
        var v = ImGui.ColorConvertU32ToFloat4(color);
        v.W = alpha;
        return ImGui.GetColorU32(v);
    }

    private static uint LerpCol(uint a, uint b, float t)
    {
        var va = ImGui.ColorConvertU32ToFloat4(a);
        var vb = ImGui.ColorConvertU32ToFloat4(b);
        return ImGui.GetColorU32(new Vector4(
            va.X + (vb.X - va.X) * t, va.Y + (vb.Y - va.Y) * t,
            va.Z + (vb.Z - va.Z) * t, va.W + (vb.W - va.W) * t));
    }

    private static void CenterText(ImDrawListPtr dl, string text, Vector2 center, uint color)
    {
        var sz = ImGui.CalcTextSize(text);
        dl.AddText(center - sz * 0.5f, color, text);
    }
}

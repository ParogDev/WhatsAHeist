using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;
using ExileCore.Shared.Helpers;
using ImGuiNET;
using Color = SharpDX.Color;
using Vector2 = System.Numerics.Vector2;
using Vector3 = System.Numerics.Vector3;

namespace WhatsAHeist;

public class WhatsAHeist : BaseSettingsPlugin<WhatsAHeistSettings>
{
    // Entity tracking
    private HeistPathNodeData _pathStart;
    private readonly Dictionary<uint, HeistPathNodeData> _pathNodes = new();
    private readonly Dictionary<uint, HeistPathNodeData> _pathEndpoints = new();
    private readonly Dictionary<uint, HeistDoorData> _doors = new();
    private readonly Dictionary<uint, HeistChestData> _chests = new();
    private Entity _escapeRoute;

    // Entrance reference position (from Escape Route AreaTransition or HeistPathStart)
    private Vector2 _entranceGridPos;
    private bool _hasEntrance;

    // Graph + pathfinding
    private readonly HeistGraph _graph = new();
    private List<GraphNode> _currentRoute;
    private bool _graphDirty = true;
    private readonly Stopwatch _graphThrottle = Stopwatch.StartNew();
    private Vector2 _lastRoutePlayerPos;

    // Phase
    private HeistPhase _currentPhase = HeistPhase.Infiltration;
    private bool _isHeistZone;
    private bool _initialLoadComplete;
    private bool _reinforcementDetected;

    // Minimap state
    private bool? _largeMap;
    private float _mapScale;
    private Vector2 _mapCenter;

    // Settings UI
    private HeistSettingsUi _settingsUi;

    // Constants
    private const float GridToWorldMultiplier = 250f / 23f;
    private const float CameraAngle = 38.7f * MathF.PI / 180;
    private static readonly float CameraAngleCos = MathF.Cos(CameraAngle);
    private static readonly float CameraAngleSin = MathF.Sin(CameraAngle);

    private float[][] _heightData;
    private Camera Camera => GameController.IngameState.Camera;

    public override bool Initialise()
    {
        Name = "Whats A Heist";
        return true;
    }

    public override void AreaChange(AreaInstance area)
    {
        _pathStart = null;
        _pathNodes.Clear();
        _pathEndpoints.Clear();
        _doors.Clear();
        _chests.Clear();
        _escapeRoute = null;
        _entranceGridPos = Vector2.Zero;
        _hasEntrance = false;
        _currentRoute = null;
        _graphDirty = true;
        _currentPhase = HeistPhase.Infiltration;
        _isHeistZone = false;
        _initialLoadComplete = false;
        _reinforcementDetected = false;
        _lastRoutePlayerPos = Vector2.Zero;
        _heightData = GameController.IngameState.Data.RawTerrainHeightData;
    }

    public override void EntityAdded(Entity entity)
    {
        var path = entity.Path;
        if (string.IsNullOrEmpty(path))
            return;

        if (path.Contains("HeistPathStart", StringComparison.Ordinal))
        {
            _isHeistZone = true;
            _pathStart = new HeistPathNodeData(entity, HeistNodeType.Start);
            _graphDirty = true;
            // HeistPathStart marks the entrance if we don't have the AreaTransition yet
            if (!_hasEntrance)
            {
                _entranceGridPos = entity.GridPosNum;
                _hasEntrance = true;
            }
        }
        else if (path.Contains("HeistPathEndpoint", StringComparison.Ordinal))
        {
            _isHeistZone = true;
            _pathEndpoints[entity.Id] = new HeistPathNodeData(entity, HeistNodeType.Endpoint);
            _graphDirty = true;
        }
        else if (path.Contains("HeistPathNode", StringComparison.Ordinal))
        {
            _isHeistZone = true;
            _pathNodes[entity.Id] = new HeistPathNodeData(entity, HeistNodeType.Node);
            _graphDirty = true;
        }
        else if (path.Contains("Door_NPC", StringComparison.Ordinal) ||
                 path.Contains("HeistLockPick", StringComparison.Ordinal))
        {
            _doors[entity.Id] = new HeistDoorData(entity);
        }
        else if (path.Contains("AreaTransition", StringComparison.Ordinal))
        {
            var render = entity.GetComponent<Render>();
            if (render?.Name?.Contains("Escape", StringComparison.OrdinalIgnoreCase) == true)
            {
                _escapeRoute = entity;
                _isHeistZone = true;
                // The Escape Route AreaTransition is always near the heist entrance
                _entranceGridPos = entity.GridPosNum;
                _hasEntrance = true;
            }
        }
        else if (path.Contains("Heist", StringComparison.Ordinal) &&
                 path.Contains("Chest", StringComparison.Ordinal))
        {
            _chests[entity.Id] = new HeistChestData(entity);
        }

        // Detect reinforcement spawns (alarm maxed) after initial zone load
        if (_initialLoadComplete && !_reinforcementDetected &&
            (path.Contains("HeistSoldier", StringComparison.Ordinal) ||
             path.Contains("LeagueHeist/Military", StringComparison.Ordinal)))
        {
            _reinforcementDetected = true;
        }
    }

    public override Job Tick()
    {
        if (!GameController.InGame || !_isHeistZone) return null;

        // Update door states
        foreach (var door in _doors.Values)
        {
            if (!door.Entity.IsValid) continue;
            door.GridPos = door.Entity.GridPosNum;

            var targetable = door.Entity.GetComponent<Targetable>();
            if (targetable != null)
            {
                door.IsClosed = targetable.isTargetable;
            }
            else
            {
                var sm = door.Entity.GetComponent<StateMachine>();
                if (sm?.States != null)
                {
                    foreach (var state in sm.States)
                    {
                        if (state.Name == "open_door")
                        {
                            door.IsClosed = state.Value == 0;
                            break;
                        }
                    }
                }
            }
        }

        // Update chest states
        foreach (var chest in _chests.Values)
        {
            if (!chest.Entity.IsValid) continue;
            var chestComp = chest.Entity.GetComponent<Chest>();
            if (chestComp != null)
                chest.IsOpened = chestComp.IsOpened;
        }

        // Mark initial load complete after first tick so we can detect reinforcements
        if (!_initialLoadComplete)
            _initialLoadComplete = true;

        // Detect escape phase via reinforcement spawns (not escape route entity presence)
        if (_currentPhase == HeistPhase.Infiltration && _reinforcementDetected)
        {
            _currentPhase = HeistPhase.Escape;
            _currentRoute = null; // Force recompute
        }

        // Update path node positions
        if (_pathStart?.Entity.IsValid == true)
            _pathStart.GridPos = _pathStart.Entity.GridPosNum;
        foreach (var node in _pathNodes.Values)
            if (node.Entity.IsValid) node.GridPos = node.Entity.GridPosNum;

        // Update endpoint positions and associate with nearby doors
        var entranceRef = _hasEntrance ? _entranceGridPos : Vector2.Zero;
        float farthestEndpointDist = 0;
        HeistPathNodeData farthestEndpoint = null;

        foreach (var ep in _pathEndpoints.Values)
        {
            if (!ep.Entity.IsValid) continue;
            ep.GridPos = ep.Entity.GridPosNum;

            // Find nearest door within 30 grid units for job label
            if (ep.NearbyDoorJob == null)
            {
                float bestDoorDist = 30f;
                foreach (var door in _doors.Values)
                {
                    var d = Vector2.Distance(ep.GridPos, door.GridPos);
                    if (d < bestDoorDist)
                    {
                        bestDoorDist = d;
                        ep.NearbyDoorJob = door.RequiredJob;
                    }
                }
            }

            // Track farthest endpoint from entrance
            if (_hasEntrance)
            {
                var dist = Vector2.Distance(ep.GridPos, entranceRef);
                if (dist > farthestEndpointDist)
                {
                    farthestEndpointDist = dist;
                    farthestEndpoint = ep;
                }
            }
        }

        // Mark the objective endpoint (farthest from entrance)
        foreach (var ep in _pathEndpoints.Values)
            ep.IsObjective = ep == farthestEndpoint;

        // Rebuild graph if dirty (throttled)
        if (_graphDirty && _graphThrottle.ElapsedMilliseconds > 500)
        {
            RebuildGraph();
            _graphDirty = false;
            _graphThrottle.Restart();
            _currentRoute = null; // Force recompute
        }

        // Recompute route if needed
        var player = GameController.Player;
        if (player != null && _graph.Nodes.Count > 1)
        {
            var playerGridPos = player.GridPosNum;
            var moved = Vector2.Distance(playerGridPos, _lastRoutePlayerPos) > 30;

            if (_currentRoute == null || moved)
            {
                ComputeRoute(playerGridPos);
                _lastRoutePlayerPos = playerGridPos;
            }
        }

        // Update minimap state
        var ingameUi = GameController.IngameState.IngameUi;
        var smallMiniMap = ingameUi.Map.SmallMiniMap;
        if (smallMiniMap.IsValid && smallMiniMap.IsVisibleLocal)
        {
            var mapRect = smallMiniMap.GetClientRectCache;
            _mapCenter = mapRect.Center.ToVector2Num();
            _largeMap = false;
            _mapScale = smallMiniMap.MapScale;
        }
        else if (ingameUi.Map.LargeMap.IsVisibleLocal)
        {
            var largeMapWindow = ingameUi.Map.LargeMap;
            _mapCenter = largeMapWindow.MapCenter;
            _largeMap = true;
            _mapScale = largeMapWindow.MapScale;
        }
        else
        {
            _largeMap = null;
        }

        return null;
    }

    private void RebuildGraph()
    {
        var allNodes = new List<HeistPathNodeData>();
        if (_pathStart != null) allNodes.Add(_pathStart);
        allNodes.AddRange(_pathNodes.Values);
        allNodes.AddRange(_pathEndpoints.Values);

        _graph.Build(
            allNodes,
            _doors.Values,
            Settings.General.GraphConnectionDistance.Value,
            6,
            _heightData);
    }

    private void ComputeRoute(Vector2 playerGridPos)
    {
        var source = _graph.FindNearest(playerGridPos);
        if (source == null) return;

        var dijkstra = _graph.Dijkstra(source, Settings.Doors.DoorWeightMultiplier.Value);

        if (_currentPhase == HeistPhase.Escape)
        {
            // Route toward entrance: escape route entity or start node
            GraphNode bestTarget = null;
            float bestDist = float.MaxValue;

            if (_escapeRoute?.IsValid == true)
            {
                var escapeNode = _graph.FindNearest(_escapeRoute.GridPosNum);
                if (escapeNode != null && dijkstra.ContainsKey(escapeNode))
                {
                    bestTarget = escapeNode;
                    bestDist = dijkstra[escapeNode].Dist;
                }
            }

            foreach (var node in _graph.Nodes)
            {
                if (node.Data.NodeType != HeistNodeType.Start) continue;
                if (dijkstra.TryGetValue(node, out var info) && info.Dist < bestDist)
                {
                    bestTarget = node;
                    bestDist = info.Dist;
                }
            }

            _currentRoute = bestTarget != null ? _graph.ReconstructPath(dijkstra, bestTarget) : null;
        }
        else
        {
            // Infiltration: route AWAY from entrance, toward the deepest part of the heist
            // 1) Find endpoints that are far from entrance (real reward rooms)
            // 2) If none, route to the frontier (farthest reachable node from entrance)

            var entranceRef = _hasEntrance ? _entranceGridPos : playerGridPos;

            // Look for endpoints far from entrance (>80 grid units)
            GraphNode bestEndpoint = null;
            float bestEndpointDistFromEntrance = 0;

            foreach (var node in _graph.Nodes)
            {
                if (node.Data.NodeType != HeistNodeType.Endpoint) continue;
                if (node.Data.IsVisited) continue;
                if (!dijkstra.ContainsKey(node)) continue;

                var distFromEntrance = Vector2.Distance(node.Data.GridPos, entranceRef);
                if (distFromEntrance > 80 && distFromEntrance > bestEndpointDistFromEntrance)
                {
                    bestEndpointDistFromEntrance = distFromEntrance;
                    bestEndpoint = node;
                }
            }

            if (bestEndpoint != null)
            {
                _currentRoute = _graph.ReconstructPath(dijkstra, bestEndpoint);
            }
            else
            {
                // No distant endpoints found - route to the frontier
                // Pick the reachable node farthest from entrance
                GraphNode frontier = null;
                float frontierDist = 0;

                foreach (var node in _graph.Nodes)
                {
                    if (!dijkstra.ContainsKey(node)) continue;
                    var distFromEntrance = Vector2.Distance(node.Data.GridPos, entranceRef);
                    if (distFromEntrance > frontierDist)
                    {
                        frontierDist = distFromEntrance;
                        frontier = node;
                    }
                }

                _currentRoute = frontier != null ? _graph.ReconstructPath(dijkstra, frontier) : null;
            }
        }
    }

    public override void Render()
    {
        if (!GameController.InGame || !_isHeistZone) return;

        var player = GameController.Player;
        if (player == null) return;

        var playerRender = player.GetComponent<Render>();
        if (playerRender == null) return;

        var playerGridPos = playerRender.PosNum.WorldToGrid();
        var playerWorldPos = playerRender.PosNum;
        var maxDist = Settings.General.MaxDrawDistance.Value;

        // Escape phase with Show Escape Route off: the route, arrow and EXIT marker toward the entrance are not drawn
        // (the alarm flash keeps its own toggle).
        var escapeHidden = _currentPhase == HeistPhase.Escape && !Settings.Escape.ShowEscapeRoute.Value;

        // Draw route path
        if (_currentRoute is { Count: > 1 } && !escapeHidden)
        {
            var pathColor = _currentPhase == HeistPhase.Escape
                ? Settings.Escape.EscapePathColor.Value
                : Settings.Route.WorldPathColor.Value;

            // World path
            if (Settings.Route.ShowWorldPath)
            {
                DrawWorldPath(_currentRoute, pathColor,
                    Settings.Route.WorldPathThickness.Value,
                    Settings.Route.DrawEveryNthSegment.Value,
                    playerWorldPos);
            }

            // Minimap path
            if (Settings.Route.ShowMinimapPath && _largeMap != null)
            {
                DrawMinimapRoute(_currentRoute, playerGridPos, playerRender, pathColor);
            }

            // Direction arrow
            if (Settings.Route.ShowDirectionArrow && _currentRoute.Count > 1)
            {
                DrawDirectionArrow(playerWorldPos, playerGridPos);
            }
        }

        // Draw doors
        if (Settings.Doors.ShowDoors)
        {
            foreach (var door in _doors.Values)
            {
                if (!door.Entity.IsValid) continue;
                var gridDist = Vector2.Distance(playerGridPos, door.GridPos);
                if (gridDist > maxDist) continue;

                var worldPos = door.Entity.PosNum;
                var color = door.IsClosed
                    ? Settings.Doors.ClosedDoorColor.Value
                    : Settings.Doors.OpenDoorColor.Value;

                Graphics.DrawCircleInWorld(worldPos, 40, color, 2, 16, true);

                if (Settings.Doors.ShowJobLabel && door.IsClosed)
                {
                    var screenPos = Camera.WorldToScreen(worldPos);
                    if (IsOnScreen(screenPos))
                    {
                        Graphics.DrawText(door.RequiredJob,
                            screenPos - new Vector2(0, 25), color, FontAlign.Center);
                    }
                }
            }
        }

        // Draw endpoints with door job labels
        if (Settings.Endpoints.ShowEndpoints)
        {
            foreach (var ep in _pathEndpoints.Values)
            {
                if (!ep.Entity.IsValid) continue;
                var gridDist = Vector2.Distance(playerGridPos, ep.GridPos);
                if (gridDist > maxDist) continue;

                var worldPos = ep.Entity.PosNum;
                var color = ep.IsObjective
                    ? Settings.Endpoints.EndpointColor.Value
                    : new Color(
                        (int)(Settings.Endpoints.EndpointColor.Value.R * 0.5f),
                        (int)(Settings.Endpoints.EndpointColor.Value.G * 0.5f),
                        (int)(Settings.Endpoints.EndpointColor.Value.B * 0.5f),
                        Settings.Endpoints.EndpointColor.Value.A);

                var circleSize = ep.IsObjective ? 60 : 40;
                Graphics.DrawCircleInWorld(worldPos, circleSize, color, 2, 24, true);

                if (Settings.Endpoints.ShowRewardLabel)
                {
                    var screenPos = Camera.WorldToScreen(worldPos);
                    if (IsOnScreen(screenPos))
                    {
                        var label = ep.IsObjective
                            ? "Objective"
                            : ep.NearbyDoorJob ?? "";
                        if (!string.IsNullOrEmpty(label))
                        {
                            Graphics.DrawText(label,
                                screenPos - new Vector2(0, 30), color, FontAlign.Center);
                        }
                    }
                }

                if (Settings.Endpoints.ShowOnMinimap && _largeMap != null)
                {
                    DrawMinimapDot(ep.GridPos, playerGridPos, playerRender,
                        Settings.Endpoints.MinimapIconSize.Value, color);
                }
            }
        }

        // Draw escape route entity marker
        if (_currentPhase == HeistPhase.Escape && !escapeHidden && _escapeRoute?.IsValid == true)
        {
            var escapeWorldPos = _escapeRoute.PosNum;
            var escapeColor = Settings.Escape.EscapePathColor.Value;
            Graphics.DrawCircleInWorld(escapeWorldPos, 80, escapeColor, 3, 24, true);

            var escapeScreenPos = Camera.WorldToScreen(escapeWorldPos);
            if (IsOnScreen(escapeScreenPos))
            {
                Graphics.DrawText("EXIT",
                    escapeScreenPos - new Vector2(0, 35), escapeColor, FontAlign.Center);
            }
        }

        // Flash on alarm max
        if (_currentPhase == HeistPhase.Escape && Settings.Escape.FlashOnAlarmMax)
        {
            float pulse = (float)(0.15 + 0.1 * Math.Sin(Environment.TickCount64 / 200.0));
            var flashColor = new Color(255, 50, 0, (int)(pulse * 255));
            var screenSize = new Vector2(Camera.Width, Camera.Height);
            Graphics.DrawFrame(Vector2.Zero, screenSize, flashColor, 4);
        }
    }

    private void DrawWorldPath(List<GraphNode> route, Color pathColor, int thickness, int nth, Vector3 playerWorldPos)
    {
        var p0 = Camera.WorldToScreen(playerWorldPos);

        for (int i = 0; i < route.Count; i++)
        {
            var nodeGridPos = route[i].Data.GridPos;

            float height = 0;
            int gx = (int)nodeGridPos.X;
            int gy = (int)nodeGridPos.Y;
            if (_heightData != null && gy >= 0 && gx >= 0
                && gy < _heightData.Length && gx < _heightData[gy].Length)
                height = _heightData[gy][gx];

            var p1 = Camera.WorldToScreen(
                new Vector3(nodeGridPos.X * GridToWorldMultiplier,
                            nodeGridPos.Y * GridToWorldMultiplier, height));

            if ((i + 1) % nth == 0)
            {
                if (IsOnScreen(p0) || IsOnScreen(p1))
                    Graphics.DrawLine(p0, p1, thickness, pathColor);
                else if (i > 2)
                    break; // Stop drawing if we're off screen
            }
            p0 = p1;
        }
    }

    private void DrawMinimapRoute(List<GraphNode> route, Vector2 playerGridPos, Render playerRender, Color color)
    {
        var playerHeight = -playerRender.UnclampedHeight;

        for (int i = 0; i < route.Count; i++)
        {
            var nodeGridPos = route[i].Data.GridPos;

            float height = 0;
            int gx = (int)nodeGridPos.X;
            int gy = (int)nodeGridPos.Y;
            if (_heightData != null && gy >= 0 && gx >= 0
                && gy < _heightData.Length && gx < _heightData[gy].Length)
                height = _heightData[gy][gx];

            var delta = nodeGridPos - playerGridPos;
            var deltaZ = (playerHeight + height) * PoeMapExtension.WorldToGridConversion;
            var mapPos = _mapCenter + DeltaInWorldToMinimapDelta(delta, deltaZ);

            Graphics.DrawCircleFilled(mapPos, 3, color, 6);
        }
    }

    private void DrawDirectionArrow(Vector3 playerWorldPos, Vector2 playerGridPos)
    {
        if (_currentRoute == null || _currentRoute.Count < 2) return;

        // Find next unvisited waypoint on route (skip first which is nearest to player)
        GraphNode target = null;
        for (int i = 1; i < _currentRoute.Count; i++)
        {
            if (!_currentRoute[i].Data.IsVisited)
            {
                target = _currentRoute[i];
                break;
            }
        }
        target ??= _currentRoute[^1];

        var targetGridPos = target.Data.GridPos;
        var playerScreenPos = Camera.WorldToScreen(playerWorldPos);

        float targetHeight = 0;
        int gx = (int)targetGridPos.X;
        int gy = (int)targetGridPos.Y;
        if (_heightData != null && gy >= 0 && gx >= 0
            && gy < _heightData.Length && gx < _heightData[gy].Length)
            targetHeight = _heightData[gy][gx];

        var targetWorldPos = new Vector3(
            targetGridPos.X * GridToWorldMultiplier,
            targetGridPos.Y * GridToWorldMultiplier,
            targetHeight);
        var targetScreenPos = Camera.WorldToScreen(targetWorldPos);

        var screenSize = new Vector2(Camera.Width, Camera.Height);
        var arrowSize = Settings.Route.ArrowSize.Value;
        var arrowDist = Settings.Route.ArrowDistance.Value;
        var arrowColor = _currentPhase == HeistPhase.Escape
            ? Settings.Escape.EscapePathColor.Value
            : Settings.Route.WorldPathColor.Value;

        Vector2 direction;
        Vector2 arrowCenter;

        if (IsOnScreen(targetScreenPos))
        {
            direction = Vector2.Normalize(targetScreenPos - playerScreenPos);
            arrowCenter = playerScreenPos + direction * arrowDist;
        }
        else
        {
            var dx = targetGridPos.X - playerGridPos.X;
            var dy = targetGridPos.Y - playerGridPos.Y;
            var isoX = dx - dy;
            var isoY = -(dx + dy);
            direction = Vector2.Normalize(new Vector2(isoX, isoY));

            var center = screenSize / 2;
            arrowCenter = center + direction * arrowDist;

            var margin = arrowSize + 10f;
            arrowCenter = Vector2.Clamp(arrowCenter,
                new Vector2(margin, margin),
                screenSize - new Vector2(margin, margin));
        }

        var perpendicular = new Vector2(-direction.Y, direction.X);
        var tip = arrowCenter + direction * arrowSize;
        var baseLeft = arrowCenter - direction * (arrowSize * 0.5f) + perpendicular * (arrowSize * 0.33f);
        var baseRight = arrowCenter - direction * (arrowSize * 0.5f) - perpendicular * (arrowSize * 0.33f);

        Graphics.DrawConvexPolyFilled(new[] { tip, baseLeft, baseRight }, arrowColor);

        var dist = Vector2.Distance(playerGridPos, targetGridPos);
        Graphics.DrawText($"{(int)dist}", arrowCenter + direction * (arrowSize + 10),
            arrowColor, FontAlign.Center);
    }

    private void DrawMinimapDot(Vector2 entityGridPos, Vector2 playerGridPos,
        Render playerRender, int size, Color color)
    {
        var playerHeight = -playerRender.UnclampedHeight;

        float height = 0;
        int gx = (int)entityGridPos.X;
        int gy = (int)entityGridPos.Y;
        if (_heightData != null && gy >= 0 && gx >= 0
            && gy < _heightData.Length && gx < _heightData[gy].Length)
            height = _heightData[gy][gx];

        var delta = entityGridPos - playerGridPos;
        var deltaZ = (playerHeight + height) * PoeMapExtension.WorldToGridConversion;
        var mapPos = _mapCenter + DeltaInWorldToMinimapDelta(delta, deltaZ);

        var halfSize = size / 2f;

        if (_largeMap == false)
        {
            var ingameUi = GameController.IngameState.IngameUi;
            var mapRect = ingameUi.Map.SmallMiniMap.GetClientRectCache;
            var drawRect = new SharpDX.RectangleF(mapPos.X - halfSize, mapPos.Y - halfSize, size, size);
            if (!mapRect.Contains(drawRect))
                return;
        }

        Graphics.DrawCircleFilled(mapPos, halfSize, color, 8);
    }

    private Vector2 DeltaInWorldToMinimapDelta(Vector2 delta, float deltaZ)
    {
        return _mapScale * Vector2.Multiply(
            new Vector2(delta.X - delta.Y, deltaZ - (delta.X + delta.Y)),
            new Vector2(CameraAngleCos, CameraAngleSin));
    }

    private bool IsOnScreen(Vector2 pos)
    {
        return pos.X > 0 && pos.Y > 0 && pos.X < Camera.Width && pos.Y < Camera.Height;
    }

    public override void DrawSettings()
    {
        _settingsUi ??= new HeistSettingsUi();
        _settingsUi.Draw(Settings, new HeistUiState(
            (_pathStart != null ? 1 : 0) + _pathNodes.Count + _pathEndpoints.Count,
            _pathEndpoints.Count,
            _doors.Count,
            _doors.Count(kv => kv.Value.IsClosed),
            _chests.Count(kv => !kv.Value.IsOpened && kv.Value.Entity.IsValid),
            _currentPhase,
            _graph.Nodes.Count > 1,
            _currentRoute != null
        ));
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace WhatsAHeist;

public class GraphEdge
{
    public GraphNode Target { get; }
    public float Distance { get; }
    public bool HasDoor { get; }
    public HeistDoorData Door { get; }

    public GraphEdge(GraphNode target, float distance, bool hasDoor = false, HeistDoorData door = null)
    {
        Target = target;
        Distance = distance;
        HasDoor = hasDoor;
        Door = door;
    }
}

public class GraphNode
{
    public HeistPathNodeData Data { get; }
    public List<GraphEdge> Edges { get; } = new();

    public GraphNode(HeistPathNodeData data)
    {
        Data = data;
    }
}

public sealed class HeistGraph
{
    private readonly List<GraphNode> _nodes = new();
    private readonly Dictionary<uint, GraphNode> _entityToNode = new();

    public IReadOnlyList<GraphNode> Nodes => _nodes;

    public void Build(
        IEnumerable<HeistPathNodeData> pathNodes,
        IEnumerable<HeistDoorData> doors,
        float maxConnectionDistance,
        int maxEdgesPerNode,
        float[][] heightData)
    {
        _nodes.Clear();
        _entityToNode.Clear();

        var doorList = doors.ToList();

        foreach (var nodeData in pathNodes)
        {
            var node = new GraphNode(nodeData);
            _nodes.Add(node);
            if (nodeData.Entity.IsValid)
                _entityToNode[nodeData.Entity.Id] = node;
        }

        if (_nodes.Count < 2) return;

        // Connect nodes by proximity
        for (int i = 0; i < _nodes.Count; i++)
        {
            var nodeA = _nodes[i];
            var candidates = new List<(GraphNode node, float dist)>();

            for (int j = 0; j < _nodes.Count; j++)
            {
                if (i == j) continue;
                var nodeB = _nodes[j];
                var dist = Vector2.Distance(nodeA.Data.GridPos, nodeB.Data.GridPos);
                if (dist <= maxConnectionDistance)
                    candidates.Add((nodeB, dist));
            }

            // Take K-nearest
            candidates.Sort((a, b) => a.dist.CompareTo(b.dist));
            var taken = 0;

            foreach (var (candidate, dist) in candidates)
            {
                if (taken >= maxEdgesPerNode) break;

                // Already connected in the other direction?
                if (nodeA.Edges.Any(e => e.Target == candidate)) continue;

                // Check for terrain walkability by sampling points along the edge
                bool blocked = false;
                bool hasDoor = false;
                HeistDoorData nearDoor = null;

                if (heightData != null)
                {
                    var posA = nodeA.Data.GridPos;
                    var posB = candidate.Data.GridPos;
                    int blockedSamples = 0;

                    for (int s = 1; s <= 10; s++)
                    {
                        float t = s / 11f;
                        var sample = Vector2.Lerp(posA, posB, t);
                        int sx = (int)sample.X;
                        int sy = (int)sample.Y;

                        if (sy >= 0 && sy < heightData.Length && sx >= 0 && sx < heightData[sy].Length)
                        {
                            // Height of -9999 or very low indicates unwalkable terrain
                            if (heightData[sy][sx] < -9000)
                                blockedSamples++;
                        }
                    }

                    if (blockedSamples > 3)
                    {
                        // Check if a door is near the midpoint
                        var midpoint = (posA + posB) * 0.5f;
                        foreach (var door in doorList)
                        {
                            if (Vector2.Distance(midpoint, door.GridPos) < 15f)
                            {
                                hasDoor = true;
                                nearDoor = door;
                                break;
                            }
                        }

                        if (!hasDoor)
                        {
                            blocked = true;
                        }
                    }
                }

                if (!blocked)
                {
                    var edge = new GraphEdge(candidate, dist, hasDoor, nearDoor);
                    nodeA.Edges.Add(edge);
                    // Bidirectional
                    candidate.Edges.Add(new GraphEdge(nodeA, dist, hasDoor, nearDoor));
                    taken++;
                }
            }
        }

        // Retry with increased threshold for orphaned nodes
        var orphans = _nodes.Where(n => n.Edges.Count == 0).ToList();
        if (orphans.Count > 0)
        {
            float extendedDist = maxConnectionDistance * 1.5f;
            foreach (var orphan in orphans)
            {
                GraphNode closest = null;
                float closestDist = float.MaxValue;

                foreach (var other in _nodes)
                {
                    if (other == orphan) continue;
                    if (other.Edges.Count == 0) continue; // Don't connect orphan to orphan
                    var dist = Vector2.Distance(orphan.Data.GridPos, other.Data.GridPos);
                    if (dist < extendedDist && dist < closestDist)
                    {
                        closestDist = dist;
                        closest = other;
                    }
                }

                if (closest != null)
                {
                    orphan.Edges.Add(new GraphEdge(closest, closestDist));
                    closest.Edges.Add(new GraphEdge(orphan, closestDist));
                }
            }
        }
    }

    /// <summary>
    /// Dijkstra from a source node to all reachable nodes.
    /// Returns dictionary of node -> (predecessor, total distance).
    /// </summary>
    public Dictionary<GraphNode, (GraphNode Prev, float Dist)> Dijkstra(
        GraphNode source, float doorWeightMultiplier)
    {
        var dist = new Dictionary<GraphNode, float>();
        var prev = new Dictionary<GraphNode, GraphNode>();
        var visited = new HashSet<GraphNode>();
        // Simple priority queue via sorted set (graph is tiny)
        var queue = new SortedSet<(float dist, int id, GraphNode node)>();
        int counter = 0;

        foreach (var node in _nodes)
            dist[node] = float.MaxValue;

        dist[source] = 0;
        queue.Add((0, counter++, source));

        while (queue.Count > 0)
        {
            var (d, _, current) = queue.Min;
            queue.Remove(queue.Min);

            if (!visited.Add(current)) continue;

            foreach (var edge in current.Edges)
            {
                if (visited.Contains(edge.Target)) continue;

                float weight = edge.Distance;
                if (edge.HasDoor && edge.Door is { IsClosed: true })
                    weight *= doorWeightMultiplier;

                float newDist = d + weight;
                if (newDist < dist[edge.Target])
                {
                    dist[edge.Target] = newDist;
                    prev[edge.Target] = current;
                    queue.Add((newDist, counter++, edge.Target));
                }
            }
        }

        var result = new Dictionary<GraphNode, (GraphNode, float)>();
        foreach (var node in _nodes)
        {
            if (dist[node] < float.MaxValue)
                result[node] = (prev.GetValueOrDefault(node), dist[node]);
        }
        return result;
    }

    /// <summary>
    /// Reconstruct path from Dijkstra results.
    /// </summary>
    public List<GraphNode> ReconstructPath(
        Dictionary<GraphNode, (GraphNode Prev, float Dist)> dijkstra,
        GraphNode target)
    {
        if (!dijkstra.ContainsKey(target)) return null;

        var path = new List<GraphNode>();
        var current = target;
        while (current != null)
        {
            path.Add(current);
            current = dijkstra.TryGetValue(current, out var info) ? info.Prev : null;
        }
        path.Reverse();
        return path;
    }

    /// <summary>
    /// Find the nearest graph node to a grid position.
    /// </summary>
    public GraphNode FindNearest(Vector2 gridPos)
    {
        GraphNode best = null;
        float bestDist = float.MaxValue;

        foreach (var node in _nodes)
        {
            var dist = Vector2.Distance(gridPos, node.Data.GridPos);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = node;
            }
        }

        return best;
    }
}

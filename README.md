# What's a Heist?

> **Work in Progress** -- This plugin is an early exploration of what game data is available during Heist contracts. It is not fully functional and not particularly useful yet.

Part of the **WhatsA** plugin family for ExileApi.

## Current State

This plugin is primarily a research tool for understanding the Heist entity model -- path nodes, endpoints, doors, and reward chests. The routing and overlay features exist as proof-of-concept and will need significant iteration before they are reliable for actual gameplay.

## What It Attempts

- Build a navigation graph from HeistPathNode entities and find routes via Dijkstra pathfinding
- Identify the heist objective (farthest endpoint from entrance) and route toward it during infiltration
- Detect escape phase via reinforcement spawns and reroute toward the entrance
- Show door NPC job requirements and endpoint labels
- Draw route lines in-world and on the minimap

## Known Limitations

- Route accuracy depends heavily on which entities the game exposes at any given moment
- Graph connectivity can be incomplete, leading to missing or broken routes
- Escape phase detection is heuristic-based and may trigger incorrectly
- Objective identification assumes the farthest endpoint is the target, which is not always correct

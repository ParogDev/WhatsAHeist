# CLAUDE.md -- Whats A Heist

## Plugin Context

WhatsAHeist is a routing overlay plugin for the Heist league mechanic in Path of Exile. It tracks HeistPathStart, HeistPathNode, and HeistPathEndpoint terrain entities, connects them into a navigation graph using proximity-based edge construction with terrain walkability validation, and runs Dijkstra pathfinding to show optimal routes to reward rooms (infiltration phase) or back to the exit (escape phase). It also tracks NPC skill doors, reward chests, and the escape route portal, drawing world overlays, minimap markers, and a direction arrow.

**Main class**: `WhatsAHeist` inherits `BaseSettingsPlugin<WhatsAHeistSettings>`
**Settings class**: `WhatsAHeistSettings` implements `ISettings`
**Key classes**: HeistPathNodeData, HeistDoorData, HeistChestData, HeistGraph, GraphNode, GraphEdge, HeistSettingsUi, HeistUiState (record), HeistPhase (enum), HeistNodeType (enum)

### Architecture

Entity tracking via `EntityAdded()` filters by metadata path for heist-specific terrain objects (HeistPathStart/Node/Endpoint), NPC doors (Door_NPC), area transitions named "Escape Route", and heist chests. `Tick()` updates door states (via Targetable or StateMachine), chest open states (via Chest component), detects escape phase (when escape route entity appears), rebuilds the navigation graph when nodes change (throttled to 500ms), and recomputes the Dijkstra route when the graph changes or the player moves 30+ grid units. `Render()` draws world path lines between route nodes, minimap route dots, a direction arrow toward the next waypoint, colored door indicators with NPC job labels, gold endpoint markers, and an escape route exit marker with screen-border flash effect. `DrawSettings()` delegates to `HeistSettingsUi` for a custom tabbed ImGui panel with dark teal accent.

### Lifecycle Methods Used

| Method | Purpose |
|---|---|
| `Initialise` | Returns true (no special setup needed) |
| `AreaChange` | Clears all entity dictionaries, resets graph/route/phase, reloads terrain height data |
| `EntityAdded` | Filters entities by path and populates path node/door/chest/escape route tracking |
| `Tick` | Updates door/chest state, detects escape phase, rebuilds graph, recomputes route, caches minimap state |
| `Render` | Draws world path, minimap path, direction arrow, door/endpoint markers, escape flash |
| `DrawSettings` | Custom tabbed settings UI via HeistSettingsUi |

### Current Settings

> **Note**: This settings table is a snapshot from generation time. If settings are added/removed later, re-run `/setup-plugin-project` to regenerate, or update CLAUDE.md manually.

| Setting | Type | Default | Parent |
|---|---|---|---|
| Enable | ToggleNode | true | root |
| Route.ShowWorldPath | ToggleNode | true | RouteSettings |
| Route.ShowMinimapPath | ToggleNode | true | RouteSettings |
| Route.WorldPathColor | ColorNode | Cyan (0,255,255) | RouteSettings |
| Route.WorldPathThickness | RangeNode\<int\> | 3 (1-5) | RouteSettings |
| Route.DrawEveryNthSegment | RangeNode\<int\> | 2 (1-10) | RouteSettings |
| Route.ShowDirectionArrow | ToggleNode | true | RouteSettings |
| Route.ArrowSize | RangeNode\<int\> | 40 (20-80) | RouteSettings |
| Route.ArrowDistance | RangeNode\<int\> | 120 (60-300) | RouteSettings |
| Doors.ShowDoors | ToggleNode | true | DoorSettings |
| Doors.ClosedDoorColor | ColorNode | Red (255,60,60) | DoorSettings |
| Doors.OpenDoorColor | ColorNode | Green (60,255,60) | DoorSettings |
| Doors.ShowJobLabel | ToggleNode | true | DoorSettings |
| Doors.DoorWeightMultiplier | RangeNode\<float\> | 2.0 (1.0-5.0) | DoorSettings |
| Endpoints.ShowEndpoints | ToggleNode | true | EndpointSettings |
| Endpoints.EndpointColor | ColorNode | Gold (255,215,0) | EndpointSettings |
| Endpoints.ShowRewardLabel | ToggleNode | true | EndpointSettings |
| Endpoints.ShowOnMinimap | ToggleNode | true | EndpointSettings |
| Endpoints.MinimapIconSize | RangeNode\<int\> | 15 (5-30) | EndpointSettings |
| Escape.ShowEscapeRoute | ToggleNode | true | EscapeSettings |
| Escape.EscapePathColor | ColorNode | Orange-red (255,100,50) | EscapeSettings |
| Escape.FlashOnAlarmMax | ToggleNode | true | EscapeSettings |
| General.MaxDrawDistance | RangeNode\<int\> | 200 (50-500) | HeistGeneralSettings |
| General.GraphConnectionDistance | RangeNode\<int\> | 150 (50-300) | HeistGeneralSettings |

## Project Setup

- This is an ExileApi plugin (game HUD overlay framework for Path of Exile)
- Do not edit anything outside this directory
- Target framework: net10.0-windows, OutputType: Library

### Namespace -> DLL Mapping
| DLL | Key Namespaces |
|---|---|
| `ExileCore.dll` | `ExileCore` (BaseSettingsPlugin, GameController, Graphics, Input), `ExileCore.Shared` (Nodes, Enums, Interfaces, Attributes, Helpers), `ExileCore.PoEMemory` (Components, MemoryObjects) |
| `GameOffsets.dll` | `GameOffsets` (offsets structs), `GameOffsets.Native` (Vector2i, NativeStringU) |

## Build & Run

- NO manual build command -- Loader.exe auto-compiles from Plugins/Source/
- HUD installation path: resolved from .csproj HintPath (parent dir of ExileCore.dll)
- For IDE support set env var: `exapiPackage` = `<HUD installation path>`

## API Reference

- **Default**: HUD installation (from .csproj HintPath) -- compiled DLLs with intellisense
- **Enhanced**: If `.claude/override-path` exists, read it for a path to expanded
  API reference with full type definitions and source. Use that path for deep lookups
  when the compiled DLLs don't provide enough detail about a type, method, or pattern.

## Plugin Anatomy

Every plugin is a C# class library. The main class inherits `BaseSettingsPlugin<TSettings>` and the settings class implements `ISettings`.

Minimal `.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-windows</TargetFramework>
    <OutputType>Library</OutputType>
    <UseWindowsForms>true</UseWindowsForms>
    <PlatformTarget>x64</PlatformTarget>
    <LangVersion>latest</LangVersion>
    <DebugType>embedded</DebugType>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="ExileCore">
      <HintPath>$(exapiPackage)\ExileCore.dll</HintPath>
      <Private>False</Private>
    </Reference>
    <Reference Include="GameOffsets">
      <HintPath>$(exapiPackage)\GameOffsets.dll</HintPath>
      <Private>False</Private>
    </Reference>
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="ImGui.NET" Version="1.90.0.1" />
    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
    <PackageReference Include="SharpDX.Mathematics" Version="4.2.0" />
  </ItemGroup>
</Project>
```

### Plugin Lifecycle

| Method | When called | Notes |
|---|---|---|
| `Initialise()` | Once on load | Register hotkeys, wire up `OnPressed`/`OnValueChanged`, return `true` on success |
| `OnLoad()` | After Initialise | Load textures: `Graphics.InitImage("file.png")` |
| `AreaChange(AreaInstance area)` | Zone change | Clear cached entity lists here |
| `Tick()` | Every frame | Return `null` (no async job needed) or a `Job` for background work |
| `Render()` | Every frame | Draw overlays; check `Settings.Enable` and `GameController.InGame` |
| `EntityAdded(Entity entity)` | Entity enters range | Filter and cache relevant entities here |
| `EntityRemoved(Entity entity)` | Entity leaves range | Remove from caches |
| `DrawSettings()` | Settings panel open | Call `base.DrawSettings()` unless fully custom |

## GameController API

`GameController` is the main access point available in all plugin methods:

```csharp
// State checks
GameController.InGame
GameController.Game.IsInGameState

// Player
GameController.Player                          // local player Entity
GameController.Player.GetComponent<Positioned>().GridPosNum

// Area
GameController.Area.CurrentArea.IsPeaceful
GameController.Area.CurrentArea.Area.RawName
GameController.Area.CurrentArea.IsTown
GameController.Area.CurrentArea.IsHideout

// Entities -- prefer ValidEntitiesByType over Entities for filtered access
GameController.EntityListWrapper.ValidEntitiesByType[EntityType.Monster]
GameController.Entities   // all entities, more expensive

// Ingame UI / state
GameController.Game.IngameState.IngameUi.Map.LargeMap.IsVisible
GameController.Game.IngameState.IngameUi.FullscreenPanels
GameController.Game.IngameState.IngameUi.OpenRightPanel
GameController.Game.IngameState.Camera        // Camera, WorldToScreen
GameController.Game.IngameState.Data          // terrain, server data, area dimensions

// Game files (static data)
GameController.Files.BaseItemTypes.Translate(entity.Path)
GameController.Files.GemEffects.GetById(id)

// Window
GameController.Window.GetWindowRectangleTimeCache   // cached, use this not GetWindowRectangle()

// Inter-plugin
GameController.PluginBridge.SaveMethod("MyPlugin.Method", delegate)
GameController.PluginBridge.GetMethod<TDelegate>("OtherPlugin.Method")

// Timing
GameController.DeltaTime  // double, seconds since last frame
```

## Entity API

```csharp
entity.Path         // "Metadata/Monsters/..."
entity.Metadata     // same as Path for most entities
entity.Id           // uint, unique per session
entity.IsValid      // always check before extensive use
entity.IsAlive
entity.GridPosNum   // Vector2, 2D grid position
entity.PosNum       // Vector3, 3D world position
entity.Distance(otherEntity)
entity.DistancePlayer  // float, distance to local player
entity.Type         // EntityType enum
entity.GetComponent<Positioned>()     // null if not present
entity.GetComponent<Render>()
entity.GetComponent<Stats>()
entity.GetComponent<ObjectMagicProperties>()?.Mods
```

## Component Quick Reference

### Life
```csharp
var life = entity.GetComponent<Life>();
life.CurHP / life.MaxHP      // health
life.CurMana / life.MaxMana  // mana
life.CurES / life.MaxES      // energy shield
life.HPPercentage             // 0-100
```

### Buffs
```csharp
var buffs = entity.GetComponent<Buffs>();
buffs.BuffsList               // List<Buff>
buff.Name                     // internal string ID (e.g., "frozen", "shocked")
buff.DisplayName              // human-readable
buff.Timer                    // seconds remaining
buff.MaxTime                  // original duration
buff.Charges                  // stack count
```

### Positioned & Render
```csharp
var pos = entity.GetComponent<Positioned>();
pos.GridPosNum                // Vector2 grid coords

var render = entity.GetComponent<Render>();
render.Name                   // display name
render.Bounds                 // bounding box
```

### ObjectMagicProperties
```csharp
var omp = entity.GetComponent<ObjectMagicProperties>();
omp.Rarity                    // MonsterRarity enum
omp.Mods                      // List<string> mod names
```

### Stats
```csharp
var stats = entity.GetComponent<Stats>();
stats.StatDictionary          // Dictionary<GameStat, int>
```

### Other Common Components
- `Monster` -- monster-specific data
- `Player` -- player-specific data
- `Chest` -- chest state (IsOpened, IsStrongbox)
- `Portal` -- portal destination
- `WorldItem` -- ground item info
- `Targetable` -- whether entity can be targeted (isTargetable)
- `Base` -- item base type info (Name, ItemBaseName)
- `Mods` -- item mods (ItemMods, ImplicitMods, ItemRarity)
- `Sockets` -- socket links, colors, count
- `StateMachine` -- state machine states (used for door state detection)

## Settings Node Types

```csharp
public class MySettings : ISettings
{
    public ToggleNode Enable { get; set; } = new ToggleNode(false);
    public RangeNode<int> SomeRange { get; set; } = new RangeNode<int>(10, 1, 100);
    public RangeNode<float> SomeFloat { get; set; } = new RangeNode<float>(1.5f, 0f, 10f);
    public ColorNode SomeColor { get; set; } = new ColorNode(Color.White);
    public HotkeyNodeV2 SomeKey { get; set; } = Keys.F5;
    public ButtonNode SomeButton { get; set; }
    public TextNode SomeText { get; set; } = new TextNode("default");
    public EmptyNode SomeHeader { get; set; }

    [Menu("Display Name")]
    public ToggleNode WithLabel { get; set; } = new ToggleNode(true);

    [Submenu]
    public class NestedSection
    {
        public ToggleNode SubOption { get; set; } = new ToggleNode(false);
    }
}
```

## Graphics API

```csharp
// Rectangles
Graphics.DrawBox(topLeft, bottomRight, color, rounding);
Graphics.DrawFrame(topLeft, bottomRight, color, borderWidth, segments, rounding);

// Text
Graphics.DrawText("text", position, color);
Graphics.DrawTextWithBackground("text", position, textColor, alignment, bgColor);
var size = Graphics.MeasureText("text");
using (Graphics.SetTextScale(1.5f)) { /* draw at scale */ }

// World / map lines
Graphics.DrawLineInWorld(gridPos1, gridPos2, width, color);
Graphics.DrawLineOnLargeMap(gridPos1, gridPos2, width, color);

// Circles
Graphics.DrawFilledCircleInWorld(worldPos, radius, color);
Graphics.DrawCircleInWorld(worldPos, radius, color, thickness, segments, filled);

// Textures (load in OnLoad, draw in Render)
Graphics.InitImage("myimage.png");
Graphics.DrawImage("key", rectF, color);

// Camera projection
var screenPos = GameController.Game.IngameState.Camera.WorldToScreen(entity.PosNum);
```

## Performance Rules

ExileApi plugins run every frame (60+ fps). The game process is memory-mapped, so every property access that isn't cached may read from game memory.

- **NEVER** call `GetComponent<T>()` in `Render()` -- do it in `Tick()`, store in fields
- **NEVER** iterate `GameController.Entities` -- use `EntityListWrapper.ValidEntitiesByType`
- **NEVER** call `entity.Path` per-frame -- cache in `EntityAdded()` via `SetHudComponent`
- Use `EntityAdded`/`EntityRemoved` to maintain filtered entity lists
- Use `TimeCache<T>` for expensive operations that don't need per-frame freshness
- Clear state in `AreaChange()`, not per-tick
- Use cached accessors (`GetWindowRectangleTimeCache`, `GetClientRectCache`)
- Check `entity.IsValid` and `entity.IsAlive` before processing
- Check `screenPos == Vector2.Zero` after `WorldToScreen()`
- Load textures in `OnLoad()`, never in `Render()`
- Separate data reads (`Tick`) from drawing (`Render`) -- never GetComponent in Render
- Use `_canRender` flags to skip `Render()` entirely when nothing to draw
- Guard early in `Tick()`: InGame, IsAlive, area checks (town/hideout) before work

## C# Best Practices for Plugin Development

- Use file-scoped namespaces
- Seal leaf classes for virtual dispatch optimization
- Object-pool reusable instances to avoid per-frame GC pressure
- Use Dictionary/HashSet lookups instead of per-frame string comparisons
- Use switch expressions for tier/type mappings
- Use readonly fields for immutable configuration
- Any plugin with 4+ settings should use a dedicated `<PluginName>SettingsUi` class

## Coordinate Systems

- **Grid** (`GridPosNum`, `Vector2`): tile-based map coordinates. Used for minimap drawing.
- **World** (`PosNum`, `Vector3`): 3D world coordinates. Convert to screen with `Camera.WorldToScreen`.
- **Screen** (`Vector2`): pixel coordinates. Used for ImGui and `Graphics.DrawText/DrawFrame`.
- GridToWorld multiplier: `250f / 23f`

## Common Patterns

### Entity Tracking
```csharp
public override void EntityAdded(Entity entity)
{
    if (entity.Type == EntityType.Monster && entity.IsAlive)
        _trackedMonsters[entity.Id] = entity;
}

public override void EntityRemoved(Entity entity)
{
    _trackedMonsters.Remove(entity.Id);
}

public override void AreaChange(AreaInstance area)
{
    _trackedMonsters.Clear();
}
```

### Rate-Limited Updates
```csharp
private readonly TimeCache<List<MyData>> _cache;
_cache = new TimeCache<List<MyData>>(BuildData, 200); // refresh every 200ms

private readonly Stopwatch _timer = Stopwatch.StartNew();
if (_timer.ElapsedMilliseconds < 200) return null;
_timer.Restart();
```

### Inter-Plugin Communication
```csharp
GameController.PluginBridge.SaveMethod("MyPlugin.GetData", (Func<MyData>)GetData);
var getData = GameController.PluginBridge.GetMethod<Func<OtherData>>("OtherPlugin.GetData");
if (getData != null) { var data = getData(); }
```

### Custom ImGui Settings UI

The settings panel is drawn by `SettingsMenu.cs` (per-plugin copy of the scaffolding's `new-plugin/templates/SettingsMenu.template.cs`, `__HUD__` = ExileCore) from the `[Menu]` attributes: one tab per root `[Submenu]` (Route / Doors / Endpoints / Escape; General joins Route via `[MenuTab]`), `[MenuUnit]` slider units, `[Menu(..., index, parentIndex)]` nesting. `HeistSettingsUi` builds it (dark teal accent #1A936F) and adds what it can't know: the live status strip (`.Banner`), and per tab an explanation, live status lines, the door colour legend and the escape-phase banner (`.Tab(..., first: true)`).

### UI-First Feature Development

When adding a new feature, always design the settings UI experience in the same pass -- never leave bare controls without domain context.

**Required UI enrichment for each feature type:**

| What you're adding | What the SettingsUi needs |
|---|---|
| New entity type or data source | Live count in a status bar + status line in the relevant tab |
| New color setting | Legend item showing a colored dot + what that color means in context |
| Bridge/integration (e.g., Radar) | Connection status indicator (green/red dot + status text) |
| Visual feature (arrows, circles, paths) | Preview widget showing what the in-game overlay will look like |
| New mechanic | Info block explaining how the mechanic works for users unfamiliar with it |

**Live runtime data pattern** -- define a record to pass live state from `DrawSettings()`:
```csharp
public record HeistUiState(int NodeCount, int EndpointCount, int DoorCount, int ClosedDoorCount, int ChestCount, HeistPhase Phase, bool HasGraph, bool HasRoute);

// In DrawSettings():
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
```

## Reference Plugins

Study other plugins in `Plugins/Source/` for pattern reference. Look at:
- Simple buff-tracking plugins for entity/buff patterns
- Minimap overlay plugins for coordinate system usage
- Settings-heavy plugins for ImGui custom UI patterns
- WhatsACrowdControl / WhatsATincture for custom settings UI patterns

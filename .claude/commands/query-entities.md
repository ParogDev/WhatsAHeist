# Query Game Entities

Query live game state from ExileApi via the DevTree bridge expression.

## Prerequisites (one-time setup)

The user must have the DevTree bridge expression active:
1. Open DevTree in the overlay (NumPad9 or enable in settings)
2. Paste the bridge expression into DevTree's "Custom expression" input field
3. Check "Eval every frame"
4. The expression status should show "idle - waiting for request"

## How to Find the Bridge Files

Read this plugin's `.csproj` file and extract the HintPath directory for ExileCore.dll.
The bridge files are located at `<that_directory>/claude-bridge/`.

- Bridge expression: `<hud_root>/claude-bridge/bridge-expression.cs`
- Request file: `<hud_root>/claude-bridge/request.txt`
- Response file: `<hud_root>/claude-bridge/response.json`

If the HintPath uses `$(exapiPackage)`, the HUD root is the value of that environment variable.

## How to use this skill

You are a game-state query tool. The user wants you to inspect something in-game. Follow this protocol:

### Step 1: Resolve the bridge path

Read the `.csproj` in this plugin directory. Extract the HintPath for ExileCore.dll to determine the HUD root directory. The bridge files are at `<hud_root>/claude-bridge/`.

### Step 2: Write the request

Write a request file to `<hud_root>/claude-bridge/request.txt`.

The request content should be one of:
- `all` -- full dump: player + area + all nearby entities
- `player` -- player vitals, position, buffs
- `area` -- current area name, level, act
- `entities` -- all nearby entities (default 200 range)
- `entities:500` -- entities within custom range
- `monsters` -- only alive hostile monsters
- `items` -- only world items (drops on ground)

Choose the most focused query for what you need. Use `all` only when you genuinely need everything.

### Step 3: Tell the user to wait

Say something like: "Grabbing game state -- hold still for a moment."

If you're uncertain whether the bridge is active, ask: "Is the DevTree bridge expression running? You should see 'idle - waiting for request' in the DevTree Custom expression output."

### Step 4: Poll for the response

Read `<hud_root>/claude-bridge/response.json`. If it doesn't exist yet, wait briefly and try again. The bridge writes the response within one frame (~16ms), so if it's not there after a couple seconds, the bridge may not be active.

### Step 5: Tell the user they can continue

Once you've read the response, say: "Got it -- you can keep playing."

### Step 6: Analyze and respond

Parse the JSON response and answer the user's question. The response format is:

```json
{
  "player": { "path": "...", "hp": N, "maxHp": N, "es": N, "maxEs": N, "mana": N, "maxMana": N, "pos": [X, Y], "buffs": [...] },
  "area": { "name": "...", "level": N, "act": N },
  "entities": [
    { "id": N, "type": "Monster", "path": "Metadata/...", "name": "...", "alive": true, "hostile": true, "rarity": "Rare", "dist": 45.2, "pos": [X, Y], "hp": N, "maxHp": N }
  ],
  "timestamp": "2026-03-12T..."
}
```

Entity `path` is the metadata path (e.g., `Metadata/Monsters/AtlasExiles/AtlasExile1`). Use this to identify monster types. The `name` field is the display name from the Render component.

## Important notes

- The bridge expression runs in DevTree's Render loop -- DevTree must be enabled and rendering
- Queries are nearly instant (one frame), but the user should hold still to avoid entity state changing mid-capture
- Entity data is a point-in-time snapshot -- positions, HP, and buffs may have changed by the time you analyze
- If the response file is missing or stale, the bridge may not be active -- guide the user through setup
- Never send synthetic inputs -- all interaction is file-based

$ARGUMENTS

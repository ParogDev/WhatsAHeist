# PoEDB Datamined Data Lookup

Look up datamined Path of Exile internal data from **poedb.tw** -- modifier tables with tiers/weights, buff definitions with internal IDs, skill level progressions, item mod pools, monster stats, and more. This is the complement to `/poe-wiki`: where poewiki.net gives prose explanations, PoEDB gives raw datamined numbers that map directly to ExileApi identifiers.

## When to Use

Use this skill when you need **internal game data** such as:
- **Modifier tables**: tier numbers, stat ranges, spawn weights, tags, domains
- **Buff definitions**: internal IDs (`ignited`, `chilled`), BuffGroupsID, icon paths
- **Skill gem data**: level progression tables, stat values per level, support gem compatibility
- **Item mod pools**: which mods can roll on a base type, with weights
- **Monster stats**: damage%, life%, spectre eligibility
- **Unique items**: exact mod ranges, item class, base type

Use `/poe-wiki` instead when you need prose explanations of how mechanics work.

## Operating Modes

This skill supports three modes. Detect which mode to use from the user's input:

### Mode 1: Single Lookup (default)
User provides one term. Fetch the page, detect its type, extract all data, map to ExileApi identifiers.

### Mode 2: Batch Lookup
User provides multiple terms, or you're analyzing an entity with many mods/buffs/skills. Launch **parallel Agent sub-agents** (one per term or small groups) to fetch and extract concurrently, then compile results.

### Mode 3: Deep Extract
When a page is very data-dense (e.g., modifier tables with hundreds of entries), perform **two-phase fetching**: a structural probe first, then targeted extraction of specific sections. Also follow links to sub-pages for more detail.

---

## URL Construction

### Direct Pages
```
https://poedb.tw/us/<Name>
```
- Replace spaces with underscores: `Fireball` -> `https://poedb.tw/us/Fireball`
- Use Title_Case: `Herald of Ash` -> `https://poedb.tw/us/Herald_of_Ash`
- Unique items: `https://poedb.tw/us/Headhunter`

### Search Fallback
If a direct URL returns no useful content or 404:
```
https://poedb.tw/us/search?q=<term>&in=default
```
Parse the search results to find the correct page, then fetch that.

### Image CDN Patterns

| Type | URL Pattern |
|------|-------------|
| Skill gems | `https://cdn.poedb.tw/image/Art/2DItems/Gems/<Name>.webp` |
| Support gems | `https://cdn.poedb.tw/image/Art/2DItems/Gems/Support/<Name>.webp` |
| Buff icons | `https://cdn.poedb.tw/image/Art/2DArt/BuffIcons/<Name>.webp` |
| Items | `https://cdn.poedb.tw/image/Art/2DItems/<Category>/<Name>.webp` |
| Currency | `https://cdn.poedb.tw/image/Art/2DItems/Currency/<Name>.webp` |
| Minimap icons | `https://cdn.poedb.tw/image/Art/2DArt/minimap/player/<Name>.webp` |

---

## Page Type Detection

After fetching a page, detect the type from content structure:

| Type | Key Indicators |
|------|---------------|
| `skill_gem` | Level progression table, support gem tags, mana cost/cast time |
| `unique_item` | Item image, explicit mod ranges with min-max, flavor text |
| `base_item` | Mod pool tables (prefix/suffix lists with weights) |
| `buff` | Buff attributes like BuffGroupsID, IsBuffDefinition, buff icon |
| `mechanic` | Item modifier tables with spawn weights across categories |
| `monster` | Damage%, life%, resistances, spectre flag |

---

## ExileApi Identifier Mapping

This is the critical mapping table. When extracting data from PoEDB, always map fields to their ExileApi equivalents:

### Buff Fields
| PoEDB Field | ExileApi Identifier | Notes |
|-------------|-------------------|-------|
| Buff internal ID (e.g. "ignited") | `Buff.Name` -> `BuffDefinition.Id` | The string key used in code |
| Buff display name | `Buff.DisplayName` -> `BuffDefinition.Name` | Human-readable |
| Buff description | `Buff.Description` -> `BuffDefinition.Description` | |
| Buff icon path | `BuffVisual.DdsFile` | DDS texture path |
| BuffGroupsID | `BuffDefinition.Type` | Numeric group ID |

### Modifier Fields
| PoEDB Field | ExileApi Identifier | Notes |
|-------------|-------------------|-------|
| Modifier key (e.g. "FireDamagePercent3") | `ItemMod.RawName` = `ModRecord.Key` | Primary lookup key |
| Modifier display name | `ItemMod.DisplayName` = `ModRecord.UserFriendlyName` | |
| Prefix / Suffix | `ModRecord.AffixType` (`ModType` enum) | |
| Domain | `ModRecord.Domain` (`ModDomain` enum) | |
| Group | `ModRecord.Group` / `ItemMod.Group` | |
| Required iLvl | `ModRecord.MinLevel` / `ItemMod.Level` | |
| Stat ranges | `ModRecord.StatRange` / `ItemMod.ValuesMinMax` | |
| Tag weights | `ModRecord.TagChances` (dict) | Spawn weight per tag |
| Tier | `ModRecord.Tier` | |
| Stats | `ModRecord.StatNames` (`StatRecord[]`) | |
| Is essence mod | `ModRecord.IsEssence` | |
| Influence type | `ModRecord.InfluenceType` | |
| Hash | `ModRecord.Hash32` | |
| Human-readable text | `ItemMod.Translation` | Formatted mod text |

---

## Instructions

### Mode 1: Single Lookup

1. Parse the user's query to identify the game term
2. Construct the direct URL: `https://poedb.tw/us/<Term_With_Underscores>`
3. Use `WebFetch` to retrieve the page. Use a targeted prompt based on expected page type:

   **For skill gems**, use this WebFetch prompt:
   > "Extract ALL data from this skill gem page: gem tags, mana cost, cast time, critical strike chance, damage effectiveness, quality bonus, the COMPLETE level progression table (every level with all stat columns), all support gem compatibility tags, and any threshold jewel interactions. Include exact numbers."

   **For unique items**, use this WebFetch prompt:
   > "Extract ALL data from this unique item page: base type, item class, required level, all explicit mods with their min-max ranges, implicit mods, flavor text, drop restrictions, and any related divination cards. Include exact numbers and ranges."

   **For buffs/mechanics**, use this WebFetch prompt:
   > "Extract ALL data about this buff or mechanic: internal ID, BuffGroupsID, buff description, buff icon path, associated modifiers with their stat values and weights, any related skills or items that apply this buff. Include exact internal identifiers."

   **For base items / mod pools**, use this WebFetch prompt:
   > "Extract ALL modifier data from this page: list every prefix and suffix with its modifier key name, tier number, required item level, stat ranges (min-max for each stat), spawn weight, and tags. Present as a complete table."

   **For monsters**, use this WebFetch prompt:
   > "Extract ALL monster data: damage%, life%, experience, resistances, abilities, spectre eligibility, monster type tags, and any associated buffs or skills."

   **Generic fallback prompt** (if type is unknown):
   > "Extract ALL datamined game data from this PoEDB page. Include every table, every stat, every internal identifier, every modifier with tiers and weights. Be comprehensive -- do not summarize or skip data."

4. If the direct URL fails, fall back to search: `https://poedb.tw/us/search?q=<term>&in=default`
   - Use WebFetch with prompt: "List all search results with their titles and URLs."
   - Pick the best match and fetch that page

5. Detect page type from content and apply the ExileApi identifier mapping
6. Format output (see Output Format below)

### Mode 2: Batch Lookup

When the user provides multiple terms, or when analyzing an entity that references many game elements:

1. Collect all terms needing lookup
2. Launch **parallel Agent sub-agents** -- one per term (or group 2-3 related terms per agent if there are many):

```
For each term, launch an Agent with prompt:
"Fetch https://poedb.tw/us/<Term> using WebFetch with this prompt: '<appropriate type-specific prompt from above>'. Then extract all data and map to ExileApi identifiers using this mapping: [include relevant subset of mapping table]. Return the structured result block."
```

3. Wait for all agents to complete
4. Compile results into a unified reference with a summary table at the top

### Mode 3: Deep Extract

For data-dense pages where a single WebFetch would lose detail:

**Phase 1 -- Structural Probe:**
```
WebFetch the page with prompt: "Describe the structure of this page: what sections exist, how many entries are in each table, what column headers are present. Do NOT extract the actual data yet -- just describe the structure."
```

**Phase 2 -- Targeted Extraction:**
Based on the structure, make multiple WebFetch calls targeting specific sections:
- If the page has a modifier table with 50+ entries, fetch in chunks by requesting specific tier ranges
- If the page links to sub-pages with more detail, follow those links
- Use section-specific prompts to get exact data

**When to use Deep Extract:**
- Page has modifier tables with more than ~20 entries
- You need complete tier data for crafting analysis
- The user explicitly asks for "everything" or "all tiers"
- Initial fetch clearly truncated or summarized data

---

## Output Format

### Single Lookup Result

```
## [Term] -- [page_type] -- https://poedb.tw/us/[URL_path]

### Core Data
[All extracted fields, organized by category]

### Image URLs
- Icon: [cdn URL if applicable]
- Art: [cdn URL if applicable]

### ExileApi Mapping
| Data Point | ExileApi Field | Value |
|-----------|---------------|-------|
| [field]   | [identifier]  | [extracted value] |

### Related Pages
- [Link to related PoEDB pages for further lookup]
```

### Batch Result

```
## PoEDB Batch Lookup -- [N] terms

### Summary
| Term | Type | Key Identifier | Primary Value |
|------|------|---------------|---------------|
| [term1] | [type] | [main ExileApi field] | [main value] |
| ... | ... | ... | ... |

### Detailed Results
[Individual blocks for each term, using single lookup format]
```

### Deep Extract Result

```
## [Term] -- Deep Extract -- https://poedb.tw/us/[URL_path]

### Page Structure
[Section inventory from Phase 1]

### [Section 1 Name]
[Complete data from targeted extraction]

### [Section 2 Name]
[Complete data from targeted extraction]

### ExileApi Mapping
[Full mapping table with all extracted identifiers]
```

---

## Important Notes

- PoEDB data is datamined and may lag behind game patches by a few hours/days
- Internal IDs from PoEDB map directly to what ExileApi reads from game memory
- When modifier keys contain numbers (e.g., `FireDamagePercent3`), the number is the tier
- Always include the `ModRecord.Key` / `ItemMod.RawName` -- this is the primary identifier used in ExileApi code
- If a buff page shows both a "buff" and associated "modifiers", extract both -- they're different things in ExileApi (`Buff` component vs `Mods` component)

$ARGUMENTS

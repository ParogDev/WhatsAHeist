# PoE Wiki Lookup

Look up Path of Exile game mechanics, items, skills, keystones, and other game knowledge from **poewiki.net** — the community-maintained wiki and authoritative source for how mechanics work, interact, and scale. This is the complement to `/poedb`: where PoEDB gives raw datamined numbers, the wiki gives prose explanations, formulas, thresholds, edge cases, and mechanic interactions that inform automation logic.

## When to Use

Use this skill when you need to understand **how something works**:
- **Skill gems**: mechanics, scaling, interactions, support compatibility, threshold jewels
- **Items**: unique effects, base type properties, drop restrictions, divination cards
- **Keystones / Ascendancy nodes**: what they change, override, or disable
- **Ailments & buffs**: application formulas, duration, thresholds, immunity sources
- **Charges**: generation, effects, scaling, max stacks
- **Auras & reservations**: effect radii, reservation mechanics, aura stacking
- **Map mods & league mechanics**: what they do, entity types, reward structures
- **Damage types & conversions**: how damage flows, conversion order, double-dipping rules
- **Mechanic interactions**: keystones that change ailment behavior, items that override defaults

Use `/poedb` instead when you need raw datamined numbers (modifier tiers, spawn weights, internal IDs).

## Operating Modes

This skill supports three modes. Detect which mode to use from the user's input:

### Mode 1: Single Lookup (default)
User provides one term. Fetch the page, detect its type, extract all relevant data, map to ExileApi concepts.

### Mode 2: Batch Lookup
User provides multiple terms, or you're investigating how several mechanics interact. Launch **parallel Agent sub-agents** (one per term or small groups) to fetch and extract concurrently, then compile results with an interaction analysis.

### Mode 3: Deep Extract
When a wiki article is long (e.g., the "Ailment" page covers all ailments, or "Modifier" explains the entire mod system), perform **two-phase fetching**: structural probe first, then targeted extraction of specific sections. Also follow "Main article" links for sub-page detail.

---

## URL Construction

### Direct Pages
```
https://www.poewiki.net/wiki/<Name>
```
- Replace spaces with underscores: `Herald of Ash` -> `https://www.poewiki.net/wiki/Herald_of_Ash`
- Case-sensitive first letter, rest matches wiki title: `Elemental ailment` not `elemental_ailment`
- Disambiguation: some terms have `_(disambiguation)` pages

### Search Fallback
If a direct URL returns no useful content or 404:
```
https://www.poewiki.net/w/index.php?search=<term>
```
Parse the search results to find the correct page, then fetch that.

### Common URL Patterns

| Category | URL Example |
|----------|-------------|
| Skill gems | `https://www.poewiki.net/wiki/Fireball` |
| Support gems | `https://www.poewiki.net/wiki/Spell_Echo_Support` |
| Unique items | `https://www.poewiki.net/wiki/Headhunter` |
| Base types | `https://www.poewiki.net/wiki/Leather_Belt` |
| Keystones | `https://www.poewiki.net/wiki/Elemental_Overload` |
| Ascendancy | `https://www.poewiki.net/wiki/Juggernaut` |
| Ailments | `https://www.poewiki.net/wiki/Ignite` |
| Currency | `https://www.poewiki.net/wiki/Chaos_Orb` |
| League mechanics | `https://www.poewiki.net/wiki/Delirium` |
| Map mods | `https://www.poewiki.net/wiki/List_of_map_mods` |
| Charges | `https://www.poewiki.net/wiki/Frenzy_charge` |

---

## Page Type Detection

After fetching a page, detect the type from content structure:

| Type | Key Indicators |
|------|---------------|
| `skill_gem` | Skill gem infobox, gem tags, mana cost, cast time, level progression table |
| `support_gem` | Support gem infobox, "supported skills" description, multiplier stats |
| `unique_item` | Item infobox with explicit/implicit mods, flavor text, drop info |
| `base_item` | Item class, base stats (armour/evasion/ES), implicit mod |
| `keystone` | Passive skill description, "grants" and "removes" effects |
| `ascendancy` | Ascendancy class description, notable nodes list |
| `ailment` | Damage type, application threshold, duration formula, immunity sources |
| `buff_mechanic` | Buff/debuff description, stacking rules, sources |
| `charge` | Per-charge effect, max charges, generation/consumption mechanics |
| `currency` | Orb/item function, vendor recipes, drop level |
| `league_mechanic` | League-specific entities, reward types, encounter structure |
| `map_mod` | Mod effect on area, monster modifiers, player restrictions |
| `damage_type` | Conversion rules, mitigation, related ailments |

---

## ExileApi Concept Mapping

Wiki content describes game mechanics in prose. Map these to the ExileApi types and components used to detect/react to them in code:

### Skill Gems
| Wiki Concept | ExileApi Identifier | Notes |
|-------------|-------------------|-------|
| Gem tags (Spell, Fire, AoE...) | `SkillGemWrapper.Tags` | Bitfield of gem tags |
| Gem level | `SkillGemWrapper.Level` | Current socketed level |
| Gem quality | `SkillGemWrapper.Quality` | 0-20+ quality |
| Cast time | `SkillGemWrapper.CastTime` | Base cast time in ms |
| Mana cost | `SkillGemWrapper.ManaCost` | After modifiers |
| Cooldown | `SkillGemWrapper.Cooldown` | In ms |
| DPS stats | `SkillGemWrapper.DPS` / `.DamageMin` / `.DamageMax` | Tooltip values |
| Socket links | `ItemMods` on socketed item | Linked support gems |

### Ailments & Buffs
| Wiki Concept | ExileApi Identifier | Notes |
|-------------|-------------------|-------|
| Ailment active on entity | `Buff.Name` (e.g. `"ignited"`, `"chilled"`, `"frozen"`, `"shocked"`, `"poisoned"`) | Check via `Entity.GetComponent<Buffs>()` |
| Buff display name | `Buff.DisplayName` -> `BuffDefinition.Name` | Human-readable |
| Buff duration remaining | `Buff.Timer` | Seconds remaining |
| Buff stack count | `Buff.Charges` | For stackable buffs (poison, wither) |
| Ailment immunity | Absence of buff OR specific immunity buff | Wiki documents immunity sources |
| Elemental ailment threshold | Not directly in API -- wiki formula needed | `Damage / (AilmentThreshold)` |

### Items & Mods
| Wiki Concept | ExileApi Identifier | Notes |
|-------------|-------------------|-------|
| Unique item name | `Entity.GetComponent<Base>().Name` | |
| Item rarity | `Entity.GetComponent<Mods>().ItemRarity` | Normal/Magic/Rare/Unique |
| Explicit mods | `Entity.GetComponent<Mods>().ItemMods` | List of `ItemMod` |
| Implicit mods | `Entity.GetComponent<Mods>().ImplicitMods` | |
| Mod text | `ItemMod.Translation` | Human-readable mod line |
| Item base type | `Entity.GetComponent<Base>().ItemBaseName` | |
| Item class (weapon, armour...) | `GameController.Files.BaseItemTypes` | Lookup by path |
| Socket info | `Entity.GetComponent<Sockets>()` | Links, colors, count |

### Keystones & Passives
| Wiki Concept | ExileApi Identifier | Notes |
|-------------|-------------------|-------|
| Keystone allocated | `PlayerPassives.Keystones` | Set of allocated keystone IDs |
| Notable allocated | `PlayerPassives.Notables` | |
| Ascendancy node | `PlayerPassives.AscendancyAllocated` | |
| Passive grants buff | Check `Buffs` component for the granted buff | Wiki documents which buffs keystones grant |

### Charges & Resources
| Wiki Concept | ExileApi Identifier | Notes |
|-------------|-------------------|-------|
| Frenzy/Power/Endurance charges | `Buff.Name` = `"frenzy_charge"`, `"power_charge"`, `"endurance_charge"` | Stack count via `Buff.Charges` |
| Life/Mana/ES | `Entity.GetComponent<Life>()` | `.CurHP`, `.MaxHP`, `.CurMana`, `.MaxMana`, `.CurES`, `.MaxES` |
| Flask charges | `Entity.GetComponent<Flask>()` | `.CurrentCharges`, `.MaxCharges` |
| Aura active | Check `Buffs` for aura buff name | Wiki lists aura buff names |

### Monsters & Entities
| Wiki Concept | ExileApi Identifier | Notes |
|-------------|-------------------|-------|
| Monster type | `Entity.GetComponent<ObjectMagicProperties>().Rarity` | Normal/Magic/Rare/Unique |
| Monster mods | `Entity.GetComponent<ObjectMagicProperties>().Mods` | Nemesis, bloodline, etc. |
| Is alive | `Entity.IsAlive` | |
| Monster name | `Entity.GetComponent<Render>().Name` | Display name |
| Proximity shield | Check buffs or `ObjectMagicProperties.Mods` | Wiki describes the mechanic |

---

## Instructions

### Mode 1: Single Lookup

1. Parse the user's query to identify the game term
2. Construct the wiki URL: `https://www.poewiki.net/wiki/<Term_With_Underscores>`
3. Use `WebFetch` to retrieve the page. Use a targeted prompt based on expected page type:

   **For skill gems**, use this WebFetch prompt:
   > "Extract ALL information about this skill gem: gem tags, mana cost, cast time, critical strike chance, damage effectiveness, quality bonus, the level progression table, support gem compatibility, skill mechanics description, any threshold jewel interactions, and notable interactions with keystones or ascendancy nodes. Include exact numbers and formulas."

   **For unique items**, use this WebFetch prompt:
   > "Extract ALL information about this unique item: base type, item class, required level, all explicit mods with their value ranges, implicit mods, flavor text, drop restrictions, acquisition methods (divination cards, boss drops, vendor recipes), and any notable build-defining interactions or synergies."

   **For ailments/buffs**, use this WebFetch prompt:
   > "Extract ALL information about this ailment or buff: how it's applied, damage type, application formula/threshold, duration formula, effect scaling, stack behavior, immunity sources (keystones, flasks, mods, ascendancy), related ailments, and any keystones or items that modify its behavior. Include exact formulas and thresholds."

   **For keystones**, use this WebFetch prompt:
   > "Extract ALL information about this keystone: what it grants, what it removes or disables, exact stat values, interactions with other mechanics (ailments, charges, damage types), and any ascendancy nodes or items that interact with it. Note any counterintuitive behaviors."

   **For league mechanics**, use this WebFetch prompt:
   > "Extract ALL information about this league mechanic: how encounters work, entity types that spawn, reward structure, any special monsters or bosses, modifiers that appear, and map mod interactions. Include any relevant thresholds or scaling."

   **For charges**, use this WebFetch prompt:
   > "Extract ALL information about this charge type: per-charge effects, max charges (base and modifiers), generation methods, consumption mechanics, duration, related skills/items/keystones, and any special interactions."

   **For damage types / conversions**, use this WebFetch prompt:
   > "Extract ALL information about this damage type: conversion rules and order, related ailments, mitigation mechanics (resistance, armour, evasion), penetration, and any keystones or items that modify conversion behavior. Include formulas."

   **Generic fallback prompt** (if type is unknown):
   > "Extract ALL game mechanics information from this wiki page. Include every formula, threshold, interaction, condition, and edge case. Be comprehensive -- do not summarize or skip details. List any related wiki pages referenced."

4. If the direct URL fails, fall back to search: `https://www.poewiki.net/w/index.php?search=<term>`
   - Use WebFetch with prompt: "List all search results with their titles and URLs."
   - Pick the best match and fetch that page

5. Detect page type from content and apply the ExileApi concept mapping
6. Flag automation implications -- how this mechanic affects script/plugin logic
7. Format output (see Output Format below)

### Mode 2: Batch Lookup

When the user provides multiple terms, or when investigating mechanic interactions:

1. Collect all terms needing lookup
2. Launch **parallel Agent sub-agents** -- one per term (or group 2-3 related terms per agent if there are many):

```
For each term, launch an Agent with prompt:
"Fetch https://www.poewiki.net/wiki/<Term> using WebFetch with this prompt: '<appropriate type-specific prompt from above>'. Extract all mechanics information and map to ExileApi concepts using this mapping: [include relevant subset of mapping table]. Return the structured result block."
```

3. Wait for all agents to complete
4. Compile results into a unified reference
5. **Cross-reference interactions** -- if the looked-up terms interact with each other (e.g., a keystone that modifies an ailment, an item that changes charge behavior), call this out explicitly

### Mode 3: Deep Extract

For long wiki articles where a single WebFetch would lose detail:

**Phase 1 -- Structural Probe:**
```
WebFetch the page with prompt: "Describe the structure of this wiki article: what sections and subsections exist, what topics are covered, are there tables or formulas. Do NOT extract the actual content yet -- just describe the structure and list section headings."
```

**Phase 2 -- Targeted Extraction:**
Based on the structure, make multiple WebFetch calls targeting specific sections:
- Request content from specific sections by name
- Follow "Main article: X" links to dedicated sub-pages
- Use section-specific prompts for formulas, tables, or interaction lists

**When to use Deep Extract:**
- Article covers a broad topic (e.g., "Ailment", "Modifier", "Damage")
- Article has many sections with different subtopics
- User explicitly asks for "everything" or "all details"
- Initial fetch clearly truncated or summarized important content
- You need formulas and thresholds that might be buried in dense prose

---

## Output Format

### Single Lookup Result

```
## [Term] -- [page_type] -- https://www.poewiki.net/wiki/[URL_path]

### How It Works
[Core mechanic explanation -- what it does, how it's applied, key formulas]

### Key Numbers
- [Thresholds, durations, scaling values, base stats]
- [Exact formulas where available]

### ExileApi Detection
| Concept | ExileApi Approach | Code Pattern |
|---------|------------------|--------------|
| [what to detect] | [which component/field] | [brief code hint] |

### Interactions
- [Keystones that modify this mechanic]
- [Items that change behavior]
- [Ascendancy interactions]
- [Map mods that disable or alter it]

### Automation Implications
- [How this affects plugin/script logic]
- [Edge cases to handle]
- [Known limitations in ExileApi detection]

### Related Pages
- [Links to related wiki articles for further reading]
```

### Batch Result

```
## PoE Wiki Batch Lookup -- [N] terms

### Summary
| Term | Type | Key Mechanic | ExileApi Detection |
|------|------|-------------|-------------------|
| [term1] | [type] | [one-line summary] | [primary API approach] |
| ... | ... | ... | ... |

### Cross-References
- [Interaction between looked-up terms, if any]
- [Shared mechanics or conflicts]

### Detailed Results
[Individual blocks for each term, using single lookup format]
```

### Deep Extract Result

```
## [Term] -- Deep Extract -- https://www.poewiki.net/wiki/[URL_path]

### Article Structure
[Section inventory from Phase 1]

### [Section 1 Name]
[Complete content from targeted extraction]

### [Section 2 Name]
[Complete content from targeted extraction]

### ExileApi Mapping
[Full mapping table for all concepts found in the article]

### Automation Implications
[Comprehensive list across all sections]
```

---

## Important Notes

- poewiki.net is community-maintained and generally up-to-date within days of patches
- Wiki formulas are the authoritative source for mechanic calculations -- PoEDB doesn't include these
- Many wiki pages link to "Main article" sub-pages -- follow these for full detail
- Ailment thresholds and damage formulas are NOT available in ExileApi -- the wiki is the only source for understanding these calculations
- When the wiki describes a mechanic that "cannot be" applied (immunity), document ALL sources of immunity -- these are critical for automation logic to handle edge cases
- Some buff/debuff names on the wiki match `Buff.Name` strings exactly (e.g., "ignited", "chilled") -- always note these when found
- If the wiki and PoEDB disagree, the wiki is authoritative for mechanics/behavior, PoEDB is authoritative for internal IDs/numbers

$ARGUMENTS

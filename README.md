# RTS-Warcraft — a Warcraft III style RTS for Unity

A complete real-time strategy game in the spirit of **Warcraft III**: two races,
resource gathering, base building, unit training, heroes with spells, fog of
war, a minimap and a computer opponent that attacks you in waves.

Everything — terrain, units, buildings, effects, interface, minimap — is
**generated procedurally at runtime**. The repository contains no binary art
assets, so the project is small, diff-friendly and impossible to have a broken
import.

---

## Quick start

1. Install **Unity 2022.3 LTS** (any 2022.3.x; the project was authored against
   `2022.3.62f1`). 2021.3 LTS and 2023.x will also open it.
2. Open Unity Hub → *Add project from disk* → select this folder.
3. Open the scene **`Assets/Scenes/Main.unity`**.
4. Press **Play**.

That is the entire setup. The scene contains a single GameObject
(`GameBootstrap`); at runtime it builds the camera, the sun, the 128×128 map,
the resource nodes, both players, all systems and the whole interface.

> **Render pipeline:** built-in. **Input:** legacy Input Manager.
> **Packages:** only `com.unity.ugui` plus the default engine modules — see
> `Packages/manifest.json`. Nothing to buy, nothing to import.

### Inspector options on `GameBootstrap`

| Field | Meaning |
| --- | --- |
| `mapSeed` | Deterministic map layout. Change it for a different map. |
| `spawnAiOpponent` | Play against the Horde AI. Turn off for a sandbox. |
| `startingWorkers` | Workers each player begins with (1–12). |
| `startingHeroes` | Give every player one hero at the start. |
| `verbose` | Log bootstrap info and print tutorial messages in game. |

---

## Controls

| Input | Action |
| --- | --- |
| **Left drag** | Box-select your units |
| **Left click** | Select a unit / building / resource node |
| **Shift + click** | Add to / remove from the selection |
| **Double click** a unit | Select every unit of that type you own |
| **Right click** ground | Move |
| **Right click** enemy | Attack it |
| **Right click** mine / tree | Send workers to harvest |
| **Right click** own building | Repair it (workers) or walk to it |
| **Middle click** with a building selected | Set the rally point |
| **A**, then left click | Attack-move |
| **S** / **H** | Stop / Hold position |
| **Ctrl + 0…9** / **0…9** | Assign / recall control groups (**Alt+number** also centres the camera) |
| **F1 / F2 / F3** | Select all military / all workers / idle workers |
| **Space** | Centre the camera on the selection |
| **WASD**, arrow keys, screen edge | Pan the camera (`A` pans only with the left arrow — `A` is attack-move) |
| **Mouse wheel** | Zoom |
| **Q / E**, **R** | Rotate the camera, reset rotation |
| **Middle drag** | Pan by dragging |
| **+ / −**, **P** | Game speed up / down, pause |
| **Esc** | Cancel placement, or clear the selection |

Select a **worker** to get the build menu in the command card; click a building
icon, move the ghost to a green spot and click to place. Select a **hero** to
get its spells (`Q` / `W` hotkeys are shown on the buttons).

---

## What is in the box

### Races

| Alliance (blue) | Horde (red) |
| --- | --- |
| Peasant (worker) | Peon (worker) |
| Footman, Archer, Knight | Grunt, Troll Axethrower, Ogre |
| **Paladin** — Holy Light, Divine Shield | **Blademaster** — Critical Strike, Wind Walk |
| **Archmage** — Blizzard, Water Elemental | **Far Seer** — Chain Lightning, Feral Spirit |
| Town Hall, Farm, Barracks, Lumber Mill, Blacksmith, Guard Tower, Altar of Kings, Arcane Sanctum | Great Hall, Burrow, Troll Hut, War Mill, Stronghold, Watch Tower, Altar of Storms, Spirit Lodge |

### Systems

* **Economy** — gold mines (5 workers per mine) and trees, 10 resources per
  carry, drop-off at the Town Hall / Great Hall, farms supply food up to 90.
* **Construction** — workers path to the site, the footprint is reserved on the
  grid, progress is shared between builders, repair costs 2 % of the base cost
  per tick.
* **Combat** — Warcraft-style armour (`damage × 0.94^armor`), damage types
  (normal / piercing / siege / magic) with a modifier table, ranged projectiles
  with an arc, tower defences, auto-acquisition at 10 units.
* **Heroes** — 10 levels, strength / agility / intelligence, mana, cooldowns,
  experience from nearby kills, passives and active spells.
* **Pathfinding** — A\* on the tile grid with octile heuristic, no corner
  cutting, line-of-sight string pulling, nearest-walkable fallback and local
  separation steering so groups spread out instead of stacking.
* **Fog of war** — a 64×64 visibility grid baked into a texture and drawn with
  the included `Assets/Shaders/FogOfWar.shader`; unexplored / explored /
  visible states, enemy units hidden unless revealed.
* **Interface** — resource bar, command card with cooldowns, 12-slot selection
  strip, message log, drag box, floating damage numbers, world-space health and
  mana bars, clickable minimap, victory / defeat screen.
* **AI opponent** — grows the economy to 16 workers, builds farms, a barracks
  and an altar, trains a mixed army, retreats wounded units and attacks in
  escalating waves.

---

## Project layout

```
Assets/
  Scenes/Main.unity          the only scene (one bootstrap object)
  Shaders/FogOfWar.shader    the fog mask shader
  Scripts/
    Core/       GameBootstrap, GameManager, GameMap, GameData, GameEnums,
                RTSPlayer, OrderSystem, SelectionManager, PlacementController,
                RTSCameraController, EnemyAI, RTSGameUtility
    Units/      UnitController
    Buildings/  BuildingController
    Combat/     CombatMath, AbilitySystem, ProjectileController, CombatFx
    World/      WorldFactory, FogOfWar, SelectableMarker
    UI/         HUDController, MinimapController, FloatingText
ProjectSettings/             editor version + build scene list
Packages/manifest.json       ugui + engine modules only
Tools/                       offline verification (see below)
```

`GameData.cs` is the single tuning file: every unit cost, stat, build time and
ability value lives there.

---

## Verifying the project without Unity

Unity cannot run in this repository's CI sandbox (no editor binary, no GPU, no
access to `download.unity3d.com`), so the project ships its own offline checks:

```bash
pip install tree_sitter tree_sitter_c_sharp pyyaml
bash Tools/check_all.sh
```

That runs four passes:

1. **`Tools/cs_symbols.py`** — builds a symbol table (types, members, statics)
   from every `.cs` file with a real C# grammar.
2. **tree-sitter parse** — all 24 scripts must parse with zero `ERROR` nodes.
3. **`Tools/cs_xref.py`** — every `Type.Member` access resolves to a member that
   actually exists *and* is static where it is used that way; every
   `new Type { field = … }` initialiser only sets real fields.
4. **`Tools/validate_project.py`** — every asset has a `.meta` with a unique
   GUID, every Unity YAML document parses, every `{fileID: n}` reference
   resolves, and the `MonoBehaviour` in `Main.unity` points at a real script
   GUID with serialized fields that match the script's public fields.

Current result:

```
types: 44
parsed 24 files, 0 with syntax errors
cross-reference findings: 0
assets with .meta: 26, unique guids: 26
OK - no structural problems found.
ALL CHECKS PASSED
```

**What this does *not* prove:** it is not a compile and not a play test. Unity's
own C# compiler, shader compiler and runtime behaviour have not been executed
here. If anything is off, it will be an API-signature or shader detail rather
than a structural one — the checks above are the strongest available offline.

---

## Ideas for extending it

* Creep camps and neutral items (there is a hook in
  `GameBootstrap.SpawnStartingExtras`).
* Upgrades in the Blacksmith / Stronghold (`GameData` already has the buildings).
* Air units — the combat maths is 2-D by distance, so adding a flyer flag is
  mostly a pathfinding and targeting change.
* Multiplayer via `Unity.Networking` — the simulation is already driven by one
  `float dt` per tick through `GameManager.gameSpeed`.
* Real art: `WorldFactory` is the only place geometry is created, so swapping a
  procedural mesh for a prefab is a local change.

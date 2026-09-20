# Sweep Zones Redux

An [Oxygen Not Included](https://www.klei.com/games/oxygen-not-included) mod that adds a **Sweep Zones** tool to the toolbar: paint persistent zones and any debris that lands in them is automatically marked for sweeping (and moppable puddles receive mop errands) at the priority you painted with — indefinitely, until you erase the zone.

A ground-up rewrite of the feature set of [Berkays/Rubacava's Sweep Zones](https://github.com/Berkays/OniMods/tree/main/SweepZones) (unmaintained, crashes on current game versions) for the current game (U59+, all DLCs). No code or assets are shared with the original.

## Features

- **Box-drag painting** with four modes in the standard tool side menu: Sweep zone, Erase sweep zone, Mop zone, Erase mop zone.
- **Per-cell priority**: zones remember the priority (including yellow-alert) selected when painted; number keys work while the tool is active. Newly arriving debris is marked within a second; priorities of already-marked items are never overridden, so manual adjustments stick.
- **Priority labels**: while the tool is active, every contiguous same-priority region shows its priority number centered on the region (painting adjacent to an existing zone of the same priority merges them into one labeled region).
- **Zone display without overlays**: while the tool is active, the zones of the selected mode's kind (sweep or mop, following the mode selector) are tinted with a light perimeter stroke (warm gradient for sweep, cool for mop, brighter = higher priority) via the game's per-tool cell-coloring hook — the overlay system is untouched, so overlay-adding mods (foot traffic, transit tube, etc.) are unaffected. This deliberately fixes the original's overlay conflicts.
- **Saved with your game** (zones serialize into the save file; loading without the mod just drops them).
- Mop zones follow vanilla mop rules (liquid on a solid floor, ≤ 150 kg).
- **Rebindable hotkey** (default `Shift+Z`) to activate the tool, configurable under options → game → controls → Mods (via [PLib](https://github.com/peterhaneve/ONIMods/tree/main/PLib), merged into the mod DLL).
- No image assets (the toolbar icon is drawn procedurally).

Erasing a zone keeps errands that were already created — cancel those with the vanilla Cancel tool if needed.

Not carried over from the original: the standalone overlay-menu entry (by design, see above) and the Forbid Items integration.

## Installing

Subscribe on the [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3804026640).

## Building

Requires the .NET SDK (8+). Shared build configuration lives in the [oni-mods-common](https://github.com/isochronous/oni-mods-common) submodule, so clone with `--recurse-submodules` (or run `git submodule update --init`). The game DLLs are referenced directly from the game install; override the path if yours differs:

```
dotnet build src/SweepZones -c Release -p:GameFolder="<path-to>\OxygenNotIncluded"
```

A successful build automatically deploys the mod to `Documents\Klei\OxygenNotIncluded\mods\local\SweepZones`.

## Implementation notes

Four Harmony patch points, all on stable surfaces: `Db.Initialize` (strings + icon), `SaveGame.OnPrefabInit` (zone store component), `PlayerController.OnPrefabInit` (tool registration), `ToolMenu.CreateBasicTools` (toolbar button). The tool itself subclasses the game's `DragTool` and reuses the vanilla Sweep tool's cursor/visualizer assets; the mode menu is the game's own `ToolParameterMenu`.

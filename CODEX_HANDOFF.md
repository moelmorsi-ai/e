# Codex Handoff — 3000 BC Simulation Expansion

## User's locked decisions
- Procedural Earth-like alternate planet, set in 3000 BC.
- Keep 512x256 global grid initially; cells represent regions.
- Multiple settlement entities may exist within one cell.
- Aggregated POP cohorts.
- Rule-based rulers initially; optional generative AI later.
- Dry hot deserts yellow-gold; cold deserts pale sand.

## Files changed
- `MapView.cs`: desert palette on biome map and main terrain map; main map defaults to Terrain; mineral-resource display includes Tin.
- `Scripts/WorldGen/WorldData.cs`: Tin resource enum member and resource count 11.
- `Scripts/WorldGen/NaturalResourceGenerator.cs`: procedural Tin potential.
- `Scripts/WorldGen/ProceduralWorldGenerator.cs`: initializes simulation runtime from generated geography.
- `Main.tscn`, `TheGame.tscn`: attach `SimulationRuntime`.
- `Scripts/Simulation/DomainModels.cs`: complete first-pass simulation entity/data model.
- `Scripts/Simulation/SimulationSystems.cs`: bootstrap and first-pass simulation systems.
- `Scripts/Simulation/SimulationRuntime.cs`: Godot runtime host.
- `MASTER_GAME_DESIGN.md`, `DEVELOPMENT_STATUS.md`, `PATCH_NOTES.md`: implementation register and validation boundary.

## First Godot/Codex tasks
1. Open with Godot 4.7.2 .NET and .NET 8.
2. Build the C# project. Resolve any compiler issues before proceeding.
3. Confirm both scenes parse and `SimulationRuntime` script attaches.
4. Run main scene. Confirm world generation completes, map opens in Terrain mode, deserts appear yellow, and runtime state initializes with chiefdoms/settlements/cohorts.
5. Inspect runtime state through debugger: `SimulationRuntime.State`, settlements, cohorts, polities, cultures and resources.
6. Advance simulation for at least 24 in-game months; inspect population, food/water, trade, diplomacy and event behavior.
7. Serialize and deserialize a save; compare core counts, cell state and calendar.
8. Verify tin appears in generated resource arrays and inspector's top-resource list.
9. Test route finding across land and ensure ocean/lake cells are impassable.
10. Report compiler/runtime errors with exact stack trace and source line.

## Known first-pass limitations
This delivery adds functioning domain models and initial mechanics, but it is not a finished game. UI controls and inspectors for new simulation systems are not yet authored. Trade routes need UI and caravan logistics; economic production needs a complete recipe/labor model; technology is currently a progress proxy; military combat is not yet a battle-resolution system; diplomacy and ruler AI are initial heuristics. Geography still uses strategic proxies for climate/hydrology/geology/soil/resources. Resource potential 0–100 must be converted into calibrated physical quantities before balancing. JSON persistence needs versioning/migrations and save-slot UX.

Do not remove existing geography or rewrite MapView wholesale. Continue by compiling, correcting issues, and incrementally deepening each system while preserving the master scope.

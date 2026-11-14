# 3000 BC — Master Game Design & Implementation Register

## Locked identity
A procedural Earth-like alternate planet in 3000 BC. The landmasses, climate, resources and civilizations are generated; the world is not a reconstruction of historical Earth. Godot 4.7.2 .NET / C# is the target. The simulation is deterministic, real-time and initially uses rule-based rulers. Generative AI is optional future functionality and is not required by the simulation.

## Locked scale architecture
Keep the current 512 × 256 global grid as the initial strategic layer. A cell is a large region, not one village. Each cell carries area-based arable potential, cultivated land, pasture, forest use, built-up area, rural population, carrying capacity, water/food reserves, ownership, control, culture, religion and resource potential. Multiple settlement entities can be anchored in one cell. Settlement populations are represented by aggregated POP cohorts. Optional local subgrids can be introduced for selected major cities later, without increasing global simulation cost.

## Geography and map
Preserve the existing procedural world, spherical sampling, physical geography, climate, hydrology, geology, soil, ecology, resources, gameplay indices, inspector and all existing 44 map modes. Dry hot deserts are yellow-gold and cold deserts are pale sand on both the biome and main terrain maps. Tin is added as an eleventh strategic resource and is included in mineral resource inspection.

## Implemented in this delivery
- Added a separate BronzeAge simulation domain layer: cell state, settlements, population cohorts, cultures, religions, polities, rulers, treaties, trade routes, armies, events, buildings and technologies.
- Added world bootstrap from the generated geography and a procedural starting setup with multiple independent chiefdoms, rulers, cultures, religions, settlements and occupational cohorts.
- Added simulation clock and daily/monthly/annual scheduling.
- Added population needs, health, morale, births, deaths and food-stress migration.
- Added cultivation and climate/soil-informed strategic crop-yield estimates.
- Added bronze smelting requiring copper ore, tin ore and fuel.
- Added resource extraction, construction, markets, trade route creation and goods transfer.
- Added political control transfer, organic frontier influence, government evolution and tax/tribute revenue.
- Added culture diffusion, religion institutions, technology-development proxy, treaties and rule-based ruler behavior.
- Added strategic pathfinding, army movement orders, supply, morale and combat-power calculation.
- Added drought, flood and epidemic event operations.
- Added JSON save/load APIs and a Godot SimulationRuntime host, initialized from the same generated WorldData.
- Added Tin to the geographical resource layer.
- Changed dry desert colors to yellow-gold in the biome and main terrain map.

## Important implementation boundaries
The simulation systems are now real code with working data models and update methods, but this is not yet a finished, balanced commercial game. The following still require further implementation/integration:
- UI panels and player commands for the new systems; the existing geographical inspector remains the primary UI.
- A full production labor market, recipe catalog, inventory logistics and historically calibrated commodity quantities.
- Real crop calendars, crop-specific monthly climate integration and irrigation networks.
- Settlement founding AI, rural hamlet distribution, population cohort splitting/merging optimization and full migration route choice.
- Route capacity, caravan agents, trade tariffs, piracy/bandit risk and market clearing.
- Complete succession/dynasty model, laws, tax administration, civil wars and detailed state institutions.
- Actual technology unlock/progress trees and knowledge transfer.
- More complete language/religion diffusion, minority rights and syncretism.
- Unit equipment, recruitment, command, combat resolution, sieges and peace settlement.
- Daily weather, hydrodynamic flood depth, aquifer storage/baseflow and full lake storage.
- Autosave, save-version migrations, deterministic cross-version replay and user-facing save slots.
- Profiling, balancing, accessibility, tutorials, menus and release packaging.

## Simulation calendar
The initial prototype uses a 360-day strategic year with twelve 30-day months. This is a game calendar convention, not an assertion about the exact astronomical year. The geographical climate engine continues to use its own 12-month climatology.

## Resource model
The geographical resource arrays store 0–100 potential/accessibility proxies, not proven quantities. The simulation bootstrap copies these into `DepositPotential` as relative strategic deposits. Resource extraction currently depletes these slowly. This must be calibrated into actual tonnage/volume units before serious economic balancing. Bronze production is modeled as an explicit recipe requiring copper ore, tin ore and fuel; no bronze appears automatically from nearby deposits.

## Validation
The source archive was inspected and updated. Static delimiter checks pass. Godot/.NET compilation and runtime tests were not possible in this editing environment because Godot and the .NET CLI are not installed. Codex/Godot must compile and run the project, verify scene attachment, serialization, generated world setup, map rendering and long-duration simulation. Do not interpret source presence as runtime verification.

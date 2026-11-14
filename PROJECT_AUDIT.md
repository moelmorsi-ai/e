# Project Audit

## Project identity

- Engine: Godot 4.7.2 .NET (from `New Game Project.csproj`)
- Target framework: .NET 8 (`net8.0`)
- Main scene: `Main.tscn`
- Core display: `MapView.cs` with `MapRenderer` and `WorldGenerator` children
- Coordinate support: `WorldCoordinateSystem.cs`

## Existing source layout

- `Scripts/WorldGen/ProceduralWorldGenerator.cs`: seed-based spherical continent/elevation generation and initial texture rendering.
- `Scripts/WorldGen/WorldData.cs`: central cell arrays, physical terrain derivatives, priority-flood drainage, catchment routing, river discharge/regime, flood susceptibility and baseline gameplay indices.
- `Scripts/WorldGen/ClimateGenerator.cs`: previous preliminary monthly temperature/precipitation model; expanded in this milestone.
- `Scripts/WorldGen/WorldGenerationSettings.cs`: seed, width, height and target land fraction.
- `WorldCoordinateSystem.cs`: image-pixel to latitude/longitude mapping.
- `MapView.cs`: map-mode selector, rendering, zoom, pan, location selection and inspector.

## Architecture decisions

1. Keep `WorldData` as the sole geographical data store.
2. Keep the existing terrain-derived priority-flood drainage algorithm; change its runoff input rather than replacing its topology.
3. Move orchestration into `WorldGenerationPipeline` while keeping `WorldData.CalculateDerivedLayers()` as a compatibility entry point.
4. Put climate, geology/soil, hydrology extensions, ecology, resources and gameplay calculations in separate generator classes.
5. Preserve the existing `MapView` interaction system and extend its map-mode selector/rendering/inspector.
6. Use deterministic subsystem-specific seed offsets and spherical 3D noise sampling for generated geological/resource patterns.

## Main integration risks to validate

- Godot 4.7.2 C# API compatibility for OptionButton item IDs and the newly added generator classes.
- First-run C# script import/build behavior for the newly added `.cs` files.
- Generation time for the added monthly upwind moisture and orographic sampling at 512 × 256.
- Calibration of rainfall, runoff, river thresholds, aridity and wetland coverage across several seeds.
- Whether the current priority-flood implementation and river thresholds produce a useful distribution of river orders and permanent/seasonal/intermittent regimes.
- Whether the inspector remains comfortably readable with the additional fields.

## Explicit non-goals

This milestone does not implement political borders, population, diplomacy, economy, combat resolution, government, technology, roads/bridges, crop-specific yield or full weather forecasting.


## Follow-up source audit findings

- The monthly inspector previously refreshed the map and legend but not the selected-cell text. The selected data-cell index is now retained and its month-specific inspector line is refreshed on month changes.
- The upwind sampling Y coordinate in the climate moisture-fetch and orographic routines previously moved in the same screen-space direction as the wind's north/south component. It now samples opposite the wind-to vector.
- The soil-water bucket previously counted recharge both as groundwater recharge and soil storage, and counted snowmelt a second time in runoff. Recharge is now split from infiltration before storage; snowmelt is counted once.
- Lake inflow depth conversion previously used a coefficient 1,000 times too small.
- Monthly and annual mean discharge previously used inconsistent month/year durations. Monthly discharge now uses equal 1/12-year intervals.
- Runtime compilation is still required in the target Godot .NET environment; the present editing environment does not include Godot or the .NET CLI.

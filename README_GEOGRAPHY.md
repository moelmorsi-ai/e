# Procedural Geography Engine

## Purpose

This project now has a dependency-ordered, deterministic geographical pipeline built around one `WorldData` instance. The model is intended for a 512 × 256 grand-strategy world and provides internally connected strategic-scale estimates. It is not a GIS dataset, a numerical weather prediction model, a surveyed geology model or an engineering hazard assessment.

## Generation pipeline

`ProceduralWorldGenerator` creates the land mask and elevation, then calls `WorldData.CalculateDerivedLayers()`. That method delegates to `WorldGenerationPipeline`.

The pipeline order is:

1. Coastal classification.
2. Local relief, slope and aspect.
3. Terrain classification.
4. Procedural geological provinces, parent-rock classes, geological stability and seismic-risk proxy.
5. Monthly climate: temperature, circulation belts, ITCZ movement, prevailing winds, precipitation, PET and snowfall.
6. Soil classes and properties using climate, parent rock and terrain.
7. Monthly soil-water balance: actual evapotranspiration, infiltration, surface runoff and groundwater recharge.
8. Existing priority-flood drainage and catchment routing, now supplied with climate/soil-derived runoff.
9. Monthly discharge, river regime, groundwater potential, freshwater access and lake-balance proxy.
10. Regional flood susceptibility, including seasonal-flow influence.
11. Vegetation, biomes and wetland potential.
12. Natural-resource abundance and accessibility.
13. Construction, agriculture, settlement and unit-specific movement scores.
14. Numerical and structural diagnostics.

All systems write to or read from the same `WorldData`. No climate or ecology subsystem creates a separate world.

## Climate model

### Temperature

Monthly temperature is derived from latitude, a hemispheric seasonal cycle, elevation and a continentality proxy. The lapse-rate default is 6.5 °C/km. Continentality is estimated from distance to the coast, reducing the seasonal temperature range near oceans. The model is deliberately simple and should be tuned using generated-world diagnostics.

### Circulation and winds

The model uses simplified tropical trade-wind, mid-latitude westerly and polar-easterly belts. The ITCZ shifts through the year with a configurable 7.5° latitude amplitude. Monthly wind direction and speed are stored per cell. Wind direction is a flow-to bearing clockwise from north.

### Precipitation

Monthly rainfall combines:

- ITCZ-related convective potential;
- mid-latitude storm-track and polar precipitation proxies;
- subtropical subsidence reduction;
- upwind ocean moisture fetch and inland moisture depletion (sampling opposite the wind-to vector in both map axes);
- orographic uplift on rising windward terrain;
- leeward rain-shadow reduction behind higher upwind terrain;
- a seasonal tropical monsoon enhancement.

The monthly component arrays retain convective, orographic and rain-shadow contributions for inspection and future tuning. The upwind sample direction is opposite the wind-to vector in both longitude and latitude axes. Noise is not used as the primary rainfall generator.

### Evapotranspiration and aridity

Potential evapotranspiration is a temperature-, latitude- and season-dependent strategic proxy. Soil water balance calculates actual evapotranspiration. Aridity is annual precipitation divided by annual potential evapotranspiration (P/PET). The centralized default classes are:

| Class | P/PET |
|---|---:|
| Hyper-arid | < 0.05 |
| Arid | 0.05–0.20 |
| Semi-arid | 0.20–0.50 |
| Dry subhumid | 0.50–0.65 |
| Humid | ≥ 0.65 |

Climate families are Köppen-inspired broad strategic classes, not an exact Köppen-Geiger implementation.

## Hydrology

The existing terrain-derived priority-flood drainage tree is preserved. Its monthly runoff inputs are recalculated from precipitation, snowfall/snowmelt, PET, soil infiltration, soil storage, slope and parent-rock permeability. Groundwater recharge is deducted from infiltration before the remaining share enters soil storage, and snowmelt is counted once as liquid input. Monthly discharge is routed through the existing downstream tree using equal 1/12-year month durations, so the mean of the twelve monthly discharge rates agrees with annual mean discharge. River permanence continues to use the existing discharge-month thresholds.

Groundwater potential is a relative proxy derived from recharge, soil infiltration and simplified parent-rock permeability. Freshwater access also accounts for mapped rivers and lakes nearby. Lake water balance is a simplified annual inflow/precipitation/evaporation proxy; discharge-to-depth conversion uses 31,536 mm·km² per (m³/s·year) before division by lake-cell area. Regional flood susceptibility is not a hydraulic inundation or flood-depth model.

## Geology and soil

Geological types and tectonic province IDs are deterministic spherical-noise proxies influenced by relief, elevation and a procedural tectonic-activity field. Geological stability and seismic risk are relative 0–100 indices.

Soil classes include sandy, clay, loamy, silt-rich, rocky, peaty, saline and shallow-mountain soils. Fertility, depth, infiltration rate, drainage, erosion risk and stability respond to parent rock, temperature, precipitation, slope and relief.

These layers are synthetic game data and must not be presented as actual mapped geology, soil surveys or building-code advice.

## Ecology and resources

Biomes and potential vegetation cover are derived from climate, aridity, water balance, elevation and flood/wetland conditions. Forest, grassland, desert and wetland coverage are separate numerical layers.

Resource abundance is a deterministic 0–100 potential. Iron, copper and gold are associated with selected igneous/metamorphic/volcanic and tectonic settings; coal, oil and gas with sedimentary/basin proxies; stone with parent rock and relief; timber with forest cover; freshwater with water access; fertile land with soil fertility and depth. Resource accessibility is stored separately and penalizes difficult terrain.

## Gameplay geography

- Construction suitability starts with the existing terrain/flood baseline and is adjusted by soil stability, geological stability, seismic risk, erosion and wetland coverage. Water cells are excluded.
- Agricultural suitability combines soil fertility/depth, P/PET, temperature, growing-season length, slope and elevation.
- Settlement suitability combines freshwater access, construction, agriculture, climate comfort and hazard exposure.
- Infantry, cavalry and vehicle movement costs use the existing terrain baseline plus vegetation, wetland, desert, heat and cold penalties. The legacy generic army layer remains the infantry-like baseline.

Roads, bridges, unit supply, crop-specific yield, ownership and building-specific engineering constraints are not included in this milestone.

## Data layout

Cell layers use `cellIndex = y * Width + x`. Monthly layers use:

`month * (Width * Height) + cellIndex`

Resource layers use:

`resourceIndex * (Width * Height) + cellIndex`

Longitude wrapping and pole-bounded latitude behavior continue to use the existing `WorldData` coordinate helpers. The climate, geology and resource noise fields are sampled in 3D spherical coordinates to avoid a longitude seam.

## Map modes

The existing map modes remain. New modes include monthly temperature, monthly precipitation, seasonal temperature range, precipitation seasonality, climate zones, growing season, monthly wind direction/speed, river discharge/permanence, groundwater potential, water availability, vegetation/forest/grassland/wetland coverage, soil types/fertility/infiltration, geological types/stability/seismic risk, mineral resources, agricultural suitability, settlement suitability and unit-specific movement. A month selector controls the monthly climate and wind maps.

The inspector exposes the selected cell's physical, climate, hydrological, soil, geological, ecological, resource and gameplay information. Water-only values are labeled N/A where appropriate.

## Determinism and tuning

Each noise subsystem uses a seed derived from `WorldData.Seed` and a documented integer offset. Changing the map mode does not regenerate world data. Tune climate parameters through the exported fields on `ProceduralWorldGenerator` in the Godot inspector. They are copied into a per-world `ClimateModelSettings` instance, normalized and retained by `WorldData`. Soil, geology and gameplay coefficients are currently centralized within their responsible generator classes.

## Known model limitations

- The atmosphere is a three-belt heuristic, not a fluid-dynamics model.
- Wind fields are broad prevailing-flow proxies; local storms, cyclones, fronts and daily weather are not simulated.
- Rainfall and rain shadows use finite upwind sampling rather than a full three-dimensional moisture-advection solver.
- PET is a temperature/radiation proxy, not Penman–Monteith.
- Soil is a strategic classification, not a pedogenesis or measured soil profile model.
- Tectonic provinces and seismic risk are synthetic proxies, not plate-tectonic reconstruction or probabilistic seismic hazard.
- Groundwater and lake balances are strategic proxies, not aquifer or lake hydrodynamics.
- Flood susceptibility is not flood depth, extent or return-period simulation.
- Biomes represent potential broad land cover, not species-level ecology or succession.
- Resource layers represent potential deposits, not verified reserves.
- Agricultural suitability is not crop yield or food production.
- Military movement does not yet include roads, bridges, supply, weather events or unit logistics.

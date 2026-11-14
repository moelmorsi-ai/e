# Follow-up Audit Fixes

This update applies a focused source audit to the integrated Procedural Geography Engine.

## Fixes
- The selected location's `Selected month` inspector line now updates immediately when the month selector changes.
- Climate moisture-fetch and orographic routines now sample the correct upwind side on the latitude axis.
- Orographic windward-gradient sampling uses the first valid upwind land cell.
- Groundwater recharge is split out of infiltration before soil storage is updated.
- Snowmelt is no longer counted twice in the runoff calculation.
- Water-availability values are cleared before a world recalculation.
- Lake inflow discharge is converted to annual depth using the correct unit factor.
- Monthly discharge uses equal 1/12-year intervals, consistent with annual mean discharge.
- Diagnostics now check annual/monthly totals and annual/monthly discharge consistency.

## Validation boundary
- C# source delimiter checks passed for all 14 C# files.
- A formula-level test of the revised soil-water bucket passed 10,000 synthetic cases.
- The project was **not compiled or run** in this environment because Godot and the .NET SDK/CLI are unavailable here.

## Required user-side validation
Open the project in Godot 4.7.2 .NET with .NET 8 installed, build it, run seed `123456`, and check the new `WORLD DIAGNOSTICS` output. Specifically test changing the month after selecting a location and inspect the new rainfall and lake-balance outputs.


## 3000 BC simulation foundation
- Added the BronzeAge simulation domain and first-pass systems for dynamic cell state, settlements, POP cohorts, agriculture, production, economy, trade, politics, culture, technology, diplomacy, rule-based rulers, military movement, events, infrastructure and persistence.
- Added an initial civilization seeder that creates independent chiefdoms and differentiated cultures from the generated geography.
- Added a Godot simulation runtime node to both main scene variants and initialized it from the generated geographical world.
- Added Tin as a resource and included it in the copper/tin/fuel bronze recipe.
- Changed hot desert to yellow-gold and cold desert to pale sand in the biome map; the main terrain map now overlays desert biomes in yellow.
- Compilation/runtime validation remains pending in Godot 4.7.2 .NET.

#nullable enable

using System;

/// <summary>
/// Coordinates the dependency-ordered generation of all derived geographical
/// layers. WorldData remains the single shared data source for every subsystem.
/// </summary>
public static class WorldGenerationPipeline
{
	public static void Calculate(WorldData world)
	{
		if (world == null)
			throw new ArgumentNullException(nameof(world));

		world.ResetDerivedLayerState();

		// 1. Physical geography must exist before any environmental calculations.
		world.CalculateCoastalLayer();
		world.CalculateLocalReliefLayer();
		world.CalculateSlopeAndAspectLayers();
		world.CalculateTerrainTypes();

		// 2. Parent geology is independent of climate; soils are not.
		GeologySoilGenerator.GenerateGeology(world);

		// 3. Climate supplies monthly temperature, wind, rainfall, PET and snow.
		ClimateGenerator.Calculate(world);

		// 4. Soil properties feed the monthly infiltration/runoff balance.
		GeologySoilGenerator.GenerateSoils(world);
		GeologySoilGenerator.CalculateSoilWaterBalance(world);

		// 5. Existing terrain-derived drainage is retained and now receives
		//    climate/soil runoff. The extensions calculate discharge diagnostics,
		//    groundwater potential and freshwater access from that same network.
		world.CalculateHydrologyLayer();
		HydrologyExtensionGenerator.Calculate(world);
		world.CalculateRegionalFloodSusceptibilityLayer();

		// 6. Gameplay's original terrain baselines are preserved, then enriched
		//    with soil, geology, ecological and seasonal effects.
		world.CalculateArmyMovementCostLayer();
		world.CalculateConstructionSuitabilityLayer();
		EcologyGenerator.Generate(world);
		NaturalResourceGenerator.Generate(world);
		GameplayGeographyGenerator.Calculate(world);

		world.MarkDerivedLayersCalculated();
	}
}

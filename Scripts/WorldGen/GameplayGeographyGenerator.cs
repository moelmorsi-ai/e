#nullable enable

using Godot;
using System;

/// <summary>
/// Converts the physical and environmental layers into strategic gameplay indices.
/// Scores are relative world-generation heuristics, not engineering certifications
/// or real-world land-use recommendations.
/// </summary>
public static class GameplayGeographyGenerator
{
	public static void Calculate(WorldData world)
	{
		if (world == null)
			throw new ArgumentNullException(nameof(world));

		for (int index = 0; index < world.Width * world.Height; index++)
		{
			if (!world.IsLand[index] || world.IsLake[index])
			{
				world.AgriculturalSuitability[index] = 0.0f;
				world.SettlementSuitability[index] = 0.0f;
				world.InfantryMovementCostMultiplier[index] = 0.0f;
				world.CavalryMovementCostMultiplier[index] = 0.0f;
				world.VehicleMovementCostMultiplier[index] = 0.0f;
				world.ArmyMovementCostMultiplier[index] = 0.0f;
				world.ConstructionSuitability[index] = 0.0f;
				continue;
			}

			float baseMovement = Mathf.Max(0.8f, world.ArmyMovementCostMultiplier[index]);
			float forest = world.ForestDensity[index] / 100.0f;
			float wetland = world.WetlandCoverage[index] / 100.0f;
			float desert = world.DesertCoverage[index] / 100.0f;
			float temperature = world.MeanAnnualTemperatureC[index];
			float heatStress = Mathf.Clamp((temperature - 30.0f) / 20.0f, 0.0f, 0.45f);
			float coldStress = Mathf.Clamp((-temperature - 5.0f) / 25.0f, 0.0f, 0.55f);
			float snowStress = temperature < 0.0f
				? Mathf.Clamp((0.0f - temperature) / 25.0f, 0.0f, 0.35f)
				: 0.0f;

			world.InfantryMovementCostMultiplier[index] = Mathf.Clamp(
				baseMovement * (1.0f + forest * 0.34f + wetland * 0.85f + desert * 0.16f + heatStress + coldStress),
				0.8f, 35.0f);
			world.CavalryMovementCostMultiplier[index] = Mathf.Clamp(
				baseMovement * (1.0f + forest * 0.72f + wetland * 1.05f + desert * 0.22f + heatStress * 1.1f + coldStress + snowStress),
				0.8f, 45.0f);
			world.VehicleMovementCostMultiplier[index] = Mathf.Clamp(
				baseMovement * (1.0f + forest * 0.92f + wetland * 1.35f + desert * 0.30f + heatStress + coldStress * 1.15f + snowStress),
				0.8f, 55.0f);

			// The existing generic army layer remains the infantry-like baseline so
			// older movement queries continue to work without an API migration.
			world.ArmyMovementCostMultiplier[index] = world.InfantryMovementCostMultiplier[index];

			float temperatureSuitability = Mathf.Clamp(1.0f - Mathf.Abs(temperature - 20.0f) / 29.0f, 0.0f, 1.0f);
			float waterSuitability = Mathf.Clamp(world.AridityIndex[index] / 0.82f, 0.0f, 1.0f);
			if (world.AridityIndex[index] > 2.2f)
				waterSuitability *= Mathf.Clamp(1.0f - (world.AridityIndex[index] - 2.2f) / 3.0f, 0.35f, 1.0f);
			float growingSeasonSuitability = Mathf.Clamp(world.GrowingSeasonMonths[index] / 10.0f, 0.0f, 1.0f);
			float soilSuitability = Mathf.Clamp(world.SoilFertility[index] / 100.0f, 0.0f, 1.0f) *
				Mathf.Clamp(world.SoilDepthMeters[index] / 1.1f, 0.25f, 1.0f);
			float slopeSuitability = 1.0f - Mathf.Clamp(world.SlopeDegrees[index] / 28.0f, 0.0f, 1.0f);
			float altitudeSuitability = 1.0f - Mathf.Clamp(Mathf.Max(0.0f, world.ElevationMeters[index] - 1800.0f) / 3500.0f, 0.0f, 0.75f);
			float agriculture = 100.0f * (
				soilSuitability * 0.34f + waterSuitability * 0.24f +
				temperatureSuitability * 0.18f + growingSeasonSuitability * 0.14f +
				slopeSuitability * 0.07f + altitudeSuitability * 0.03f);
			if (world.Biomes[index] == BiomeType.HotDesert || world.Biomes[index] == BiomeType.ColdDesert)
				agriculture *= 0.35f;
			if (world.Biomes[index] == BiomeType.Wetland)
				agriculture *= 0.72f;
			world.AgriculturalSuitability[index] = Mathf.Clamp(agriculture, 0.0f, 100.0f);

			float baseConstruction = world.ConstructionSuitability[index];
			float soilStabilityFactor = 0.55f + world.SoilStability[index] / 220.0f;
			float seismicPenalty = world.SeismicRisk[index] * 0.20f;
			float erosionPenalty = world.SoilErosionRisk[index] * 0.12f;
			float wetlandPenalty = wetland * 22.0f;
			float construction = baseConstruction * soilStabilityFactor - seismicPenalty - erosionPenalty - wetlandPenalty;
			world.ConstructionSuitability[index] = Mathf.Clamp(construction, 0.0f, 100.0f);

			float freshwater = world.FreshwaterAccess[index] / 100.0f;
			float agriculturalPotential = world.AgriculturalSuitability[index] / 100.0f;
			float constructionPotential = world.ConstructionSuitability[index] / 100.0f;
			float climateComfort = temperatureSuitability * 0.65f + waterSuitability * 0.35f;
			float hazardSafety = 1.0f - Mathf.Clamp(
				(world.RegionalFloodSusceptibility[index] * 0.55f + world.SeismicRisk[index] * 0.45f) / 100.0f,
				0.0f, 1.0f);
			float settlement = 100.0f * (
				freshwater * 0.27f + constructionPotential * 0.25f +
				agriculturalPotential * 0.20f + climateComfort * 0.13f +
				hazardSafety * 0.15f);
			if (world.IsCoastal[index])
				settlement += 3.0f;
			if (world.IsRiver[index])
				settlement += 4.0f;
			world.SettlementSuitability[index] = Mathf.Clamp(settlement, 0.0f, 100.0f);
		}
	}
}

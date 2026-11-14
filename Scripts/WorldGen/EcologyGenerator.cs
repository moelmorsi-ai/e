#nullable enable

using Godot;
using System;

/// <summary>
/// Generates potential biome and vegetation layers from climate, water balance,
/// soils, relief and hydrological conditions. It models broad land-cover potential,
/// not individual plants or ecological succession.
/// </summary>
public static class EcologyGenerator
{
	public static void Generate(WorldData world)
	{
		if (world == null)
			throw new ArgumentNullException(nameof(world));

		for (int index = 0; index < world.Width * world.Height; index++)
		{
			if (!world.IsLand[index] || world.IsLake[index])
			{
				world.Biomes[index] = BiomeType.Ocean;
				world.VegetationTypes[index] = VegetationType.None;
				world.VegetationDensity[index] = 0.0f;
				world.ForestDensity[index] = 0.0f;
				world.GrasslandCoverage[index] = 0.0f;
				world.DesertCoverage[index] = 0.0f;
				world.WetlandCoverage[index] = 0.0f;
				world.VegetationWaterDemand[index] = 0.0f;
				continue;
			}

			float temperature = world.MeanAnnualTemperatureC[index];
			float precipitation = world.AnnualPrecipitationMm[index];
			float aridity = world.AridityIndex[index];
			float elevation = world.ElevationMeters[index];
			float slope = world.SlopeDegrees[index];
			float runoff = world.AnnualRunoffMm[index];
			float soilDrainage = world.SoilDrainage[index];
			float moisture = Mathf.Clamp(aridity / 1.15f, 0.0f, 1.0f);
			float thermal = Mathf.Clamp((temperature + 12.0f) / 32.0f, 0.0f, 1.0f);

			float wetlandPotential =
				Mathf.Clamp(runoff / 500.0f, 0.0f, 1.0f) * 0.28f +
				Mathf.Clamp(world.RegionalFloodSusceptibility[index] / 100.0f, 0.0f, 1.0f) * 0.24f +
				Mathf.Clamp(precipitation / 1800.0f, 0.0f, 1.0f) * 0.22f +
				(1.0f - Mathf.Clamp(soilDrainage / 100.0f, 0.0f, 1.0f)) * 0.26f;
			if (slope > 5.0f || elevation > 1200.0f || aridity < 0.25f)
				wetlandPotential *= 0.35f;

			BiomeType biome = ClassifyBiome(world, index, wetlandPotential);
			world.Biomes[index] = biome;

			VegetationType vegetation = biome switch
			{
				BiomeType.TropicalRainforest => VegetationType.TropicalForest,
				BiomeType.TropicalSeasonalForest => VegetationType.SeasonalForest,
				BiomeType.Savanna => VegetationType.Savanna,
				BiomeType.HotDesert => VegetationType.DesertScrub,
				BiomeType.ColdDesert => VegetationType.DesertScrub,
				BiomeType.Steppe => VegetationType.Grassland,
				BiomeType.Mediterranean => VegetationType.Shrubland,
				BiomeType.TemperateGrassland => VegetationType.Grassland,
				BiomeType.TemperateForest => VegetationType.TemperateForest,
				BiomeType.BorealForest => VegetationType.BorealForest,
				BiomeType.Tundra => VegetationType.Tundra,
				BiomeType.Alpine => VegetationType.Alpine,
				BiomeType.Wetland => VegetationType.Wetland,
				_ => VegetationType.None
			};
			world.VegetationTypes[index] = vegetation;

			float vegetationDensity = Mathf.Clamp(
				(0.10f + moisture * 0.68f + thermal * 0.22f) * 100.0f,
				0.0f, 100.0f);
			if (biome == BiomeType.HotDesert || biome == BiomeType.ColdDesert)
				vegetationDensity *= 0.12f;
			else if (biome == BiomeType.Steppe || biome == BiomeType.Savanna)
				vegetationDensity *= 0.62f;
			else if (biome == BiomeType.Tundra || biome == BiomeType.Alpine)
				vegetationDensity *= 0.35f;
			else if (biome == BiomeType.Wetland)
				vegetationDensity = Mathf.Max(vegetationDensity, 72.0f);
			world.VegetationDensity[index] = Mathf.Clamp(vegetationDensity, 0.0f, 100.0f);

			float forestDensity = biome switch
			{
				BiomeType.TropicalRainforest => 88.0f + moisture * 12.0f,
				BiomeType.TropicalSeasonalForest => 62.0f + moisture * 18.0f,
				BiomeType.TemperateForest => 72.0f,
				BiomeType.BorealForest => 58.0f,
				BiomeType.Wetland => 34.0f,
				BiomeType.Mediterranean => 28.0f,
				_ => 0.0f
			};
			world.ForestDensity[index] = Mathf.Clamp(forestDensity, 0.0f, 100.0f);

			float grassland = biome switch
			{
				BiomeType.Savanna => 72.0f,
				BiomeType.Steppe => 84.0f,
				BiomeType.TemperateGrassland => 92.0f,
				BiomeType.Tundra => 22.0f,
				BiomeType.Mediterranean => 34.0f,
				_ => 0.0f
			};
			world.GrasslandCoverage[index] = grassland;

			float desert = biome == BiomeType.HotDesert || biome == BiomeType.ColdDesert
				? Mathf.Clamp(88.0f + (0.20f - aridity) * 50.0f, 0.0f, 100.0f)
				: biome == BiomeType.Steppe ? 24.0f : 0.0f;
			world.DesertCoverage[index] = desert;
			world.WetlandCoverage[index] = biome == BiomeType.Wetland
				? Mathf.Clamp(wetlandPotential * 100.0f, 55.0f, 100.0f)
				: 0.0f;

			world.VegetationWaterDemand[index] = vegetation switch
			{
				VegetationType.TropicalForest => 0.95f,
				VegetationType.SeasonalForest => 0.78f,
				VegetationType.TemperateForest => 0.70f,
				VegetationType.BorealForest => 0.48f,
				VegetationType.Wetland => 0.90f,
				VegetationType.Savanna => 0.52f,
				VegetationType.Grassland => 0.42f,
				VegetationType.Shrubland => 0.32f,
				VegetationType.DesertScrub => 0.12f,
				_ => 0.18f
			};

			// Potential ground cover reduces the exposure of soil to rainfall and runoff.
			world.SoilErosionRisk[index] = Mathf.Clamp(
				world.SoilErosionRisk[index] * (1.0f - world.VegetationDensity[index] / 100.0f * 0.48f),
				0.0f, 100.0f);
			world.SoilStability[index] = Mathf.Clamp(
				world.SoilStability[index] * 0.85f + (100.0f - world.SoilErosionRisk[index]) * 0.15f,
				0.0f, 100.0f);
		}
	}

	private static BiomeType ClassifyBiome(WorldData world, int index, float wetlandPotential)
	{
		float temperature = world.MeanAnnualTemperatureC[index];
		float precipitation = world.AnnualPrecipitationMm[index];
		float aridity = world.AridityIndex[index];
		float elevation = world.ElevationMeters[index];
		ClimateZoneType climate = world.ClimateZones[index];

		if (wetlandPotential >= 0.72f && world.SlopeDegrees[index] < 3.5f && precipitation > 650.0f)
			return BiomeType.Wetland;
		if (elevation >= 4200.0f || climate == ClimateZoneType.Highland && temperature < 5.0f)
			return BiomeType.Alpine;
		if (temperature < -5.0f || climate == ClimateZoneType.Polar)
			return BiomeType.Tundra;
		if (aridity < 0.20f || precipitation < 180.0f)
			return temperature < 5.0f ? BiomeType.ColdDesert : BiomeType.HotDesert;
		if (aridity < 0.50f || precipitation < 450.0f)
			return BiomeType.Steppe;

		float wettest = 0.0f;
		float driest = float.MaxValue;
		for (int month = 0; month < WorldData.MonthsPerYear; month++)
		{
			float monthly = world.MonthlyPrecipitationMm[world.GetMonthlyIndex(index, month)];
			wettest = Mathf.Max(wettest, monthly);
			driest = Mathf.Min(driest, monthly);
		}
		float seasonality = wettest / Mathf.Max(1.0f, driest);

		if (climate == ClimateZoneType.Tropical || Mathf.Abs(GetLatitude(world, index)) < 23.5f && temperature >= 18.0f)
		{
			if (precipitation >= 1700.0f && seasonality < 3.0f)
				return BiomeType.TropicalRainforest;
			if (seasonality >= 3.0f || precipitation < 1050.0f)
				return BiomeType.Savanna;
			return BiomeType.TropicalSeasonalForest;
		}

		if (climate == ClimateZoneType.Mediterranean)
			return BiomeType.Mediterranean;
		if (climate == ClimateZoneType.Subpolar)
			return temperature < 2.0f ? BiomeType.Tundra : BiomeType.BorealForest;
		if (climate == ClimateZoneType.TemperateContinental || climate == ClimateZoneType.TemperateOceanic)
			return precipitation >= 700.0f ? BiomeType.TemperateForest : BiomeType.TemperateGrassland;
		return precipitation >= 700.0f ? BiomeType.TemperateForest : BiomeType.TemperateGrassland;
	}

	private static float GetLatitude(WorldData world, int index)
	{
		int y = index / world.Width;
		return 90.0f - ((y + 0.5f) / world.Height) * 180.0f;
	}
}

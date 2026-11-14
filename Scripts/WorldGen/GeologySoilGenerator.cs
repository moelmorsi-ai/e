#nullable enable

using Godot;
using System;

/// <summary>
/// Deterministic, game-scale geology and soil generation. The output represents
/// plausible procedural provinces and relative properties, not mapped real-world
/// lithology, soil surveys or measured geotechnical values.
/// </summary>
public static class GeologySoilGenerator
{
	private static FastNoiseLite CreateNoise(int seed, float frequency)
	{
		FastNoiseLite noise = new FastNoiseLite();
		noise.Seed = seed;
		noise.NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth;
		noise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
		noise.FractalOctaves = 3;
		noise.FractalLacunarity = 2.0f;
		noise.FractalGain = 0.5f;
		noise.Frequency = frequency;
		return noise;
	}

	public static void GenerateGeology(WorldData world)
	{
		FastNoiseLite provinceNoise = CreateNoise(world.Seed + 3109, 0.85f);
		FastNoiseLite tectonicNoise = CreateNoise(world.Seed + 7919, 2.2f);
		FastNoiseLite lithologyNoise = CreateNoise(world.Seed + 104729, 4.5f);

		for (int y = 0; y < world.Height; y++)
		{
			float latitude = Mathf.Pi * (0.5f - ((y + 0.5f) / world.Height));
			float cosLatitude = Mathf.Cos(latitude);
			for (int x = 0; x < world.Width; x++)
			{
				int index = world.GetIndex(x, y);
				if (!world.IsLand[index])
				{
					world.RockTypes[index] = RockType.None;
					world.TectonicProvinceIds[index] = -1;
					world.GeologicalStability[index] = 0.0f;
					world.SeismicRisk[index] = 0.0f;
					continue;
				}

				float longitude = (Mathf.Tau * ((x + 0.5f) / world.Width)) - Mathf.Pi;
				float sx = cosLatitude * Mathf.Cos(longitude);
				float sy = Mathf.Sin(latitude);
				float sz = cosLatitude * Mathf.Sin(longitude);

				float province = provinceNoise.GetNoise3D(sx, sy, sz);
				float tectonic = Mathf.Abs(tectonicNoise.GetNoise3D(sx, sy, sz));
				float lithology = lithologyNoise.GetNoise3D(sx, sy, sz);
				int provinceId = Mathf.Clamp(Mathf.FloorToInt((province + 1.0f) * 3.0f), 0, 5);
				world.TectonicProvinceIds[index] = provinceId;

				float elevation = world.ElevationMeters[index];
				float relief = world.LocalReliefMeters[index];
				float slope = world.SlopeDegrees[index];
				RockType rock;

				if (elevation < 180.0f && world.IsCoastal[index])
					rock = RockType.Alluvial;
				else if (tectonic < 0.16f && relief < 180.0f && slope < 4.0f)
					rock = RockType.Cratonic;
				else if (tectonic > 0.68f && lithology > 0.28f)
					rock = RockType.Volcanic;
				else if (relief > 300.0f || slope > 12.0f)
					rock = tectonic > 0.42f ? RockType.Metamorphic : RockType.Igneous;
				else if (lithology < -0.12f || elevation < 900.0f)
					rock = RockType.Sedimentary;
				else
					rock = RockType.Igneous;

				world.RockTypes[index] = rock;

				float tectonicActivity = Mathf.Clamp(1.0f - tectonic, 0.0f, 1.0f);
				float ruggedness = Mathf.Clamp(relief / 900.0f, 0.0f, 1.0f);
				float slopeFactor = Mathf.Clamp(slope / 35.0f, 0.0f, 1.0f);
				float stability = 96.0f - tectonicActivity * 38.0f - ruggedness * 22.0f - slopeFactor * 12.0f;
				if (rock == RockType.Volcanic)
					stability -= 10.0f;
				if (rock == RockType.Cratonic)
					stability += 4.0f;

				world.GeologicalStability[index] = Mathf.Clamp(stability, 8.0f, 100.0f);
				world.SeismicRisk[index] = Mathf.Clamp(100.0f - world.GeologicalStability[index], 0.0f, 100.0f);
			}
		}
	}

	public static void GenerateSoils(WorldData world)
	{
		FastNoiseLite soilTextureNoise = CreateNoise(world.Seed + 65537, 6.0f);

		for (int y = 0; y < world.Height; y++)
		{
			float latitude = Mathf.Pi * (0.5f - ((y + 0.5f) / world.Height));
			float cosLatitude = Mathf.Cos(latitude);
			for (int x = 0; x < world.Width; x++)
			{
				int index = world.GetIndex(x, y);
				if (!world.IsLand[index])
				{
					world.SoilTypes[index] = SoilType.None;
					world.SoilFertility[index] = 0.0f;
					world.SoilDepthMeters[index] = 0.0f;
					world.SoilInfiltrationRateMmPerHour[index] = 0.0f;
					world.SoilDrainage[index] = 0.0f;
					world.SoilErosionRisk[index] = 0.0f;
					world.SoilStability[index] = 0.0f;
					continue;
				}

				float longitude = (Mathf.Tau * ((x + 0.5f) / world.Width)) - Mathf.Pi;
				float sx = cosLatitude * Mathf.Cos(longitude);
				float sy = Mathf.Sin(latitude);
				float sz = cosLatitude * Mathf.Sin(longitude);
				float texture = soilTextureNoise.GetNoise3D(sx, sy, sz);

				float elevation = world.ElevationMeters[index];
				float slope = world.SlopeDegrees[index];
				float rainfall = world.AnnualPrecipitationMm[index];
				float aridity = world.AridityIndex[index];
				float temperature = world.MeanAnnualTemperatureC[index];
				RockType rock = world.RockTypes[index];

				SoilType soil;
				if (slope >= 18.0f || elevation >= 2200.0f)
					soil = SoilType.ShallowMountain;
				else if (slope >= 10.0f || world.LocalReliefMeters[index] >= 500.0f)
					soil = SoilType.Rocky;
				else if (aridity < 0.16f && elevation < 500.0f && (world.IsCoastal[index] || texture > 0.35f))
					soil = SoilType.Saline;
				else if (rainfall > 1300.0f && slope < 2.0f && texture < -0.15f)
					soil = SoilType.Peaty;
				else if (aridity < 0.30f || texture > 0.38f)
					soil = SoilType.Sandy;
				else if (rainfall > 850.0f && slope < 3.0f && texture < -0.35f)
					soil = SoilType.Clay;
				else if (rainfall > 650.0f && slope < 5.0f)
					soil = SoilType.SiltRich;
				else
					soil = SoilType.Loamy;

				world.SoilTypes[index] = soil;

				float baseFertility = soil switch
				{
					SoilType.Loamy => 76.0f,
					SoilType.SiltRich => 82.0f,
					SoilType.Clay => 66.0f,
					SoilType.Peaty => 70.0f,
					SoilType.Sandy => 36.0f,
					SoilType.Rocky => 28.0f,
					SoilType.Saline => 18.0f,
					SoilType.ShallowMountain => 22.0f,
					_ => 0.0f
				};
				float moistureFactor = Mathf.Clamp(0.42f + rainfall / 2200.0f, 0.35f, 1.0f);
				float thermalFactor = Mathf.Clamp((temperature + 8.0f) / 28.0f, 0.15f, 1.0f);
				float slopePenalty = Mathf.Clamp(slope * 1.35f, 0.0f, 45.0f);
				float parentMaterialBonus = rock == RockType.Alluvial || rock == RockType.Volcanic ? 7.0f : 0.0f;
				world.SoilFertility[index] = Mathf.Clamp(baseFertility * moistureFactor * thermalFactor + parentMaterialBonus - slopePenalty, 0.0f, 100.0f);

				float depth = soil switch
				{
					SoilType.ShallowMountain => 0.18f,
					SoilType.Rocky => 0.35f,
					SoilType.Sandy => 0.85f,
					SoilType.Clay => 1.05f,
					SoilType.SiltRich => 1.45f,
					SoilType.Peaty => 1.65f,
					SoilType.Saline => 0.55f,
					_ => 1.30f
				};
				world.SoilDepthMeters[index] = Mathf.Clamp(depth * (1.0f - Mathf.Clamp(slope / 55.0f, 0.0f, 0.65f)), 0.10f, 2.2f);

				float infiltration = soil switch
				{
					SoilType.Sandy => 38.0f,
					SoilType.Rocky => 25.0f,
					SoilType.ShallowMountain => 21.0f,
					SoilType.Loamy => 17.0f,
					SoilType.SiltRich => 11.0f,
					SoilType.Peaty => 9.0f,
					SoilType.Clay => 5.0f,
					SoilType.Saline => 6.0f,
					_ => 12.0f
				};
				world.SoilInfiltrationRateMmPerHour[index] = Mathf.Clamp(infiltration * (1.0f + texture * 0.10f), 2.0f, 55.0f);

				float drainage = Mathf.Clamp(
					world.SoilInfiltrationRateMmPerHour[index] * 1.6f + slope * 1.8f - (soil == SoilType.Peaty ? 25.0f : 0.0f),
					0.0f, 100.0f);
				world.SoilDrainage[index] = drainage;

				float rainfallErosivity = Mathf.Clamp(rainfall / 1800.0f, 0.0f, 1.0f);
				float slopeErosivity = Mathf.Clamp(slope / 30.0f, 0.0f, 1.0f);
				world.SoilErosionRisk[index] = Mathf.Clamp(100.0f * (0.15f + rainfallErosivity * 0.45f + slopeErosivity * 0.55f), 0.0f, 100.0f);
				world.SoilStability[index] = Mathf.Clamp(
					world.GeologicalStability[index] * 0.62f + (100.0f - slopeErosivity * 100.0f) * 0.28f + (100.0f - world.SoilErosionRisk[index]) * 0.10f,
					0.0f, 100.0f);
			}
		}
	}

	/// <summary>
	/// Replaces the climate-only runoff estimate with a soil-aware monthly bucket
	/// balance. Runoff, actual evapotranspiration and recharge are all expressed
	/// as millimetres over the cell per month/year.
	/// </summary>
	public static void CalculateSoilWaterBalance(WorldData world)
	{
		int size = world.Width * world.Height;
		Array.Clear(world.AnnualRunoffMm, 0, size);
		Array.Clear(world.AnnualActualEvapotranspirationMm, 0, size);
		Array.Clear(world.AnnualGroundwaterRechargeMm, 0, size);
		Array.Clear(world.WaterAvailabilityMm, 0, size);
		Array.Clear(world.MonthlyRunoffMm, 0, world.MonthlyRunoffMm.Length);
		Array.Clear(world.MonthlyActualEvapotranspirationMm, 0, world.MonthlyActualEvapotranspirationMm.Length);
		Array.Clear(world.MonthlyGroundwaterRechargeMm, 0, world.MonthlyGroundwaterRechargeMm.Length);

		for (int index = 0; index < size; index++)
		{
			if (!world.IsLand[index])
				continue;

			float soilCapacity = Mathf.Clamp(world.SoilDepthMeters[index] * 115.0f, 20.0f, 240.0f);
			float soilStorage = soilCapacity * 0.45f;
			float infiltrationFraction = Mathf.Clamp(world.SoilInfiltrationRateMmPerHour[index] / 42.0f, 0.08f, 0.90f);
			float slopeRunoff = Mathf.Clamp(world.SlopeDegrees[index] / 38.0f, 0.0f, 0.72f);
			float annualRunoff = 0.0f;
			float annualActualEt = 0.0f;
			float annualRecharge = 0.0f;

			for (int month = 0; month < WorldData.MonthsPerYear; month++)
			{
				int monthlyIndex = world.GetMonthlyIndex(index, month);
				float precipitation = world.MonthlyPrecipitationMm[monthlyIndex];
				float snowfall = world.MonthlySnowfallMm[monthlyIndex];
				float snowmelt = world.MonthlySnowmeltMm[monthlyIndex];
				float pet = world.MonthlyPotentialEvapotranspirationMm[monthlyIndex];
				float liquidInput = Mathf.Max(0.0f, precipitation - snowfall) + snowmelt;

				float soilRelease = Mathf.Min(soilStorage * 0.18f, pet * 0.35f);
				float availableForEvaporation = liquidInput + soilRelease;
				float actualEt = Mathf.Min(pet, availableForEvaporation);
				float surplus = Mathf.Max(0.0f, availableForEvaporation - actualEt);

				float effectiveInfiltration = infiltrationFraction * (1.0f - slopeRunoff * 0.55f);
				float infiltration = surplus * effectiveInfiltration;
				float surfaceRunoff = surplus - infiltration;
				float permeability = world.RockTypes[index] switch
				{
					RockType.Sedimentary => 0.72f,
					RockType.Alluvial => 0.82f,
					RockType.Cratonic => 0.28f,
					RockType.Igneous => 0.42f,
					RockType.Metamorphic => 0.35f,
					RockType.Volcanic => 0.58f,
					_ => 0.35f
				};

				// Recharge is a share of infiltrated water, not an additional input.
				// Remove it from the soil bucket so the same water is not counted
				// simultaneously as soil storage and groundwater recharge.
				float recharge = infiltration * (0.08f + permeability * 0.30f);
				float soilInfiltration = Mathf.Max(0.0f, infiltration - recharge);
				float storageBeforeOverflow = Mathf.Max(0.0f, soilStorage - soilRelease) + soilInfiltration;
				float overflow = Mathf.Max(0.0f, storageBeforeOverflow - soilCapacity);
				soilStorage = Mathf.Clamp(storageBeforeOverflow - overflow, 0.0f, soilCapacity);

				// Snowmelt is already included in liquidInput above; adding it again
				// here would double-count the same water as runoff.
				float runoff = surfaceRunoff + overflow;

				world.MonthlyActualEvapotranspirationMm[monthlyIndex] = actualEt;
				world.MonthlyRunoffMm[monthlyIndex] = runoff;
				world.MonthlyGroundwaterRechargeMm[monthlyIndex] = recharge;
				annualRunoff += runoff;
				annualActualEt += actualEt;
				annualRecharge += recharge;
			}

			world.AnnualRunoffMm[index] = annualRunoff;
			world.AnnualActualEvapotranspirationMm[index] = annualActualEt;
			world.AnnualGroundwaterRechargeMm[index] = annualRecharge;
			world.WaterAvailabilityMm[index] = Mathf.Max(0.0f,
				world.AnnualPrecipitationMm[index] - annualActualEt + annualRecharge);
		}
	}
}

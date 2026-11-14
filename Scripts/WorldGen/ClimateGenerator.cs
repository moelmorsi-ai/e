#nullable enable

using Godot;
using System;

/// <summary>
/// Strategic-scale, deterministic climate generator. This is a climatological
/// approximation for a game world, not a numerical weather prediction model.
/// It combines latitude/seasonal insolation, elevation lapse rate, continentality,
/// moving pressure and wind belts, ocean moisture fetch, orographic uplift and
/// leeward rain-shadow reduction.
/// </summary>
public static class ClimateGenerator
{
	private const double TwoPi = Math.PI * 2.0;
	private const int MoistureFetchSteps = 14;

	public static void Calculate(WorldData world)
	{
		if (world == null)
			throw new ArgumentNullException(nameof(world));

		int size = world.Width * world.Height;
		int[] coastDistance = CalculateCoastDistance(world);

		Array.Clear(world.MonthlyTemperatureC, 0, world.MonthlyTemperatureC.Length);
		Array.Clear(world.MonthlyPrecipitationMm, 0, world.MonthlyPrecipitationMm.Length);
		Array.Clear(world.MonthlySnowfallMm, 0, world.MonthlySnowfallMm.Length);
		Array.Clear(world.MonthlySnowmeltMm, 0, world.MonthlySnowmeltMm.Length);
		Array.Clear(world.MonthlyRunoffMm, 0, world.MonthlyRunoffMm.Length);
		Array.Clear(world.MonthlyPotentialEvapotranspirationMm, 0, world.MonthlyPotentialEvapotranspirationMm.Length);
		Array.Clear(world.MonthlyActualEvapotranspirationMm, 0, world.MonthlyActualEvapotranspirationMm.Length);
		Array.Clear(world.MonthlyWindDirectionDegrees, 0, world.MonthlyWindDirectionDegrees.Length);
		Array.Clear(world.MonthlyWindSpeedMps, 0, world.MonthlyWindSpeedMps.Length);
		Array.Clear(world.MonthlyConvectivePrecipitationMm, 0, world.MonthlyConvectivePrecipitationMm.Length);
		Array.Clear(world.MonthlyOrographicPrecipitationMm, 0, world.MonthlyOrographicPrecipitationMm.Length);
		Array.Clear(world.MonthlyRainShadowReductionMm, 0, world.MonthlyRainShadowReductionMm.Length);
		Array.Clear(world.AnnualPrecipitationMm, 0, size);
		Array.Clear(world.AnnualPotentialEvapotranspirationMm, 0, size);
		Array.Clear(world.AnnualActualEvapotranspirationMm, 0, size);
		Array.Clear(world.AnnualRunoffMm, 0, size);
		Array.Clear(world.TemperatureRangeC, 0, size);
		Array.Clear(world.PrecipitationSeasonalityIndex, 0, size);
		Array.Clear(world.GrowingSeasonMonths, 0, size);

		CalculateAtmosphericCirculation(world);
		CalculateSeasonalTemperature(world, coastDistance);
		CalculatePrecipitation(world, coastDistance);
		CalculateClimateClassification(world);

		world.ClimateCalculated = true;
		GD.Print("CLIMATE GENERATED");
		GD.Print($"Annual precipitation over land: {GetMinLand(world.AnnualPrecipitationMm, world.IsLand):F0}–{GetMaxLand(world.AnnualPrecipitationMm, world.IsLand):F0} mm");
		GD.Print($"Annual mean temperature over land: {GetMinLand(world.MeanAnnualTemperatureC, world.IsLand):F1}–{GetMaxLand(world.MeanAnnualTemperatureC, world.IsLand):F1} °C");
	}

	// A simple three-cell atmospheric circulation representation. Pressure belts
	// and the ITCZ move seasonally; the hemisphere determines the wind direction.
	private static void CalculateAtmosphericCirculation(WorldData world)
	{
		for (int month = 0; month < WorldData.MonthsPerYear; month++)
		{
			float phase = (float)(TwoPi * (month - 6.0) / 12.0);
			world.MonthlyITCZLatitudeDegrees[month] = world.ClimateSettings.ITCZSeasonalAmplitudeDegrees * Mathf.Cos(phase);
		}

		for (int y = 0; y < world.Height; y++)
		{
			float latitude = GetLatitudeDegrees(y, world.Height);
			float absoluteLatitude = Mathf.Abs(latitude);
			PressureBeltType pressure = absoluteLatitude < 12.0f
				? PressureBeltType.EquatorialLow
				: absoluteLatitude < 35.0f
					? PressureBeltType.SubtropicalHigh
					: absoluteLatitude < 65.0f
						? PressureBeltType.MidLatitudeLow
						: PressureBeltType.PolarHigh;

			for (int x = 0; x < world.Width; x++)
			{
				int index = world.GetIndex(x, y);
				world.PressureBelts[index] = pressure;
				Vector2 annualWind = Vector2.Zero;

				for (int month = 0; month < WorldData.MonthsPerYear; month++)
				{
					int monthlyIndex = world.GetMonthlyIndex(index, month);
					float direction = CalculateSeasonalWindDirection(latitude, month, world.ClimateSettings.ITCZSeasonalAmplitudeDegrees);
					float speed = CalculateWindStrength(latitude, month);
					world.MonthlyWindDirectionDegrees[monthlyIndex] = direction;
					world.MonthlyWindSpeedMps[monthlyIndex] = speed;

					float radians = direction * Mathf.Pi / 180.0f;
					annualWind += new Vector2(Mathf.Sin(radians), Mathf.Cos(radians));
				}

				if (annualWind.LengthSquared() > 0.0001f)
				{
					float direction = Mathf.Atan2(annualWind.X, annualWind.Y) * 180.0f / Mathf.Pi;
					world.PrevailingWindDirectionDegrees[index] = direction < 0.0f ? direction + 360.0f : direction;
				}
				else
				{
					world.PrevailingWindDirectionDegrees[index] = CalculateSeasonalWindDirection(latitude, 6, world.ClimateSettings.ITCZSeasonalAmplitudeDegrees);
				}
			}
		}
	}

	private static float CalculateSeasonalWindDirection(float latitude, int month, float itczAmplitudeDegrees)
	{
		float itczShift = itczAmplitudeDegrees * Mathf.Cos((float)(TwoPi * (month - 6.0) / 12.0));
		float effectiveLatitude = latitude - itczShift * 0.22f;
		float absoluteLatitude = Mathf.Abs(effectiveLatitude);
		float hemisphere = latitude >= 0.0f ? 1.0f : -1.0f;
		float east;
		float north;

		if (absoluteLatitude < 30.0f)
		{
			// NE trades in the north, SE trades in the south: both flow westward.
			east = -1.0f;
			north = -hemisphere * 0.35f;
		}
		else if (absoluteLatitude < 60.0f)
		{
			// Mid-latitude westerlies.
			east = 1.0f;
			north = hemisphere * 0.30f;
		}
		else
		{
			// Polar easterlies.
			east = -1.0f;
			north = hemisphere * 0.20f;
		}

		float degrees = Mathf.Atan2(east, north) * 180.0f / Mathf.Pi;
		return degrees < 0.0f ? degrees + 360.0f : degrees;
	}

	private static float CalculateWindStrength(float latitude, int month)
	{
		float absoluteLatitude = Mathf.Abs(latitude);
		float seasonal = 0.7f + 0.3f * Mathf.Abs(Mathf.Cos((float)(TwoPi * (month - 6.0) / 12.0)));
		float subtropicalJet = Gaussian(absoluteLatitude - 30.0f, 13.0f) * 5.0f;
		float westerlyJet = Gaussian(absoluteLatitude - 52.0f, 14.0f) * 4.0f;
		float equatorialCalm = Gaussian(absoluteLatitude, 8.0f) * 2.0f;
		return Mathf.Clamp((3.0f + subtropicalJet + westerlyJet - equatorialCalm) * seasonal, 1.0f, 16.0f);
	}

	private static void CalculateSeasonalTemperature(WorldData world, int[] coastDistance)
	{
		for (int y = 0; y < world.Height; y++)
		{
			float latitude = GetLatitudeDegrees(y, world.Height);
			float meanCellKm = GetMeanCellSizeKm(world, y);

			for (int x = 0; x < world.Width; x++)
			{
				int index = world.GetIndex(x, y);
				float coastDistanceKm = world.IsLand[index] ? coastDistance[index] * meanCellKm : 0.0f;
				float continentality = world.IsLand[index]
					? Mathf.Clamp(coastDistanceKm / 2500.0f, 0.0f, 1.0f)
					: 0.0f;
				float elevation = world.IsLand[index] ? world.ElevationMeters[index] : 0.0f;
				float mean = CalculateAnnualMeanTemperature(latitude, elevation, continentality, world.ClimateSettings);
				world.MeanAnnualTemperatureC[index] = mean;

				float minimum = float.MaxValue;
				float maximum = float.MinValue;
				for (int month = 0; month < WorldData.MonthsPerYear; month++)
				{
					int monthlyIndex = world.GetMonthlyIndex(index, month);
					float temperature = CalculateMonthlyTemperature(latitude, elevation, continentality, month, world.ClimateSettings);
					world.MonthlyTemperatureC[monthlyIndex] = temperature;
					minimum = Mathf.Min(minimum, temperature);
					maximum = Mathf.Max(maximum, temperature);
					if (temperature >= 5.0f)
						world.GrowingSeasonMonths[index]++;
				}

				world.TemperatureRangeC[index] = maximum - minimum;
			}
		}
	}

	private static float CalculateMonthlyTemperature(float latitude, float elevationMeters, float continentality, int month, ClimateModelSettings settings)
	{
		float mean = CalculateAnnualMeanTemperature(latitude, elevationMeters, continentality, settings);
		float absoluteLatitude = Mathf.Abs(latitude) / 90.0f;
		float seasonalAmplitude = 1.5f + CalculateContinentalityEffect(latitude, continentality);
		float peakMonth = latitude >= 0.0f ? 6.0f : 0.0f;
		float solarPhase = (float)(TwoPi * (month - peakMonth) / 12.0);
		return mean + seasonalAmplitude * Mathf.Cos(solarPhase);
	}

	private static float CalculateAnnualMeanTemperature(float latitude, float elevationMeters, float continentality, ClimateModelSettings settings)
	{
		float absoluteLatitude = Mathf.Abs(latitude);
		float latitudinalTemperature = 30.0f - 0.36f * absoluteLatitude - 0.0010f * absoluteLatitude * absoluteLatitude;
		float elevationCooling = CalculateElevationTemperatureEffect(elevationMeters, settings.EnvironmentalLapseRateCPerKm);
		float oceanModeration = CalculateOceanModeration(continentality);
		// Ocean moderation chiefly reduces seasonal extremes. A small annual-mean
		// correction avoids treating a maritime coast exactly like an inland cell.
		return latitudinalTemperature - elevationCooling + oceanModeration;
	}

	private static float CalculateElevationTemperatureEffect(float elevationMeters, float lapseRateCPerKm)
	{
		return Mathf.Max(0.0f, elevationMeters) / 1000.0f * lapseRateCPerKm;
	}

	private static float CalculateContinentalityEffect(float latitude, float continentality)
	{
		return Mathf.Pow(Mathf.Abs(latitude) / 90.0f, 1.35f) * (8.0f + 18.0f * continentality);
	}

	private static float CalculateOceanModeration(float continentality)
	{
		return (1.0f - Mathf.Clamp(continentality, 0.0f, 1.0f)) * 0.65f;
	}

	private static void CalculatePrecipitation(WorldData world, int[] coastDistance)
	{
		int size = world.Width * world.Height;
		for (int y = 0; y < world.Height; y++)
		{
			float latitude = GetLatitudeDegrees(y, world.Height);
			for (int x = 0; x < world.Width; x++)
			{
				int index = world.GetIndex(x, y);
				float annualPrecipitation = 0.0f;
				float annualPet = 0.0f;
				float wettest = float.MinValue;
				float driest = float.MaxValue;
				int wettestMonth = 0;
				int driestMonth = 0;

				for (int month = 0; month < WorldData.MonthsPerYear; month++)
				{
					int monthlyIndex = world.GetMonthlyIndex(index, month);
					float temperature = world.MonthlyTemperatureC[monthlyIndex];
					float precipitation;
					float convective;
					float orographic;
					float rainShadow;

					if (world.IsLand[index])
					{
						CalculateMonthlyPrecipitation(
							world, x, y, index, latitude, month, coastDistance[index],
							out precipitation, out convective, out orographic, out rainShadow);
					}
					else
				{
					precipitation = CalculateOceanPrecipitation(world, latitude, month);
					convective = precipitation * 0.68f;
					orographic = 0.0f;
					rainShadow = 0.0f;
				}

					float pet = CalculatePotentialEvapotranspiration(temperature, latitude, month);
					world.MonthlyPrecipitationMm[monthlyIndex] = precipitation;
					world.MonthlyConvectivePrecipitationMm[monthlyIndex] = convective;
					world.MonthlyOrographicPrecipitationMm[monthlyIndex] = orographic;
					world.MonthlyRainShadowReductionMm[monthlyIndex] = rainShadow;
					world.MonthlyPotentialEvapotranspirationMm[monthlyIndex] = pet;
					annualPrecipitation += precipitation;
					annualPet += pet;

					if (precipitation > wettest)
					{
						wettest = precipitation;
						wettestMonth = month;
					}
					if (precipitation < driest)
					{
						driest = precipitation;
						driestMonth = month;
					}

					float snowfall = temperature <= 0.5f ? precipitation : 0.0f;
					world.MonthlySnowfallMm[monthlyIndex] = snowfall;
				}

				world.AnnualPrecipitationMm[index] = annualPrecipitation;
				world.AnnualPotentialEvapotranspirationMm[index] = annualPet;
				world.WettestMonth[index] = wettestMonth;
				world.DriestMonth[index] = driestMonth;
				world.AridityIndex[index] = annualPet > 1.0f ? annualPrecipitation / annualPet : 0.0f;
			}
		}

		// Seasonal snowmelt is calculated after monthly temperatures and snowfall
		// are known. The same month-by-month state is used later by soil water balance.
		for (int index = 0; index < size; index++)
		{
			float snowpack = 0.0f;
			for (int month = 0; month < WorldData.MonthsPerYear; month++)
			{
				int monthlyIndex = world.GetMonthlyIndex(index, month);
				float snowfall = world.MonthlySnowfallMm[monthlyIndex];
				float temperature = world.MonthlyTemperatureC[monthlyIndex];
				snowpack += snowfall;
				float melt = temperature > 0.0f
					? Mathf.Min(snowpack, temperature * 3.0f + 2.0f)
					: 0.0f;
				snowpack -= melt;
				world.MonthlySnowmeltMm[monthlyIndex] = melt;
			}
		}
	}

	private static void CalculateMonthlyPrecipitation(
		WorldData world, int x, int y, int index, float latitude, int month, int coastDistanceCells,
		out float precipitation, out float convective, out float orographic, out float rainShadowReduction)
	{
		float absoluteLatitude = Mathf.Abs(latitude);
		float itczLatitude = world.MonthlyITCZLatitudeDegrees[month];
		float itcz = Gaussian(latitude - itczLatitude, 10.5f);
		float subtropicalSubsidence = Gaussian(absoluteLatitude - 28.0f, 10.0f);
		float midLatitudeStorms = Gaussian(absoluteLatitude - 52.0f, 16.0f);
		float polarLift = Mathf.Clamp((absoluteLatitude - 68.0f) / 22.0f, 0.0f, 1.0f);
		float summerPeakMonth = latitude >= 0.0f ? 6.0f : 0.0f;
		float seasonalPhase = Mathf.Cos((float)(TwoPi * (month - summerPeakMonth) / 12.0));
		float summerStrength = Mathf.Clamp((seasonalPhase + 1.0f) * 0.5f, 0.0f, 1.0f);
		float winterStrength = 1.0f - summerStrength;
		float midLatitudeWinterStorms = midLatitudeStorms * (1.0f + Gaussian(absoluteLatitude - 38.0f, 12.0f) * winterStrength * 1.15f);
		float seasonalSubtropicalSubsidence = subtropicalSubsidence * (1.0f + Gaussian(absoluteLatitude - 35.0f, 12.0f) * summerStrength * 0.90f);

		float windDirection = world.MonthlyWindDirectionDegrees[world.GetMonthlyIndex(index, month)];
		Vector2 wind = DirectionToVector(windDirection);
		float oceanFetch = CalculateMoistureTransport(world, x, y, wind);
		float inlandDistancePenalty = Mathf.Clamp(coastDistanceCells / 60.0f, 0.0f, 0.30f);
		float moistureSupply = Mathf.Clamp(0.38f + oceanFetch * 0.62f - inlandDistancePenalty * 0.18f, 0.28f, 1.0f);

		float convectivePotential = itcz * (105.0f + 55.0f * oceanFetch);
		float frontalPotential = midLatitudeWinterStorms * 52.0f + polarLift * 8.0f;
		float subtropicalReduction = seasonalSubtropicalSubsidence * 19.0f;
		float circulationRain = Mathf.Max(1.5f, 4.0f + frontalPotential - subtropicalReduction);
		convective = convectivePotential * moistureSupply;

		float monsoonAlignment = latitude >= 0.0f
			? Mathf.Cos((float)(TwoPi * (month - 6.0) / 12.0))
			: Mathf.Cos((float)(TwoPi * month / 12.0));
		if (absoluteLatitude >= 5.0f && absoluteLatitude <= 30.0f && monsoonAlignment > 0.0f)
			convective += monsoonAlignment * oceanFetch * 35.0f;

		CalculateOrographicEffects(world, x, y, index, wind, out float upliftFactor, out float rainShadowFactor);
		float moistureAdjustedBase = circulationRain * moistureSupply + convective;
		orographic = moistureAdjustedBase * Mathf.Max(0.0f, upliftFactor - 1.0f) * 0.55f;
		float beforeShadow = moistureAdjustedBase + orographic;
		float afterShadow = beforeShadow * rainShadowFactor;
		rainShadowReduction = Mathf.Max(0.0f, beforeShadow - afterShadow);
		precipitation = Mathf.Clamp(afterShadow, 0.0f, 650.0f);
	}

	private static void CalculateOrographicEffects(
		WorldData world, int x, int y, int index, Vector2 wind,
		out float upliftFactor, out float rainShadowFactor)
	{
		float currentElevation = world.ElevationMeters[index];
		float nearestUpwindElevation = currentElevation;
		float maximumUpwindElevation = currentElevation;
		float minimumUpwindElevation = currentElevation;
		bool foundNearestUpwindLand = false;

		for (int step = 1; step <= world.ClimateSettings.OrographicSampleSteps; step++)
		{
			int sampleX = x - Mathf.RoundToInt(wind.X * step);
			// DirectionToVector uses screen coordinates (positive Y points south).
			// Move opposite the wind vector to sample the true upwind side.
			int sampleY = Mathf.Clamp(y - Mathf.RoundToInt(wind.Y * step), 0, world.Height - 1);
			int sampleIndex = world.GetIndex(sampleX, sampleY);
			if (!world.IsLand[sampleIndex])
				continue;

			float sampleElevation = world.ElevationMeters[sampleIndex];
			if (!foundNearestUpwindLand)
			{
				nearestUpwindElevation = sampleElevation;
				foundNearestUpwindLand = true;
			}

			maximumUpwindElevation = Mathf.Max(maximumUpwindElevation, sampleElevation);
			minimumUpwindElevation = Mathf.Min(minimumUpwindElevation, sampleElevation);
		}

		float windwardRise = Mathf.Max(0.0f, currentElevation - minimumUpwindElevation);
		upliftFactor = 1.0f + Mathf.Clamp(windwardRise / 1800.0f, 0.0f, 1.25f) * 0.75f;

		float leewardBarrier = Mathf.Max(0.0f, maximumUpwindElevation - currentElevation - 350.0f);
		float shadowStrength = Mathf.Clamp(leewardBarrier / 2600.0f, 0.0f, 1.0f);
		rainShadowFactor = Mathf.Clamp(1.0f - shadowStrength * 0.62f, world.ClimateSettings.RainShadowMinimumTransmission, 1.0f);

		// A small adjacent-gradient check sharpens the windward side of a ridge.
		if (nearestUpwindElevation < currentElevation - 250.0f)
			upliftFactor += Mathf.Clamp((currentElevation - nearestUpwindElevation) / 3000.0f, 0.0f, 0.35f);
	}

	private static float CalculateMoistureTransport(WorldData world, int x, int y, Vector2 wind)
	{
		float weightedOceanFetch = 0.0f;
		float totalWeight = 0.0f;
		float landPathLength = 0.0f;

		for (int step = 1; step <= MoistureFetchSteps; step++)
		{
			int sampleX = x - Mathf.RoundToInt(wind.X * step);
			int sampleY = Mathf.Clamp(y - Mathf.RoundToInt(wind.Y * step), 0, world.Height - 1);
			int sampleIndex = world.GetIndex(sampleX, sampleY);
			float weight = 1.0f / (1.0f + step * 0.10f);
			totalWeight += weight;
			if (!world.IsLand[sampleIndex])
				weightedOceanFetch += weight;
			else
				landPathLength += 1.0f;
		}

		float oceanFraction = totalWeight > 0.0f ? weightedOceanFetch / totalWeight : 0.0f;
		float inlandDrying = Mathf.Clamp(landPathLength / MoistureFetchSteps, 0.0f, 1.0f) * 0.22f;
		return Mathf.Clamp(oceanFraction * 1.25f - inlandDrying, 0.0f, 1.0f);
	}

	private static float CalculateOceanPrecipitation(WorldData world, float latitude, int month)
	{
		float itcz = Gaussian(latitude - world.MonthlyITCZLatitudeDegrees[month], 11.5f);
		float stormTrack = Gaussian(Mathf.Abs(latitude) - 52.0f, 17.0f);
		float polar = Mathf.Clamp((Mathf.Abs(latitude) - 72.0f) / 18.0f, 0.0f, 1.0f);
		return Mathf.Clamp(5.0f + itcz * 145.0f + stormTrack * 58.0f + polar * 8.0f, 0.0f, 520.0f);
	}

	private static float CalculatePotentialEvapotranspiration(float temperature, float latitude, int month)
	{
		if (temperature <= -5.0f)
			return 0.0f;

		float thermalDemand = Mathf.Max(0.0f, temperature + 5.0f) * 2.15f;
		float absoluteLatitude = Mathf.Abs(latitude) / 90.0f;
		float radiation = 0.78f + 0.22f * Mathf.Cos((float)(TwoPi * (month - (latitude >= 0.0f ? 6.0 : 0.0)) / 12.0));
		float latitudeFactor = Mathf.Clamp(1.0f - absoluteLatitude * 0.62f, 0.18f, 1.0f);
		return Mathf.Clamp((thermalDemand + 5.0f) * radiation * latitudeFactor, 0.0f, 260.0f);
	}

	private static void CalculateClimateClassification(WorldData world)
	{
		for (int i = 0; i < world.Width * world.Height; i++)
		{
			if (!world.IsLand[i])
			{
				world.ClimateZones[i] = ClimateZoneType.Ocean;
				world.AridityZones[i] = AridityZoneType.Humid;
				world.Biomes[i] = BiomeType.Ocean;
				continue;
			}

			float aridity = world.AridityIndex[i];
			world.AridityZones[i] = aridity < world.ClimateSettings.HyperAridUpperAridityIndex ? AridityZoneType.HyperArid
				: aridity < world.ClimateSettings.AridUpperAridityIndex ? AridityZoneType.Arid
				: aridity < world.ClimateSettings.SemiAridUpperAridityIndex ? AridityZoneType.SemiArid
				: aridity < world.ClimateSettings.DrySubhumidUpperAridityIndex ? AridityZoneType.DrySubhumid
				: AridityZoneType.Humid;

			float meanTemperature = world.MeanAnnualTemperatureC[i];
			float absoluteLatitude = Mathf.Abs(GetLatitudeDegrees(i / world.Width, world.Height));
			float wettest = 0.0f;
			float driest = float.MaxValue;
			float summerRain = 0.0f;
			float winterRain = 0.0f;
			int summerMonths = 0;
			int winterMonths = 0;
			bool northernHemisphere = GetLatitudeDegrees(i / world.Width, world.Height) >= 0.0f;
			for (int month = 0; month < WorldData.MonthsPerYear; month++)
			{
				float value = world.MonthlyPrecipitationMm[world.GetMonthlyIndex(i, month)];
				wettest = Mathf.Max(wettest, value);
				driest = Mathf.Min(driest, value);
				bool isSummer = northernHemisphere ? month >= 3 && month <= 8 : month <= 2 || month >= 9;
				if (isSummer)
				{
					summerRain += value;
					summerMonths++;
				}
				else
				{
					winterRain += value;
					winterMonths++;
				}
			}

			world.PrecipitationSeasonalityIndex[i] = wettest / Mathf.Max(1.0f, driest);
			float summerMean = summerRain / Math.Max(1, summerMonths);
			float winterMean = winterRain / Math.Max(1, winterMonths);
			bool drySummer = summerMean < winterMean * 0.55f && absoluteLatitude >= 28.0f && absoluteLatitude <= 48.0f;
			if (world.ElevationMeters[i] >= 2500.0f)
				world.ClimateZones[i] = ClimateZoneType.Highland;
			else if (aridity < world.ClimateSettings.AridUpperAridityIndex)
				world.ClimateZones[i] = ClimateZoneType.Arid;
			else if (aridity < world.ClimateSettings.SemiAridUpperAridityIndex)
				world.ClimateZones[i] = ClimateZoneType.SemiArid;
			else if (aridity < world.ClimateSettings.DrySubhumidUpperAridityIndex)
				world.ClimateZones[i] = ClimateZoneType.DrySubhumid;
			else if (meanTemperature < -2.0f || (absoluteLatitude >= 66.0f && meanTemperature < 8.0f))
				world.ClimateZones[i] = ClimateZoneType.Polar;
			else if (meanTemperature < 5.0f)
				world.ClimateZones[i] = ClimateZoneType.Subpolar;
			else if (drySummer)
				world.ClimateZones[i] = ClimateZoneType.Mediterranean;
			else if (absoluteLatitude < 23.5f && meanTemperature >= 18.0f)
				world.ClimateZones[i] = ClimateZoneType.Tropical;
			else if (world.TemperatureRangeC[i] > 20.0f)
				world.ClimateZones[i] = ClimateZoneType.TemperateContinental;
			else
				world.ClimateZones[i] = ClimateZoneType.TemperateOceanic;
		}
	}

	private static int[] CalculateCoastDistance(WorldData world)
	{
		int size = world.Width * world.Height;
		int[] distance = new int[size];
		Array.Fill(distance, int.MaxValue);
		int[] queue = new int[size];
		int head = 0;
		int tail = 0;

		for (int i = 0; i < size; i++)
		{
			if (world.IsLand[i] && world.IsCoastal[i])
			{
				distance[i] = 0;
				queue[tail++] = i;
			}
		}

		while (head < tail)
		{
			int current = queue[head++];
			int x = current % world.Width;
			int y = current / world.Width;
			for (int offsetY = -1; offsetY <= 1; offsetY++)
			{
				int neighborY = y + offsetY;
				if (neighborY < 0 || neighborY >= world.Height)
					continue;
				for (int offsetX = -1; offsetX <= 1; offsetX++)
				{
					if (offsetX == 0 && offsetY == 0)
						continue;
					int neighbor = world.GetIndex(x + offsetX, neighborY);
					if (!world.IsLand[neighbor] || distance[neighbor] != int.MaxValue)
						continue;
					distance[neighbor] = distance[current] + 1;
					queue[tail++] = neighbor;
				}
			}
		}

		for (int i = 0; i < size; i++)
			if (distance[i] == int.MaxValue)
				distance[i] = 0;
		return distance;
	}

	private static Vector2 DirectionToVector(float degrees)
	{
		float radians = degrees * Mathf.Pi / 180.0f;
		// Godot screen Y increases southward; bearing 0° therefore points up.
		return new Vector2(Mathf.Sin(radians), -Mathf.Cos(radians));
	}

	private static float GetLatitudeDegrees(int y, int height) =>
		90.0f - ((y + 0.5f) / height) * 180.0f;

	private static float GetMeanCellSizeKm(WorldData world, int y)
	{
		Vector2 dimensions = world.GetReliefWindowDimensionsKm(y);
		return (dimensions.X + dimensions.Y) / (2.0f * WorldData.ReliefWindowSize);
	}

	private static float Gaussian(float distance, float sigma)
	{
		float normalized = distance / sigma;
		return Mathf.Exp(-0.5f * normalized * normalized);
	}

	private static float GetMinLand(float[] values, bool[] land)
	{
		float minimum = float.MaxValue;
		for (int i = 0; i < values.Length; i++)
			if (land[i]) minimum = Mathf.Min(minimum, values[i]);
		return minimum == float.MaxValue ? 0.0f : minimum;
	}

	private static float GetMaxLand(float[] values, bool[] land)
	{
		float maximum = float.MinValue;
		for (int i = 0; i < values.Length; i++)
			if (land[i]) maximum = Mathf.Max(maximum, values[i]);
		return maximum == float.MinValue ? 0.0f : maximum;
	}
}

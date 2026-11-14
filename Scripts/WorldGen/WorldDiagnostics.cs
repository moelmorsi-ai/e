#nullable enable

using System;
using System.Text;

/// <summary>
/// Lightweight generation-time integrity checks for the shared WorldData layers.
/// These checks detect numerical and structural failures; they do not certify
/// scientific accuracy or validate the model against observed climate datasets.
/// </summary>
public static class WorldDiagnostics
{
	public static bool Validate(WorldData world, out string report)
	{
		if (world == null)
			throw new ArgumentNullException(nameof(world));

		int errors = 0;
		int warnings = 0;
		int size = world.Width * world.Height;
		StringBuilder output = new StringBuilder();

		if (!world.ClimateCalculated)
		{
			errors++;
			output.AppendLine("ERROR: climate pipeline did not complete.");
		}
		if (!world.DerivedLayersCalculated)
		{
			errors++;
			output.AppendLine("ERROR: derived-layer pipeline did not complete.");
		}

		CheckLength(world.ElevationMeters, size, nameof(world.ElevationMeters), ref errors, output);
		CheckLength(world.IsLand, size, nameof(world.IsLand), ref errors, output);
		CheckLength(world.MeanAnnualTemperatureC, size, nameof(world.MeanAnnualTemperatureC), ref errors, output);
		CheckLength(world.AnnualPrecipitationMm, size, nameof(world.AnnualPrecipitationMm), ref errors, output);
		CheckLength(world.AridityIndex, size, nameof(world.AridityIndex), ref errors, output);
		CheckLength(world.SoilFertility, size, nameof(world.SoilFertility), ref errors, output);
		CheckLength(world.GeologicalStability, size, nameof(world.GeologicalStability), ref errors, output);
		CheckLength(world.VegetationDensity, size, nameof(world.VegetationDensity), ref errors, output);
		CheckLength(world.AgriculturalSuitability, size, nameof(world.AgriculturalSuitability), ref errors, output);
		CheckLength(world.SettlementSuitability, size, nameof(world.SettlementSuitability), ref errors, output);
		CheckLength(world.ResourceAbundance, size * WorldData.NaturalResourceCount, nameof(world.ResourceAbundance), ref errors, output);
		CheckLength(world.ResourceAccessibility, size * WorldData.NaturalResourceCount, nameof(world.ResourceAccessibility), ref errors, output);
		CheckLength(world.MonthlyPotentialEvapotranspirationMm, size * WorldData.MonthsPerYear, nameof(world.MonthlyPotentialEvapotranspirationMm), ref errors, output);
		CheckLength(world.MonthlyWindDirectionDegrees, size * WorldData.MonthsPerYear, nameof(world.MonthlyWindDirectionDegrees), ref errors, output);
		CheckLength(world.MonthlyWindSpeedMps, size * WorldData.MonthsPerYear, nameof(world.MonthlyWindSpeedMps), ref errors, output);
		CheckLength(world.MonthlyRunoffMm, size * WorldData.MonthsPerYear, nameof(world.MonthlyRunoffMm), ref errors, output);
		CheckLength(world.MonthlyActualEvapotranspirationMm, size * WorldData.MonthsPerYear, nameof(world.MonthlyActualEvapotranspirationMm), ref errors, output);
		CheckLength(world.MonthlyGroundwaterRechargeMm, size * WorldData.MonthsPerYear, nameof(world.MonthlyGroundwaterRechargeMm), ref errors, output);
		CheckLength(world.MonthlyTemperatureC, size * WorldData.MonthsPerYear, nameof(world.MonthlyTemperatureC), ref errors, output);
		CheckLength(world.MonthlyPrecipitationMm, size * WorldData.MonthsPerYear, nameof(world.MonthlyPrecipitationMm), ref errors, output);
		CheckLength(world.MonthlyDischargeM3PerSecond, size * WorldData.MonthsPerYear, nameof(world.MonthlyDischargeM3PerSecond), ref errors, output);

		CheckFiniteArray(world.MonthlyActualEvapotranspirationMm, nameof(world.MonthlyActualEvapotranspirationMm), ref errors, output);
		CheckFiniteArray(world.MonthlyWindDirectionDegrees, nameof(world.MonthlyWindDirectionDegrees), ref errors, output);
		CheckFiniteArray(world.MonthlyWindSpeedMps, nameof(world.MonthlyWindSpeedMps), ref errors, output);
		CheckFiniteArray(world.MonthlyConvectivePrecipitationMm, nameof(world.MonthlyConvectivePrecipitationMm), ref errors, output);
		CheckFiniteArray(world.MonthlyOrographicPrecipitationMm, nameof(world.MonthlyOrographicPrecipitationMm), ref errors, output);
		CheckFiniteArray(world.MonthlyRainShadowReductionMm, nameof(world.MonthlyRainShadowReductionMm), ref errors, output);
		CheckFiniteArray(world.MonthlyGroundwaterRechargeMm, nameof(world.MonthlyGroundwaterRechargeMm), ref errors, output);
		CheckFiniteArray(world.AnnualActualEvapotranspirationMm, nameof(world.AnnualActualEvapotranspirationMm), ref errors, output);
		CheckFiniteArray(world.AnnualGroundwaterRechargeMm, nameof(world.AnnualGroundwaterRechargeMm), ref errors, output);
		CheckFiniteArray(world.WaterAvailabilityMm, nameof(world.WaterAvailabilityMm), ref errors, output);
		CheckFiniteArray(world.GroundwaterPotential, nameof(world.GroundwaterPotential), ref errors, output);
		CheckFiniteArray(world.FreshwaterAccess, nameof(world.FreshwaterAccess), ref errors, output);
		CheckFiniteArray(world.SoilDepthMeters, nameof(world.SoilDepthMeters), ref errors, output);
		CheckFiniteArray(world.SoilDrainage, nameof(world.SoilDrainage), ref errors, output);
		CheckFiniteArray(world.SoilErosionRisk, nameof(world.SoilErosionRisk), ref errors, output);
		CheckFiniteArray(world.SoilStability, nameof(world.SoilStability), ref errors, output);
		CheckFiniteArray(world.VegetationDensity, nameof(world.VegetationDensity), ref errors, output);
		CheckFiniteArray(world.ForestDensity, nameof(world.ForestDensity), ref errors, output);
		CheckFiniteArray(world.GrasslandCoverage, nameof(world.GrasslandCoverage), ref errors, output);
		CheckFiniteArray(world.DesertCoverage, nameof(world.DesertCoverage), ref errors, output);
		CheckFiniteArray(world.WetlandCoverage, nameof(world.WetlandCoverage), ref errors, output);
		CheckFiniteArray(world.VegetationWaterDemand, nameof(world.VegetationWaterDemand), ref errors, output);
		CheckFiniteArray(world.ResourceAbundance, nameof(world.ResourceAbundance), ref errors, output);
		CheckFiniteArray(world.ResourceAccessibility, nameof(world.ResourceAccessibility), ref errors, output);
		CheckFiniteArray(world.InfantryMovementCostMultiplier, nameof(world.InfantryMovementCostMultiplier), ref errors, output);
		CheckFiniteArray(world.CavalryMovementCostMultiplier, nameof(world.CavalryMovementCostMultiplier), ref errors, output);
		CheckFiniteArray(world.VehicleMovementCostMultiplier, nameof(world.VehicleMovementCostMultiplier), ref errors, output);

		float minimumTemperature = float.MaxValue;
		float maximumTemperature = float.MinValue;
		float minimumRainfall = float.MaxValue;
		float maximumRainfall = float.MinValue;
		float maximumDischarge = 0.0f;
		int landCells = 0;
		int riverCells = 0;
		int lakeCells = 0;

		for (int index = 0; index < size; index++)
		{
			if (!IsFinite(world.ElevationMeters[index]) ||
				!IsFinite(world.MeanAnnualTemperatureC[index]) ||
				!IsFinite(world.AnnualPrecipitationMm[index]) ||
				!IsFinite(world.AnnualPotentialEvapotranspirationMm[index]) ||
				!IsFinite(world.AnnualRunoffMm[index]) ||
				!IsFinite(world.AridityIndex[index]) ||
				!IsFinite(world.GeologicalStability[index]) ||
				!IsFinite(world.SeismicRisk[index]) ||
				!IsFinite(world.SoilFertility[index]) ||
				!IsFinite(world.SoilInfiltrationRateMmPerHour[index]) ||
				!IsFinite(world.VegetationDensity[index]) ||
				!IsFinite(world.GroundwaterPotential[index]) ||
				!IsFinite(world.FreshwaterAccess[index]))
			{
				errors++;
				if (errors <= 12)
					output.AppendLine($"ERROR: non-finite core climate/terrain value at cell {index}.");
			}

			if (world.AnnualPrecipitationMm[index] < 0.0f || world.AnnualRunoffMm[index] < 0.0f)
			{
				errors++;
				if (errors <= 12)
					output.AppendLine($"ERROR: negative precipitation or runoff at cell {index}.");
			}

			if (world.IsLand[index])
			{
				landCells++;
				minimumTemperature = Math.Min(minimumTemperature, world.MeanAnnualTemperatureC[index]);
				maximumTemperature = Math.Max(maximumTemperature, world.MeanAnnualTemperatureC[index]);
				minimumRainfall = Math.Min(minimumRainfall, world.AnnualPrecipitationMm[index]);
				maximumRainfall = Math.Max(maximumRainfall, world.AnnualPrecipitationMm[index]);
			}
			if (world.IsRiver[index])
			{
				riverCells++;
				if (!world.IsLand[index])
				{
					warnings++;
					output.AppendLine($"WARNING: mapped river is not on a land cell at {index}.");
				}
			}
			if (world.IsLake[index])
				lakeCells++;

			if (!IsInUnitRange(world.ConstructionSuitability[index]) ||
				!IsInUnitRange(world.AgriculturalSuitability[index]) ||
				!IsInUnitRange(world.SettlementSuitability[index]) ||
				!IsInUnitRange(world.GeologicalStability[index]) ||
				!IsInUnitRange(world.SeismicRisk[index]))
			{
				errors++;
				if (errors <= 12)
					output.AppendLine($"ERROR: a 0–100 score is out of range at cell {index}.");
			}

			int downstream = world.FlowToCell[index];
			if (downstream < -1 || downstream >= size)
			{
				errors++;
				if (errors <= 12)
					output.AppendLine($"ERROR: invalid downstream cell index at {index}.");
			}

			double monthlyPrecipitationTotal = 0.0;
			double monthlyPetTotal = 0.0;
			double monthlyRunoffTotal = 0.0;
			double monthlyActualEtTotal = 0.0;
			double monthlyRechargeTotal = 0.0;
			double monthlyDischargeTotal = 0.0;

			for (int month = 0; month < WorldData.MonthsPerYear; month++)
			{
				int monthlyIndex = world.GetMonthlyIndex(index, month);
				float temperature = world.MonthlyTemperatureC[monthlyIndex];
				float precipitation = world.MonthlyPrecipitationMm[monthlyIndex];
				float discharge = world.MonthlyDischargeM3PerSecond[monthlyIndex];
				float pet = world.MonthlyPotentialEvapotranspirationMm[monthlyIndex];
				float runoff = world.MonthlyRunoffMm[monthlyIndex];
				float actualEt = world.MonthlyActualEvapotranspirationMm[monthlyIndex];
				float recharge = world.MonthlyGroundwaterRechargeMm[monthlyIndex];
				float windSpeed = world.MonthlyWindSpeedMps[monthlyIndex];
				float windDirection = world.MonthlyWindDirectionDegrees[monthlyIndex];
				if (!IsFinite(temperature) || !IsFinite(precipitation) || !IsFinite(discharge) ||
					!IsFinite(pet) || !IsFinite(runoff) || !IsFinite(actualEt) || !IsFinite(recharge) ||
					!IsFinite(windSpeed) || !IsFinite(windDirection) ||
					precipitation < 0.0f || discharge < 0.0f || pet < 0.0f ||
					runoff < 0.0f || actualEt < 0.0f || recharge < 0.0f ||
					windSpeed < 0.0f || windDirection < 0.0f || windDirection >= 360.0f)
				{
					errors++;
					if (errors <= 12)
						output.AppendLine($"ERROR: invalid monthly climate/hydrology value at cell {index}, month {month + 1}.");
				}

				monthlyPrecipitationTotal += precipitation;
				monthlyPetTotal += pet;
				monthlyRunoffTotal += runoff;
				monthlyActualEtTotal += actualEt;
				monthlyRechargeTotal += recharge;
				monthlyDischargeTotal += discharge;
				maximumDischarge = Math.Max(maximumDischarge, discharge);
			}

			if (world.IsLand[index])
			{
				CheckAnnualMonthlyAgreement(monthlyPrecipitationTotal, world.AnnualPrecipitationMm[index],
					"precipitation", index, ref errors, output);
				CheckAnnualMonthlyAgreement(monthlyPetTotal, world.AnnualPotentialEvapotranspirationMm[index],
					"potential evapotranspiration", index, ref errors, output);
				CheckAnnualMonthlyAgreement(monthlyRunoffTotal, world.AnnualRunoffMm[index],
					"runoff", index, ref errors, output);
				CheckAnnualMonthlyAgreement(monthlyActualEtTotal, world.AnnualActualEvapotranspirationMm[index],
					"actual evapotranspiration", index, ref errors, output);
				CheckAnnualMonthlyAgreement(monthlyRechargeTotal, world.AnnualGroundwaterRechargeMm[index],
					"groundwater recharge", index, ref errors, output);
			}

			float monthlyMeanDischarge = (float)(monthlyDischargeTotal / WorldData.MonthsPerYear);
			float annualMeanDischarge = world.MeanAnnualDischargeM3PerSecond[index];
			if (!IsFinite(annualMeanDischarge) ||
				Math.Abs(monthlyMeanDischarge - annualMeanDischarge) > Math.Max(0.05, annualMeanDischarge * 0.005))
			{
				errors++;
				if (errors <= 12)
					output.AppendLine($"ERROR: annual/monthly mean discharge mismatch at cell {index}.");
			}
		}

		if (landCells == 0)
		{
			errors++;
			output.AppendLine("ERROR: generated world contains no land cells.");
		}

		output.AppendLine("WORLD DIAGNOSTICS");
		output.AppendLine($"Dimensions: {world.Width} × {world.Height}");
		output.AppendLine($"Climate settings: lapse rate {world.ClimateSettings.EnvironmentalLapseRateCPerKm:F1} °C/km; ITCZ shift ±{world.ClimateSettings.ITCZSeasonalAmplitudeDegrees:F1}°; orographic samples {world.ClimateSettings.OrographicSampleSteps}");
		output.AppendLine($"Land cells: {landCells:N0} ({100.0 * landCells / size:F1}% by cell count; generation target is area-weighted)");
		output.AppendLine($"Mapped river cells: {riverCells:N0}; lake cells: {lakeCells:N0}");
		output.AppendLine($"Land mean-temperature range: {(landCells > 0 ? minimumTemperature : 0.0f):F1} to {(landCells > 0 ? maximumTemperature : 0.0f):F1} °C");
		output.AppendLine($"Land annual-precipitation range: {(landCells > 0 ? minimumRainfall : 0.0f):F0} to {(landCells > 0 ? maximumRainfall : 0.0f):F0} mm");
		output.AppendLine($"Maximum monthly mean discharge: {maximumDischarge:F1} m³/s");
		output.AppendLine($"Errors: {errors}; warnings: {warnings}");
		output.Append(errors == 0 ? "STATUS: PASS (structural/numerical checks only)." : "STATUS: FAIL (review reported errors).");

		report = output.ToString();
		return errors == 0;
	}

	private static void CheckAnnualMonthlyAgreement(
		double monthlyTotal, float annualTotal, string quantity, int cellIndex,
		ref int errors, StringBuilder output)
	{
		if (!IsFinite(annualTotal) || Math.Abs(monthlyTotal - annualTotal) > Math.Max(0.1, Math.Abs(annualTotal) * 0.001))
		{
			errors++;
			if (errors <= 12)
				output.AppendLine($"ERROR: monthly {quantity} does not match annual total at cell {cellIndex}.");
		}
	}

	private static void CheckFiniteArray(float[] values, string name, ref int errors, StringBuilder output)
	{
		for (int i = 0; i < values.Length; i++)
		{
			if (IsFinite(values[i]))
				continue;
			errors++;
			if (errors <= 12)
				output.AppendLine($"ERROR: non-finite value in {name} at index {i}.");
			break;
		}
	}

	private static void CheckLength(Array values, int expected, string name, ref int errors, StringBuilder output)
	{
		if (values.Length == expected)
			return;
		errors++;
		output.AppendLine($"ERROR: {name} length is {values.Length}; expected {expected}.");
	}

	private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
	private static bool IsInUnitRange(float value) => IsFinite(value) && value >= 0.0f && value <= 100.0f;
}

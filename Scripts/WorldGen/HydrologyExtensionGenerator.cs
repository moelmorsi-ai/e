#nullable enable

using Godot;
using System;

/// <summary>
/// Derives groundwater, freshwater access and seasonal-flow diagnostics from
/// the climate-adjusted runoff and existing priority-flood drainage network.
/// </summary>
public static class HydrologyExtensionGenerator
{
	public static void Calculate(WorldData world)
	{
		if (world == null)
			throw new ArgumentNullException(nameof(world));

		int size = world.Width * world.Height;
		Array.Clear(world.PeakMonthlyDischargeM3PerSecond, 0, size);
		Array.Clear(world.DischargeSeasonalityIndex, 0, size);
		Array.Clear(world.GroundwaterPotential, 0, size);
		Array.Clear(world.FreshwaterAccess, 0, size);
		Array.Clear(world.LakeWaterBalanceMm, 0, size);

		for (int index = 0; index < size; index++)
		{
			float peak = 0.0f;
			for (int month = 0; month < WorldData.MonthsPerYear; month++)
			{
				float discharge = world.MonthlyDischargeM3PerSecond[world.GetMonthlyIndex(index, month)];
				peak = Mathf.Max(peak, discharge);
			}

			world.PeakMonthlyDischargeM3PerSecond[index] = peak;
			float mean = world.MeanAnnualDischargeM3PerSecond[index];
			world.DischargeSeasonalityIndex[index] = peak > 0.001f
				? Mathf.Clamp((peak - mean) / peak, 0.0f, 1.0f)
				: 0.0f;

			if (!world.IsLand[index])
			{
				world.GroundwaterPotential[index] = 0.0f;
				world.FreshwaterAccess[index] = 0.0f;
				continue;
			}

			float rechargeRatio = world.AnnualPrecipitationMm[index] > 1.0f
				? world.AnnualGroundwaterRechargeMm[index] / world.AnnualPrecipitationMm[index]
				: 0.0f;
			float infiltrationScore = Mathf.Clamp(world.SoilInfiltrationRateMmPerHour[index] / 40.0f, 0.0f, 1.0f);
			float permeability = world.RockTypes[index] switch
			{
				RockType.Alluvial => 0.95f,
				RockType.Sedimentary => 0.82f,
				RockType.Volcanic => 0.68f,
				RockType.Igneous => 0.48f,
				RockType.Metamorphic => 0.36f,
				RockType.Cratonic => 0.30f,
				_ => 0.35f
			};
			world.GroundwaterPotential[index] = Mathf.Clamp(
				(0.45f * Mathf.Clamp(rechargeRatio * 2.5f, 0.0f, 1.0f) +
				 0.25f * infiltrationScore + 0.30f * permeability) * 100.0f,
				0.0f, 100.0f);

			float waterBalance = world.WaterAvailabilityMm[index];
			float freshwater = Mathf.Clamp(waterBalance / 900.0f, 0.0f, 1.0f) * 45.0f +
				world.GroundwaterPotential[index] * 0.45f;
			if (world.IsRiver[index])
				freshwater += 28.0f;
			if (world.IsLake[index])
				freshwater += 35.0f;
			world.FreshwaterAccess[index] = Mathf.Clamp(freshwater, 0.0f, 100.0f);

			if (world.IsLake[index])
			{
				float inflowDepthEquivalent = 0.0f;
				float area = world.GetCellAreaKm2(index / world.Width);
				if (area > 0.001f)
					// m³/s × seconds/year ÷ (1,000 m³ per mm·km²) = mm/year.
					inflowDepthEquivalent = mean * 31_536.0f / area;
				world.LakeWaterBalanceMm[index] =
					world.AnnualPrecipitationMm[index] + inflowDepthEquivalent -
					world.AnnualPotentialEvapotranspirationMm[index];
			}
		}

		// A small neighbourhood search supplies a useful freshwater-access proxy
		// for settlement scoring. It is deliberately not a pipeline or water-rights model.
		for (int y = 0; y < world.Height; y++)
		{
			for (int x = 0; x < world.Width; x++)
			{
				int index = world.GetIndex(x, y);
				if (!world.IsLand[index] || world.FreshwaterAccess[index] >= 70.0f)
					continue;

				float nearestWaterBonus = 0.0f;
				for (int radius = 1; radius <= 3 && nearestWaterBonus == 0.0f; radius++)
				{
					for (int oy = -radius; oy <= radius; oy++)
					{
						int ny = y + oy;
						if (ny < 0 || ny >= world.Height)
							continue;
						for (int ox = -radius; ox <= radius; ox++)
						{
							if (Math.Abs(ox) != radius && Math.Abs(oy) != radius)
								continue;
							int neighbour = world.GetIndex(x + ox, ny);
							if (world.IsRiver[neighbour] || world.IsLake[neighbour])
							{
								nearestWaterBonus = 24.0f / radius;
								break;
							}
						}
						if (nearestWaterBonus > 0.0f)
							break;
					}
				}
				world.FreshwaterAccess[index] = Mathf.Clamp(world.FreshwaterAccess[index] + nearestWaterBonus, 0.0f, 100.0f);
			}
		}
	}
}

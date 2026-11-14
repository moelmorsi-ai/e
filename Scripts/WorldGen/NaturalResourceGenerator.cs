#nullable enable

using Godot;
using System;

/// <summary>
/// Generates spatially coherent strategic resource potential. Geological deposits
/// are proxies for plausible settings; they are not claims about actual deposits.
/// Resource abundance and accessibility are separate 0-100 layers.
/// </summary>
public static class NaturalResourceGenerator
{
	private static FastNoiseLite CreateNoise(int seed)
	{
		FastNoiseLite noise = new FastNoiseLite();
		noise.Seed = seed;
		noise.NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth;
		noise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
		noise.FractalOctaves = 3;
		noise.FractalLacunarity = 2.0f;
		noise.FractalGain = 0.5f;
		noise.Frequency = 1.8f;
		return noise;
	}

	public static void Generate(WorldData world)
	{
		if (world == null)
			throw new ArgumentNullException(nameof(world));

		FastNoiseLite[] depositNoise = new FastNoiseLite[WorldData.NaturalResourceCount];
		for (int resource = 0; resource < depositNoise.Length; resource++)
			depositNoise[resource] = CreateNoise(world.Seed + 2003 + resource * 7919);

		int size = world.Width * world.Height;
		Array.Clear(world.ResourceAbundance, 0, world.ResourceAbundance.Length);
		Array.Clear(world.ResourceAccessibility, 0, world.ResourceAccessibility.Length);

		for (int y = 0; y < world.Height; y++)
		{
			float latitude = Mathf.Pi * (0.5f - ((y + 0.5f) / world.Height));
			float cosLatitude = Mathf.Cos(latitude);
			for (int x = 0; x < world.Width; x++)
			{
				int index = world.GetIndex(x, y);
				if (!world.IsLand[index] || world.IsLake[index])
					continue;

				float longitude = (Mathf.Tau * ((x + 0.5f) / world.Width)) - Mathf.Pi;
				float sx = cosLatitude * Mathf.Cos(longitude);
				float sy = Mathf.Sin(latitude);
				float sz = cosLatitude * Mathf.Sin(longitude);
				float tectonicActivity = 1.0f - world.GeologicalStability[index] / 100.0f;
				float relief = Mathf.Clamp(world.LocalReliefMeters[index] / 1000.0f, 0.0f, 1.0f);
				float basinFactor = world.ElevationMeters[index] < 1200.0f
					? 1.0f - Mathf.Clamp(world.ElevationMeters[index] / 1800.0f, 0.0f, 0.7f)
					: 0.15f;
				RockType rock = world.RockTypes[index];

				for (int resource = 0; resource < WorldData.NaturalResourceCount; resource++)
				{
					float noise = depositNoise[resource].GetNoise3D(sx, sy, sz);
					float patch = Mathf.Clamp((noise + 1.0f) * 0.5f, 0.0f, 1.0f);
					float abundance = 0.0f;

					switch ((NaturalResourceType)resource)
					{
						case NaturalResourceType.Iron:
							if (rock == RockType.Igneous || rock == RockType.Metamorphic || rock == RockType.Volcanic)
								abundance = 18.0f + patch * 58.0f + tectonicActivity * 18.0f + relief * 8.0f;
							break;
						case NaturalResourceType.Copper:
							if (rock == RockType.Igneous || rock == RockType.Volcanic || rock == RockType.Metamorphic)
								abundance = 12.0f + patch * 62.0f + tectonicActivity * 24.0f;
							break;
						case NaturalResourceType.Gold:
							if (rock == RockType.Metamorphic || rock == RockType.Igneous || rock == RockType.Volcanic)
								abundance = 8.0f + patch * 54.0f + tectonicActivity * 30.0f;
							break;
						case NaturalResourceType.Tin:
							// Cassiterite-bearing granite/greisen and hydrothermal settings are
							// represented by a conservative procedural proxy.
							if (rock == RockType.Igneous || rock == RockType.Metamorphic || rock == RockType.Volcanic)
								abundance = 5.0f + patch * 48.0f + tectonicActivity * 22.0f + relief * 10.0f;
							break;
						case NaturalResourceType.Coal:
							if (rock == RockType.Sedimentary || rock == RockType.Alluvial)
								abundance = 10.0f + patch * 56.0f + basinFactor * 22.0f + world.ForestDensity[index] * 0.08f;
							break;
						case NaturalResourceType.Oil:
							if (rock == RockType.Sedimentary || rock == RockType.Alluvial)
								abundance = 8.0f + patch * 58.0f + basinFactor * 28.0f;
							break;
						case NaturalResourceType.NaturalGas:
							if (rock == RockType.Sedimentary || rock == RockType.Alluvial)
								abundance = 8.0f + patch * 54.0f + basinFactor * 30.0f;
							break;
						case NaturalResourceType.Stone:
							abundance = 22.0f + patch * 48.0f + relief * 24.0f;
							break;
						case NaturalResourceType.Timber:
							abundance = world.ForestDensity[index] * (0.72f + patch * 0.28f);
							break;
						case NaturalResourceType.Freshwater:
							abundance = world.FreshwaterAccess[index] * 0.72f + Mathf.Clamp(world.AnnualPrecipitationMm[index] / 30.0f, 0.0f, 28.0f);
							break;
						case NaturalResourceType.FertileLand:
							abundance = world.SoilFertility[index] * 0.68f + world.SoilDepthMeters[index] * 12.0f;
							break;
					}

					int resourceIndex = resource * size + index;
					world.ResourceAbundance[resourceIndex] = Mathf.Clamp(abundance, 0.0f, 100.0f);
					float access = 100.0f - world.SlopeDegrees[index] * 1.65f - world.LocalReliefMeters[index] / 35.0f;
					if (world.ElevationMeters[index] > 3000.0f)
						access -= 15.0f;
					if (world.WetlandCoverage[index] > 0.0f)
						access -= 8.0f;
					world.ResourceAccessibility[resourceIndex] = Mathf.Clamp(access, 0.0f, 100.0f);
				}
			}
		}
	}
}

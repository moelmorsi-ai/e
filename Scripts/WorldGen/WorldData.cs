#nullable enable

using Godot;
using System;
using System.Collections.Generic;

public enum TerrainType
{
	CoastalWater,
	ShallowSea,
	ContinentalShelf,
	DeepOcean,
	CoastalLowland,
	Lowland,
	CoastalPlain,
	Plains,
	RollingHills,
	RuggedHills,
	Highlands,
	Mountains,
	HighMountains,
	AlpinePeaks
}

public enum RiverFlowRegime
{
	None,
	Intermittent,
	Seasonal,
	Perennial
}

public enum BiomeType
{
	Ocean,
	TropicalRainforest,
	TropicalSeasonalForest,
	Savanna,
	HotDesert,
	ColdDesert,
	Steppe,
	Mediterranean,
	TemperateGrassland,
	TemperateForest,
	BorealForest,
	Tundra,
	Alpine,
	Wetland
}

public enum ClimateZoneType
{
	Ocean,
	Tropical,
	Arid,
	SemiArid,
	DrySubhumid,
	Mediterranean,
	TemperateOceanic,
	TemperateContinental,
	Subpolar,
	Polar,
	Highland
}

public enum AridityZoneType
{
	HyperArid,
	Arid,
	SemiArid,
	DrySubhumid,
	Humid
}

public enum PressureBeltType
{
	EquatorialLow,
	SubtropicalHigh,
	MidLatitudeLow,
	PolarHigh
}

public enum RockType
{
	None,
	Sedimentary,
	Igneous,
	Metamorphic,
	Volcanic,
	Cratonic,
	Alluvial
}

public enum SoilType
{
	None,
	Sandy,
	Clay,
	Loamy,
	SiltRich,
	Rocky,
	Peaty,
	Saline,
	ShallowMountain
}

public enum VegetationType
{
	None,
	TropicalForest,
	SeasonalForest,
	Savanna,
	Shrubland,
	Grassland,
	DesertScrub,
	TemperateForest,
	BorealForest,
	Tundra,
	Alpine,
	Wetland
}

public enum NaturalResourceType
{
	Iron,
	Copper,
	Gold,
	Coal,
	Oil,
	NaturalGas,
	Tin,
	Stone,
	Timber,
	Freshwater,
	FertileLand
}

/// <summary>
/// Stores the world-generation layers and derives geographical information
/// used by the map modes and the location inspector.
/// </summary>
public sealed class WorldData
{
	private const double EarthRadiusMeters = 6_371_000.0;
	private const int ReliefRadiusCells = 1;
	public const int RegionalFloodSusceptibilitySearchRadiusCells = 2;
	public const float RegionalFloodSusceptibilityThreshold = 20.0f;

	// Backwards-compatible aliases for earlier project references.
	public const int FloodplainAnalysisRadiusCells = RegionalFloodSusceptibilitySearchRadiusCells;
	public const float PotentialFloodplainRiskThreshold = RegionalFloodSusceptibilityThreshold;
	public const float RiverCellConstructionPenalty = 12.0f;
	public const int MonthsPerYear = 12;
	private const double SecondsPerYear = 31_536_000.0;

	// A 3x3 neighborhood gives a more local relief measure than the previous 5x5 window.
	public const int ReliefWindowSize = ReliefRadiusCells * 2 + 1;

	public int Width { get; }
	public int Height { get; }
	public int Seed { get; }
	public ClimateModelSettings ClimateSettings { get; }

	// Primary geographical layers populated by ProceduralWorldGenerator.
	public float[] Continentalness { get; }
	public float[] ElevationMeters { get; }
	// IsLand describes the underlying cell surface. A mapped river may intersect
	// a land cell without turning the entire coarse cell into open water.
	public bool[] IsLand { get; }

	// Derived geographical layers.
	public bool[] IsCoastal { get; }
	public float[] LocalReliefMeters { get; }
	public float[] SlopeDegrees { get; }
	public float[] AspectDegrees { get; }
	public TerrainType[] TerrainTypes { get; }

	// Monthly arrays use [month * (Width * Height) + cellIndex].
	public float[] MonthlyTemperatureC { get; }
	public float[] MonthlyPrecipitationMm { get; }
	public float[] MonthlySnowfallMm { get; }
	public float[] MonthlySnowmeltMm { get; }
	public float[] MonthlyRunoffMm { get; }
	public float[] MeanAnnualTemperatureC { get; }
	public float[] AnnualPrecipitationMm { get; }
	public float[] AnnualPotentialEvapotranspirationMm { get; }
	public float[] AnnualRunoffMm { get; }
	public float[] AridityIndex { get; }
	public float[] PrevailingWindDirectionDegrees { get; }
	public int[] WettestMonth { get; }
	public int[] DriestMonth { get; }
	public BiomeType[] Biomes { get; }
	public ClimateZoneType[] ClimateZones { get; }
	public AridityZoneType[] AridityZones { get; }
	public PressureBeltType[] PressureBelts { get; }
	public float[] TemperatureRangeC { get; }	
	public float[] PrecipitationSeasonalityIndex { get; }
	public int[] GrowingSeasonMonths { get; }	
	public float[] MonthlyPotentialEvapotranspirationMm { get; }
	public float[] MonthlyActualEvapotranspirationMm { get; }
	public float[] MonthlyWindDirectionDegrees { get; }
	public float[] MonthlyWindSpeedMps { get; }
	public float[] MonthlyConvectivePrecipitationMm { get; }
	public float[] MonthlyOrographicPrecipitationMm { get; }
	public float[] MonthlyRainShadowReductionMm { get; }
	public float[] MonthlyGroundwaterRechargeMm { get; }
	public float[] AnnualActualEvapotranspirationMm { get; }
	public float[] AnnualGroundwaterRechargeMm { get; }
	public float[] WaterAvailabilityMm { get; }
	public float[] GroundwaterPotential { get; }
	public float[] FreshwaterAccess { get; }	
	public float[] PeakMonthlyDischargeM3PerSecond { get; }
	public float[] DischargeSeasonalityIndex { get; }
	public float[] LakeWaterBalanceMm { get; }
	public float[] MonthlyITCZLatitudeDegrees { get; }

	// Geology and soil layers.
	public RockType[] RockTypes { get; }
	public int[] TectonicProvinceIds { get; }
	public float[] GeologicalStability { get; }
	public float[] SeismicRisk { get; }
	public SoilType[] SoilTypes { get; }
	public float[] SoilFertility { get; }
	public float[] SoilDepthMeters { get; }
	public float[] SoilInfiltrationRateMmPerHour { get; }
	public float[] SoilDrainage { get; }
	public float[] SoilErosionRisk { get; }
	public float[] SoilStability { get; }

	// Ecology and vegetation layers.
	public VegetationType[] VegetationTypes { get; }
	public float[] VegetationDensity { get; }
	public float[] ForestDensity { get; }
	public float[] GrasslandCoverage { get; }
	public float[] DesertCoverage { get; }
	public float[] WetlandCoverage { get; }
	public float[] VegetationWaterDemand { get; }

	// Resource arrays use [resource * cellCount + cellIndex].
	public const int NaturalResourceCount = 11;
	public float[] ResourceAbundance { get; }
	public float[] ResourceAccessibility { get; }

	// Additional gameplay layers.
	public float[] AgriculturalSuitability { get; }
	public float[] SettlementSuitability { get; }
	public float[] InfantryMovementCostMultiplier { get; }
	public float[] CavalryMovementCostMultiplier { get; }
	public float[] VehicleMovementCostMultiplier { get; }

	// Hydrology layers derived from the elevation surface.
	// Flow accumulation is upstream catchment area, not measured discharge.
	// IsRiver means a mapped channel intersects this land cell; it does not mean
	// the full cell footprint is water. IsLake represents an inland-water cell.
	public float[] FilledElevationMeters { get; }
	public float[] DepressionDepthMeters { get; }
	public int[] FlowToCell { get; }
	public float[] FlowAccumulationKm2 { get; }
	public float[] FlowAccumulationRunoffM3PerYear { get; }
	public float[] MeanAnnualDischargeM3PerSecond { get; }
	public float[] MonthlyDischargeM3PerSecond { get; }
	public RiverFlowRegime[] RiverFlowRegimes { get; }
	public bool[] IsRiver { get; }
	public bool[] IsLake { get; }
	public int[] RiverOrder { get; }
	// Full provisional river-crossing penalty associated with a channel cell.
	// Applied once when a land army transitions from a non-channel cell into it.
	public float[] RiverCrossingCostMultiplier { get; }

	// Regional river-corridor flood susceptibility. This is a coarse strategic-scale
	// terrain index, not a mapped floodplain, hydraulic inundation or flood-depth simulation.
	public float[] RegionalFloodSusceptibility { get; }
	public bool[] IsElevatedFloodSusceptibility { get; }

	// Backwards-compatible aliases; these arrays are susceptibility indicators,
	// not verified floodplain boundaries or flood predictions.
	public float[] FloodplainRisk => RegionalFloodSusceptibility;
	public bool[] IsPotentialFloodplain => IsElevatedFloodSusceptibility;
	public float[] FloodplainReferenceRiverDistanceKm { get; }
	public float[] HeightAboveReferenceRiverMeters { get; }

	public float RiverStartThresholdKm2 { get; private set; }
	public float MajorRiverThresholdKm2 { get; private set; }

	// Strategic layers derived from terrain, climate, hydrology, soil, geology and ecology.
	// River crossing is queried separately through the edge-transition API;
	// suitability scores remain strategic-scale estimates.
	public float[] ArmyMovementCostMultiplier { get; }
	public float[] ConstructionSuitability { get; }

	public bool ClimateCalculated { get; internal set; }
	public bool DerivedLayersCalculated { get; private set; }

	public WorldData(int width, int height, int seed, ClimateModelSettings? climateSettings = null)
	{
		if (width < 3)
			throw new ArgumentOutOfRangeException(nameof(width), "World width must be at least 3 cells.");

		if (height < 3)
			throw new ArgumentOutOfRangeException(nameof(height), "World height must be at least 3 cells.");

		Width = width;
		Height = height;
		Seed = seed;
		ClimateSettings = climateSettings?.Clone() ?? new ClimateModelSettings();
		ClimateSettings.Normalize();

		int size = checked(width * height);

		Continentalness = new float[size];
		ElevationMeters = new float[size];
		IsLand = new bool[size];

		IsCoastal = new bool[size];
		LocalReliefMeters = new float[size];
		SlopeDegrees = new float[size];
		AspectDegrees = new float[size];
		TerrainTypes = new TerrainType[size];

		int monthlySize = checked(size * MonthsPerYear);
		MonthlyTemperatureC = new float[monthlySize];
		MonthlyPrecipitationMm = new float[monthlySize];
		MonthlySnowfallMm = new float[monthlySize];
		MonthlySnowmeltMm = new float[monthlySize];
		MonthlyRunoffMm = new float[monthlySize];
		MeanAnnualTemperatureC = new float[size];
		AnnualPrecipitationMm = new float[size];
		AnnualPotentialEvapotranspirationMm = new float[size];
		AnnualRunoffMm = new float[size];
		AridityIndex = new float[size];
		PrevailingWindDirectionDegrees = new float[size];
		WettestMonth = new int[size];
		DriestMonth = new int[size];
		Biomes = new BiomeType[size];
		ClimateZones = new ClimateZoneType[size];
		AridityZones = new AridityZoneType[size];
		PressureBelts = new PressureBeltType[size];
		TemperatureRangeC = new float[size];
		PrecipitationSeasonalityIndex = new float[size];
		GrowingSeasonMonths = new int[size];
		MonthlyPotentialEvapotranspirationMm = new float[monthlySize];
		MonthlyActualEvapotranspirationMm = new float[monthlySize];
		MonthlyWindDirectionDegrees = new float[monthlySize];
		MonthlyWindSpeedMps = new float[monthlySize];
		MonthlyConvectivePrecipitationMm = new float[monthlySize];
		MonthlyOrographicPrecipitationMm = new float[monthlySize];
		MonthlyRainShadowReductionMm = new float[monthlySize];
		MonthlyGroundwaterRechargeMm = new float[monthlySize];
		AnnualActualEvapotranspirationMm = new float[size];
		AnnualGroundwaterRechargeMm = new float[size];
		WaterAvailabilityMm = new float[size];
		GroundwaterPotential = new float[size];
		FreshwaterAccess = new float[size];
		PeakMonthlyDischargeM3PerSecond = new float[size];
		DischargeSeasonalityIndex = new float[size];
		LakeWaterBalanceMm = new float[size];
		MonthlyITCZLatitudeDegrees = new float[MonthsPerYear];

		RockTypes = new RockType[size];
		TectonicProvinceIds = new int[size];
		GeologicalStability = new float[size];
		SeismicRisk = new float[size];
		SoilTypes = new SoilType[size];
		SoilFertility = new float[size];
		SoilDepthMeters = new float[size];
		SoilInfiltrationRateMmPerHour = new float[size];
		SoilDrainage = new float[size];
		SoilErosionRisk = new float[size];
		SoilStability = new float[size];

		VegetationTypes = new VegetationType[size];
		VegetationDensity = new float[size];
		ForestDensity = new float[size];
		GrasslandCoverage = new float[size];
		DesertCoverage = new float[size];
		WetlandCoverage = new float[size];
		VegetationWaterDemand = new float[size];

		ResourceAbundance = new float[checked(size * NaturalResourceCount)];
		ResourceAccessibility = new float[checked(size * NaturalResourceCount)];
		AgriculturalSuitability = new float[size];
		SettlementSuitability = new float[size];
		InfantryMovementCostMultiplier = new float[size];
		CavalryMovementCostMultiplier = new float[size];
		VehicleMovementCostMultiplier = new float[size];

		FilledElevationMeters = new float[size];
		DepressionDepthMeters = new float[size];
		FlowToCell = new int[size];
		FlowAccumulationKm2 = new float[size];
		FlowAccumulationRunoffM3PerYear = new float[size];
		MeanAnnualDischargeM3PerSecond = new float[size];
		MonthlyDischargeM3PerSecond = new float[monthlySize];
		RiverFlowRegimes = new RiverFlowRegime[size];
		IsRiver = new bool[size];
		IsLake = new bool[size];
		RiverOrder = new int[size];
		RiverCrossingCostMultiplier = new float[size];

		RegionalFloodSusceptibility = new float[size];
		IsElevatedFloodSusceptibility = new bool[size];
		FloodplainReferenceRiverDistanceKm = new float[size];
		HeightAboveReferenceRiverMeters = new float[size];

		ArmyMovementCostMultiplier = new float[size];
		ConstructionSuitability = new float[size];
	}

	/// <summary>Longitude wraps around the globe; latitude is clamped at the poles.</summary>
	public int GetIndex(int x, int y)
	{
		int wrappedX = ((x % Width) + Width) % Width;
		int clampedY = Math.Clamp(y, 0, Height - 1);
		return clampedY * Width + wrappedX;
	}

	/// <summary>
	/// Approximate cell surface-area correction for an equirectangular map.
	/// Cells become smaller toward the poles.
	/// </summary>
	public float GetCellAreaFactor(int y)
	{
		float latitude = Mathf.Pi * (0.5f - ((y + 0.5f) / Height));
		return Mathf.Max(0.0001f, Mathf.Cos(latitude));
	}

	/// <summary>
	/// Returns the approximate east-west and north-south dimensions, in km,
	/// of the relief-analysis window at the supplied row.
	/// </summary>
	public Vector2 GetReliefWindowDimensionsKm(int y)
	{
		int clampedY = Math.Clamp(y, 0, Height - 1);
		double latitude = Math.PI * (0.5 - ((clampedY + 0.5) / Height));
		double cosLatitude = Math.Max(Math.Cos(latitude), 0.0001);
		double eastWestCellMeters = (2.0 * Math.PI * EarthRadiusMeters * cosLatitude) / Width;
		double northSouthCellMeters = (Math.PI * EarthRadiusMeters) / Height;

		return new Vector2(
			(float)(eastWestCellMeters * ReliefWindowSize / 1000.0),
			(float)(northSouthCellMeters * ReliefWindowSize / 1000.0)
		);
	}

	/// <summary>
	/// Approximate spherical surface area of one equirectangular grid cell, in km².
	/// This corrects catchment accumulation for the shrinking cell area toward the poles.
	/// </summary>
	public float GetCellAreaKm2(int y)
	{
		int row = Math.Clamp(y, 0, Height - 1);
		double latitudeNorth = Math.PI * 0.5 - (row * Math.PI / Height);
		double latitudeSouth = Math.PI * 0.5 - ((row + 1) * Math.PI / Height);
		double longitudeWidth = (2.0 * Math.PI) / Width;
		double area = EarthRadiusMeters * EarthRadiusMeters * longitudeWidth *
			(Math.Sin(latitudeNorth) - Math.Sin(latitudeSouth));

		return (float)(area / 1_000_000.0);
	}

	public int GetResourceIndex(NaturalResourceType resource, int cellIndex)
	{
		if (cellIndex < 0 || cellIndex >= Width * Height)
			throw new ArgumentOutOfRangeException(nameof(cellIndex));
		int resourceIndex = (int)resource;
		if (resourceIndex < 0 || resourceIndex >= NaturalResourceCount)
			throw new ArgumentOutOfRangeException(nameof(resource));
		return resourceIndex * Width * Height + cellIndex;
	}

	public int GetMonthlyIndex(int cellIndex, int month)
	{
		if (cellIndex < 0 || cellIndex >= Width * Height)
			throw new ArgumentOutOfRangeException(nameof(cellIndex));
		if (month < 0 || month >= MonthsPerYear)
			throw new ArgumentOutOfRangeException(nameof(month));
		return month * Width * Height + cellIndex;
	}

	/// <summary>
	/// Calculates every layer derived from elevation and land data.
	/// Call after the generator has populated ElevationMeters and IsLand.
	/// </summary>
	public void CalculateDerivedLayers()
	{
		WorldGenerationPipeline.Calculate(this);
	}

	internal void ResetDerivedLayerState()
	{
		DerivedLayersCalculated = false;
		ClimateCalculated = false;
	}

	internal void MarkDerivedLayersCalculated()
	{
		DerivedLayersCalculated = true;
	}

	internal void CalculateCoastalLayer()
	{
		for (int y = 0; y < Height; y++)
		{
			for (int x = 0; x < Width; x++)
			{
				int index = GetIndex(x, y);
				bool currentIsLand = IsLand[index];
				bool coastal = false;

				// Eight-neighbor boundary test. Longitude wraps at the map edges.
				for (int offsetY = -1; offsetY <= 1 && !coastal; offsetY++)
				{
					int neighborY = y + offsetY;
					if (neighborY < 0 || neighborY >= Height)
						continue;

					for (int offsetX = -1; offsetX <= 1; offsetX++)
					{
						if (offsetX == 0 && offsetY == 0)
							continue;

						int neighborIndex = GetIndex(x + offsetX, neighborY);
					if (IsLand[neighborIndex] != currentIsLand)
					{
						coastal = true;
						break;
					}
					}
				}

				IsCoastal[index] = coastal;
			}
		}
	}

	internal void CalculateLocalReliefLayer()
	{
		// Local relief is the elevation range inside a 3x3 cell window.
		// Land and water are measured separately so coastlines do not create
		// artificial cliffs in the relief map.
		for (int y = 0; y < Height; y++)
		{
			for (int x = 0; x < Width; x++)
			{
				int centerIndex = GetIndex(x, y);
				bool selectedIsLand = IsLand[centerIndex];

				float minimum = float.MaxValue;
				float maximum = float.MinValue;

				for (int offsetY = -ReliefRadiusCells; offsetY <= ReliefRadiusCells; offsetY++)
				{
					int neighborY = y + offsetY;
					if (neighborY < 0 || neighborY >= Height)
						continue;

					for (int offsetX = -ReliefRadiusCells; offsetX <= ReliefRadiusCells; offsetX++)
					{
						int neighborIndex = GetIndex(x + offsetX, neighborY);
						if (IsLand[neighborIndex] != selectedIsLand)
							continue;

						float elevation = ElevationMeters[neighborIndex];
					minimum = Mathf.Min(minimum, elevation);
						maximum = Mathf.Max(maximum, elevation);
					}
				}

				LocalReliefMeters[centerIndex] =
					minimum == float.MaxValue ? 0.0f : maximum - minimum;
			}
		}
	}

	internal void CalculateSlopeAndAspectLayers()
	{
		// Distances are measured on a spherical Earth using the map's
		// equirectangular grid. This preserves real slope angles rather than
		// exaggerating them to make the map look more dramatic.
		double northSouthCellSize = Math.PI * EarthRadiusMeters / Height;

		for (int y = 0; y < Height; y++)
		{
			double latitude = Math.PI * (0.5 - ((y + 0.5) / Height));
			double cosLatitude = Math.Max(Math.Cos(latitude), 0.0001);
			double eastWestCellSize =
				(2.0 * Math.PI * EarthRadiusMeters * cosLatitude) / Width;

			for (int x = 0; x < Width; x++)
			{
				int index = GetIndex(x, y);
				bool selectedIsLand = IsLand[index];
				float center = ElevationMeters[index];

				bool hasWest = TryGetSameSurfaceElevation(x - 1, y, selectedIsLand, out float west);
				bool hasEast = TryGetSameSurfaceElevation(x + 1, y, selectedIsLand, out float east);
				bool hasNorth = TryGetSameSurfaceElevation(x, y - 1, selectedIsLand, out float north);
				bool hasSouth = TryGetSameSurfaceElevation(x, y + 1, selectedIsLand, out float south);

				// Central differences are preferred. At coastlines and map edges,
				// use a one-sided difference where a same-surface neighbor exists.
				double dzEast = 0.0;
				if (hasWest && hasEast)
					dzEast = (east - west) / (2.0 * eastWestCellSize);
				else if (hasEast)
					dzEast = (east - center) / eastWestCellSize;
				else if (hasWest)
					dzEast = (center - west) / eastWestCellSize;

				double dzNorth = 0.0;
				if (hasNorth && hasSouth)
					dzNorth = (north - south) / (2.0 * northSouthCellSize);
				else if (hasNorth)
					dzNorth = (north - center) / northSouthCellSize;
				else if (hasSouth)
					dzNorth = (center - south) / northSouthCellSize;

				double gradientMagnitude = Math.Sqrt(
					dzEast * dzEast + dzNorth * dzNorth
				);

				float slope = (float)(Math.Atan(gradientMagnitude) * 180.0 / Math.PI);
				SlopeDegrees[index] = slope;

				// Aspect is the compass direction of steepest descent.
				// -1 means the surface is too flat for a meaningful direction.
				if (slope < 0.05f)
				{
					AspectDegrees[index] = -1.0f;
					continue;
				}

				double downhillEast = -dzEast;
				double downhillNorth = -dzNorth;
				double aspect = Math.Atan2(downhillEast, downhillNorth) * 180.0 / Math.PI;

				if (aspect < 0.0)
					aspect += 360.0;

				AspectDegrees[index] = (float)aspect;
			}
		}
	}

	private bool TryGetSameSurfaceElevation(
		int x,
		int y,
		bool targetIsLand,
		out float elevation)
	{
		elevation = 0.0f;

		if (y < 0 || y >= Height)
			return false;

		int index = GetIndex(x, y);
		if (IsLand[index] != targetIsLand)
			return false;

		elevation = ElevationMeters[index];
		return true;
	}

	internal void CalculateTerrainTypes()
	{
		for (int i = 0; i < ElevationMeters.Length; i++)
		{
			float elevation = ElevationMeters[i];
			bool land = IsLand[i];
			bool coastal = IsCoastal[i];
			float relief = LocalReliefMeters[i];
			float slope = SlopeDegrees[i];

			if (!land)
			{
				if (coastal)
					TerrainTypes[i] = TerrainType.CoastalWater;
				else if (Math.Abs(elevation) < 200.0f)
					TerrainTypes[i] = TerrainType.ShallowSea;
				else if (Math.Abs(elevation) < 2000.0f)
					TerrainTypes[i] = TerrainType.ContinentalShelf;
				else
					TerrainTypes[i] = TerrainType.DeepOcean;

				continue;
			}

			if (elevation < 250.0f)
			{
				TerrainTypes[i] = coastal
					? TerrainType.CoastalLowland
					: TerrainType.Lowland;
			}
			else if (elevation < 900.0f)
			{
				// At this elevation band, relief and slope distinguish plains
				// from rolling and rugged hill country.
				if (relief < 80.0f && slope < 0.5f)
				{
					TerrainTypes[i] = coastal
						? TerrainType.CoastalPlain
						: TerrainType.Plains;
				}
				else if (relief < 300.0f && slope < 5.0f)
				{
					TerrainTypes[i] = TerrainType.RollingHills;
				}
				else
				{
					TerrainTypes[i] = TerrainType.RuggedHills;
				}
			}
			else if (elevation < 1800.0f)
			{
				TerrainTypes[i] = TerrainType.Highlands;
			}
			else if (elevation < 3200.0f)
			{
				TerrainTypes[i] = TerrainType.Mountains;
			}
			else if (elevation < 5000.0f)
			{
				TerrainTypes[i] = TerrainType.HighMountains;
			}
			else
			{
				TerrainTypes[i] = TerrainType.AlpinePeaks;
			}
		}
	}

	// ==========================================
	// HYDROLOGY: FLOW ROUTING, RIVERS AND LAKES
	// ==========================================

	private const float LakeMinimumDepressionDepthMeters = 120.0f;

	/// <summary>
	/// Builds a drainage network using a priority-flood depression-filling
	/// algorithm. Each land cell points to a downstream cell, then upstream
	/// contributing surface area is accumulated through that network.
	///
	/// Drainage direction remains terrain-driven; channel selection uses climate- and soil-derived runoff. Monthly discharge is routed through the same drainage tree.
	/// </summary>
	internal void CalculateHydrologyLayer()
	{
		int size = Width * Height;

		Array.Clear(IsRiver, 0, IsRiver.Length);
		Array.Clear(IsLake, 0, IsLake.Length);
		Array.Clear(RiverOrder, 0, RiverOrder.Length);
		Array.Clear(FlowAccumulationKm2, 0, FlowAccumulationKm2.Length);
		Array.Clear(FlowAccumulationRunoffM3PerYear, 0, FlowAccumulationRunoffM3PerYear.Length);
		Array.Clear(MeanAnnualDischargeM3PerSecond, 0, MeanAnnualDischargeM3PerSecond.Length);
		Array.Clear(MonthlyDischargeM3PerSecond, 0, MonthlyDischargeM3PerSecond.Length);
		Array.Clear(RiverFlowRegimes, 0, RiverFlowRegimes.Length);
		Array.Clear(DepressionDepthMeters, 0, DepressionDepthMeters.Length);
		Array.Fill(FlowToCell, -1);
		Array.Fill(RiverCrossingCostMultiplier, 1.0f);

		List<int> floodOrder = BuildFlowRouting();

		// Give every land cell its real approximate spherical surface area.
		for (int y = 0; y < Height; y++)
		{
			float cellArea = GetCellAreaKm2(y);

			for (int x = 0; x < Width; x++)
			{
				int index = GetIndex(x, y);

				if (IsLand[index])
				{
					FlowAccumulationKm2[index] = cellArea;
					// 1 mm over 1 km² equals 1,000 m³.
					FlowAccumulationRunoffM3PerYear[index] = AnnualRunoffMm[index] * cellArea * 1000.0f;
				}

				DepressionDepthMeters[index] = IsLand[index]
					? Mathf.Max(0.0f, FilledElevationMeters[index] - ElevationMeters[index])
					: 0.0f;
			}
		}

		// Process upstream cells before their downstream receiver.
		for (int i = floodOrder.Count - 1; i >= 0; i--)
		{
			int cell = floodOrder[i];
			int downstream = FlowToCell[cell];

			if (downstream >= 0)
			{
				FlowAccumulationKm2[downstream] += FlowAccumulationKm2[cell];
				FlowAccumulationRunoffM3PerYear[downstream] += FlowAccumulationRunoffM3PerYear[cell];
			}
		}

		// Route each month's runoff through the same terrain-defined drainage network.
		// The resulting discharge is a monthly mean in m³/s, not a flood peak.
		double secondsPerMonth = SecondsPerYear / MonthsPerYear;
		for (int month = 0; month < MonthsPerYear; month++)
		{
			int monthlyOffset = month * size;
			for (int y = 0; y < Height; y++)
			{
				float cellArea = GetCellAreaKm2(y);
				for (int x = 0; x < Width; x++)
				{
					int index = GetIndex(x, y);
					if (!IsLand[index])
						continue;

					int monthlyIndex = monthlyOffset + index;
					MonthlyDischargeM3PerSecond[monthlyIndex] =
						MonthlyRunoffMm[monthlyIndex] * cellArea * 1000.0f;
				}
			}

			for (int i = floodOrder.Count - 1; i >= 0; i--)
			{
				int cell = floodOrder[i];
				int downstream = FlowToCell[cell];
				if (downstream >= 0)
				{
					int sourceMonthlyIndex = monthlyOffset + cell;
					int downstreamMonthlyIndex = monthlyOffset + downstream;
					MonthlyDischargeM3PerSecond[downstreamMonthlyIndex] +=
						MonthlyDischargeM3PerSecond[sourceMonthlyIndex];
				}
			}

			for (int i = 0; i < size; i++)
			{
				int monthlyIndex = monthlyOffset + i;
				MonthlyDischargeM3PerSecond[monthlyIndex] =
					(float)(MonthlyDischargeM3PerSecond[monthlyIndex] / secondsPerMonth);
			}
		}

		IdentifyInlandLakes();

		double averageCellAreaKm2 =
			(4.0 * Math.PI * EarthRadiusMeters * EarthRadiusMeters) /
			(Width * (double)Height) / 1_000_000.0;

		RiverStartThresholdKm2 = (float)Math.Max(10_000.0, averageCellAreaKm2 * 5.0);
		MajorRiverThresholdKm2 = (float)Math.Max(80_000.0, averageCellAreaKm2 * 30.0);

		for (int i = 0; i < size; i++)
			MeanAnnualDischargeM3PerSecond[i] = (float)(FlowAccumulationRunoffM3PerYear[i] / SecondsPerYear);

		int riverCellCount = 0;
		int lakeCellCount = 0;

		for (int i = 0; i < size; i++)
		{
			if (IsLake[i])
			{
				lakeCellCount++;
				RiverCrossingCostMultiplier[i] = 1.0f;
				continue;
			}

			if (!IsLand[i])
			{
				RiverCrossingCostMultiplier[i] = 0.0f;
				continue;
			}

			float catchmentArea = FlowAccumulationKm2[i];
			int activeMonths = 0;
			for (int month = 0; month < MonthsPerYear; month++)
			{
				float monthlyDischarge = MonthlyDischargeM3PerSecond[month * size + i];
				if (monthlyDischarge >= 0.5f)
					activeMonths++;
			}

			if (catchmentArea >= RiverStartThresholdKm2 && activeMonths > 0)
			{
				IsRiver[i] = true;

				if (activeMonths >= 9)
					RiverFlowRegimes[i] = RiverFlowRegime.Perennial;
				else if (activeMonths >= 4)
					RiverFlowRegimes[i] = RiverFlowRegime.Seasonal;
				else
					RiverFlowRegimes[i] = RiverFlowRegime.Intermittent;
				riverCellCount++;

				// Store one full provisional crossing class on the channel cell.
				// It is no longer multiplied into every river cell movement cost;
				// TryGetArmyTransitionCostMultiplier applies it on channel entry.
				if (catchmentArea >= MajorRiverThresholdKm2 * 3.0f)
					RiverCrossingCostMultiplier[i] = 2.80f;
				else if (catchmentArea >= MajorRiverThresholdKm2)
					RiverCrossingCostMultiplier[i] = 2.20f;
				else if (catchmentArea >= RiverStartThresholdKm2 * 3.0f)
					RiverCrossingCostMultiplier[i] = 1.70f;
				else
					RiverCrossingCostMultiplier[i] = 1.35f;
			}
		}

		// Stream order is calculated only on cells that meet the river threshold.
		CalculateStrahlerRiverOrders(floodOrder);

		GD.Print("HYDROLOGY GENERATED");
		GD.Print($"River cells: {riverCellCount}");
		GD.Print($"Lake cells: {lakeCellCount}");
		GD.Print($"River threshold: {RiverStartThresholdKm2:N0} km² catchment + at least one active runoff month");
		GD.Print($"Major river threshold: {MajorRiverThresholdKm2:N0} km² catchment");
	}

	/// <summary>
	/// Estimates regional river-corridor flood susceptibility around mapped channels.
	/// The score combines river proximity, relative elevation, upstream catchment,
	/// local slope and local relief. It is intended for strategic-scale gameplay
	/// on the current coarse global grid, not as a floodplain boundary or parcel-level
	/// flood prediction. Annual runoff now influences river selection, but this remains a coarse susceptibility proxy, not a hydraulic flood-depth or inundation model.
	/// </summary>
	internal void CalculateRegionalFloodSusceptibilityLayer()
	{
		Array.Clear(RegionalFloodSusceptibility, 0, RegionalFloodSusceptibility.Length);
		Array.Clear(IsElevatedFloodSusceptibility, 0, IsElevatedFloodSusceptibility.Length);
		Array.Fill(FloodplainReferenceRiverDistanceKm, float.PositiveInfinity);
		Array.Fill(HeightAboveReferenceRiverMeters, float.PositiveInfinity);

		double northSouthCellKm = (Math.PI * EarthRadiusMeters / Height) / 1000.0;
		double searchRadiusCells = RegionalFloodSusceptibilitySearchRadiusCells + 0.5;

		for (int y = 0; y < Height; y++)
		{
			double latitude = Math.PI * (0.5 - ((y + 0.5) / Height));
			double eastWestCellKm =
				(2.0 * Math.PI * EarthRadiusMeters * Math.Max(Math.Cos(latitude), 0.0001)) /
				Width / 1000.0;

			double maximumDistanceKm = Math.Sqrt(
				Math.Pow(searchRadiusCells * eastWestCellKm, 2.0) +
				Math.Pow(searchRadiusCells * northSouthCellKm, 2.0)
			);

			for (int x = 0; x < Width; x++)
			{
				int index = GetIndex(x, y);

				// Water cells are handled as water by the map and construction systems,
				// so this layer only classifies potentially flood-prone land cells.
				if (!IsLand[index] || IsRiver[index] || IsLake[index])
					continue;

				float bestRisk = 0.0f;
				double nearestRiverDistanceKm = double.PositiveInfinity;
				float nearestRiverHeightDifference = float.PositiveInfinity;

				for (int offsetY = -RegionalFloodSusceptibilitySearchRadiusCells;
					offsetY <= RegionalFloodSusceptibilitySearchRadiusCells;
					offsetY++)
				{
					int neighborY = y + offsetY;
					if (neighborY < 0 || neighborY >= Height)
						continue;

					for (int offsetX = -RegionalFloodSusceptibilitySearchRadiusCells;
						offsetX <= RegionalFloodSusceptibilitySearchRadiusCells;
						offsetX++)
					{
						if (offsetX == 0 && offsetY == 0)
							continue;

						int riverIndex = GetIndex(x + offsetX, neighborY);
						if (!IsRiver[riverIndex])
							continue;

						double eastDistanceKm = offsetX * eastWestCellKm;
						double northDistanceKm = offsetY * northSouthCellKm;
						double distanceKm = Math.Sqrt(
							eastDistanceKm * eastDistanceKm + northDistanceKm * northDistanceKm
						);

						if (distanceKm <= 0.0 || distanceKm > maximumDistanceKm)
							continue;

						float heightAboveRiver = Mathf.Max(
							0.0f,
							ElevationMeters[index] - ElevationMeters[riverIndex]
						);

						if (distanceKm < nearestRiverDistanceKm)
						{
							nearestRiverDistanceKm = distanceKm;
							nearestRiverHeightDifference = heightAboveRiver;
						}

						float distanceFactor = 1.0f - Mathf.Clamp(
							(float)(distanceKm / maximumDistanceKm), 0.0f, 1.0f
						);

						float heightFactor = 1.0f - Mathf.Clamp(heightAboveRiver / 250.0f, 0.0f, 1.0f);

						float catchmentFactor = 0.35f + 0.65f * Mathf.Clamp(
							FlowAccumulationKm2[riverIndex] / Mathf.Max(MajorRiverThresholdKm2, 1.0f),
							0.0f,
							1.0f
						);
						float seasonalFlowFactor = 0.85f + 0.30f * Mathf.Clamp(
							DischargeSeasonalityIndex[riverIndex], 0.0f, 1.0f);

						float slopeFactor = 1.0f - Mathf.Clamp(SlopeDegrees[index] / 12.0f, 0.0f, 1.0f);
						float reliefFactor = 1.0f - 0.65f * Mathf.Clamp(
							LocalReliefMeters[index] / 600.0f, 0.0f, 1.0f
						);

						float risk = 100.0f * distanceFactor * heightFactor *
							catchmentFactor * slopeFactor * reliefFactor * seasonalFlowFactor;

						if (risk > bestRisk)
							bestRisk = risk;
					}
				}

				RegionalFloodSusceptibility[index] = Mathf.Clamp(bestRisk, 0.0f, 100.0f);
				IsElevatedFloodSusceptibility[index] =
					RegionalFloodSusceptibility[index] >= RegionalFloodSusceptibilityThreshold;

				if (!double.IsPositiveInfinity(nearestRiverDistanceKm))
				{
					FloodplainReferenceRiverDistanceKm[index] = (float)nearestRiverDistanceKm;
					HeightAboveReferenceRiverMeters[index] = nearestRiverHeightDifference;
				}
			}
		}
	}

	/// <summary>
	/// Priority-flood routing. Ocean cells are drainage outlets. The flood
	/// expands inward from the ocean, filling closed depressions to their
	/// lowest spill elevation and assigning every land cell a downstream
	/// parent. Longitude wraps around the world.
	/// </summary>
	private List<int> BuildFlowRouting()
	{
		int size = Width * Height;
		bool[] visited = new bool[size];

		Array.Fill(FilledElevationMeters, float.PositiveInfinity);
		Array.Fill(FlowToCell, -1);

		MinPriorityQueue queue = new MinPriorityQueue();
		List<int> popOrder = new List<int>(size);

		// Every ocean cell is an outlet and a starting point.
		for (int i = 0; i < size; i++)
		{
			if (IsLand[i])
				continue;

			visited[i] = true;
			FilledElevationMeters[i] = ElevationMeters[i];
			queue.Push(i, FilledElevationMeters[i]);
		}

		// Defensive fallback for a hypothetical all-land world.
		if (queue.Count == 0)
		{
			int lowestIndex = 0;

			for (int i = 1; i < size; i++)
			{
				if (ElevationMeters[i] < ElevationMeters[lowestIndex])
					lowestIndex = i;
			}

			visited[lowestIndex] = true;
			FilledElevationMeters[lowestIndex] = ElevationMeters[lowestIndex];
			queue.Push(lowestIndex, FilledElevationMeters[lowestIndex]);
		}

		while (queue.Count > 0)
		{
			HeapItem current = queue.Pop();
			int currentIndex = current.Index;
			popOrder.Add(currentIndex);

			int currentX = currentIndex % Width;
			int currentY = currentIndex / Width;

			for (int offsetY = -1; offsetY <= 1; offsetY++)
			{
				int neighborY = currentY + offsetY;

				if (neighborY < 0 || neighborY >= Height)
					continue;

				for (int offsetX = -1; offsetX <= 1; offsetX++)
				{
					if (offsetX == 0 && offsetY == 0)
						continue;

					int neighborX = (currentX + offsetX + Width) % Width;
					int neighborIndex = neighborY * Width + neighborX;

					if (visited[neighborIndex])
						continue;

					visited[neighborIndex] = true;

					float filledElevation = Mathf.Max(
						ElevationMeters[neighborIndex],
						FilledElevationMeters[currentIndex]
					);

					FilledElevationMeters[neighborIndex] = filledElevation;
					FlowToCell[neighborIndex] = currentIndex;

					queue.Push(neighborIndex, filledElevation);
				}
			}
		}

		// The cylindrical grid is connected, but retain a safe fallback if
		// malformed input ever leaves a cell unvisited.
		for (int i = 0; i < size; i++)
		{
			if (visited[i])
				continue;

			FilledElevationMeters[i] = ElevationMeters[i];
			FlowToCell[i] = -1;
			popOrder.Add(i);
		}

		return popOrder;
	}

	/// <summary>
	/// Cells inside sufficiently deep, connected filled depressions are
	/// marked as procedural inland lakes. This identifies plausible basins;
	/// it does not calculate a real lake water balance or shoreline elevation.
	/// </summary>
	private void IdentifyInlandLakes()
	{
		int size = Width * Height;
		bool[] candidate = new bool[size];
		bool[] visited = new bool[size];

		double averageCellAreaKm2 =
			(4.0 * Math.PI * EarthRadiusMeters * EarthRadiusMeters) /
			(Width * (double)Height) / 1_000_000.0;

		double minimumLakeAreaKm2 = averageCellAreaKm2 * 1.5;

		for (int i = 0; i < size; i++)
		{
			candidate[i] =
				IsLand[i] &&
				DepressionDepthMeters[i] >= LakeMinimumDepressionDepthMeters;
		}

		Queue<int> queue = new Queue<int>();
		List<int> component = new List<int>();

		for (int start = 0; start < size; start++)
		{
			if (!candidate[start] || visited[start])
				continue;

			queue.Clear();
			component.Clear();

			queue.Enqueue(start);
			visited[start] = true;

			double componentAreaKm2 = 0.0;

			while (queue.Count > 0)
			{
				int current = queue.Dequeue();
				component.Add(current);

				int x = current % Width;
				int y = current / Width;
				componentAreaKm2 += GetCellAreaKm2(y);

				for (int offsetY = -1; offsetY <= 1; offsetY++)
				{
					int neighborY = y + offsetY;

					if (neighborY < 0 || neighborY >= Height)
						continue;

					for (int offsetX = -1; offsetX <= 1; offsetX++)
					{
						if (offsetX == 0 && offsetY == 0)
							continue;

						int neighborX = (x + offsetX + Width) % Width;
						int neighbor = neighborY * Width + neighborX;

						if (!candidate[neighbor] || visited[neighbor])
							continue;

						visited[neighbor] = true;
						queue.Enqueue(neighbor);
					}
				}
			}

			if (componentAreaKm2 < minimumLakeAreaKm2)
				continue;

			foreach (int cell in component)
				IsLake[cell] = true;
		}
	}

	/// <summary>
	/// Calculates Strahler stream order from the drainage tree.
	/// Order 1 is a headwater stream; order increases where equal-order
	/// tributaries merge.
	/// </summary>
	private void CalculateStrahlerRiverOrders(List<int> floodOrder)
	{
		int size = Width * Height;
		int[] highestIncomingOrder = new int[size];
		int[] highestIncomingCount = new int[size];

		for (int i = 0; i < size; i++)
			RiverOrder[i] = IsRiver[i] ? 1 : 0;

		// Reverse flood order visits upstream cells before their receivers.
		// Only mapped channel cells participate, so diffuse overland flow does
		// not artificially inflate the stream order.
		for (int i = floodOrder.Count - 1; i >= 0; i--)
		{
			int cell = floodOrder[i];

			if (!IsRiver[cell])
				continue;

			int downstream = FlowToCell[cell];

			if (downstream < 0 || !IsRiver[downstream])
				continue;

			int incomingOrder = RiverOrder[cell];

			if (incomingOrder > highestIncomingOrder[downstream])
			{
				highestIncomingOrder[downstream] = incomingOrder;
				highestIncomingCount[downstream] = 1;
			}
			else if (incomingOrder == highestIncomingOrder[downstream])
			{
				highestIncomingCount[downstream]++;
			}

			RiverOrder[downstream] =
				highestIncomingOrder[downstream] +
				(highestIncomingCount[downstream] >= 2 ? 1 : 0);
		}
	}

	private readonly struct HeapItem
	{
		public int Index { get; }
		public float Priority { get; }

		public HeapItem(int index, float priority)
		{
			Index = index;
			Priority = priority;
		}
	}

	/// <summary>A small binary min-heap used by the priority-flood algorithm.</summary>
	private sealed class MinPriorityQueue
	{
		private readonly List<HeapItem> _items = new List<HeapItem>();

		public int Count => _items.Count;

		public void Push(int index, float priority)
		{
			HeapItem item = new HeapItem(index, priority);
			int child = _items.Count;
			_items.Add(item);

			while (child > 0)
			{
				int parent = (child - 1) / 2;

				if (_items[parent].Priority <= item.Priority)
					break;

				_items[child] = _items[parent];
				child = parent;
			}

			_items[child] = item;
		}

		public HeapItem Pop()
		{
			if (_items.Count == 0)
				throw new InvalidOperationException("Cannot pop an empty priority queue.");

			HeapItem root = _items[0];
			int lastIndex = _items.Count - 1;
			HeapItem last = _items[lastIndex];
			_items.RemoveAt(lastIndex);

			if (_items.Count == 0)
				return root;

			int parent = 0;

			while (true)
			{
				int left = parent * 2 + 1;
				if (left >= _items.Count)
					break;

				int right = left + 1;
				int smallerChild = right < _items.Count &&
					_items[right].Priority < _items[left].Priority
					? right
					: left;

				if (_items[smallerChild].Priority >= last.Priority)
					break;

				_items[parent] = _items[smallerChild];
				parent = smallerChild;
			}

			_items[parent] = last;
			return root;
		}
	}

	// ==========================================
	// STRATEGIC TERRAIN LAYERS
	// ==========================================

	/// <summary>
	/// Calculates the baseline off-road terrain movement cost for land armies.
	/// 1.0 is the cost of entering one ordinary plains cell. Ocean and lake cells
	/// are stored as 0. River penalties are deliberately excluded here and are
	/// applied separately by TryGetArmyTransitionCostMultiplier when entering a
	/// mapped channel cell. Roads, bridges, weather, unit type and supply are not
	/// modelled yet.
	/// </summary>
	internal void CalculateArmyMovementCostLayer()
	{
		for (int i = 0; i < ElevationMeters.Length; i++)
		{
			if (!IsLand[i] || IsLake[i])
			{
				ArmyMovementCostMultiplier[i] = 0.0f;
				continue;
			}

			float terrainCost = TerrainTypes[i] switch
			{
				TerrainType.CoastalLowland => 1.05f,
				TerrainType.Lowland => 0.95f,
				TerrainType.CoastalPlain => 1.05f,
				TerrainType.Plains => 1.00f,
				TerrainType.RollingHills => 1.35f,
				TerrainType.RuggedHills => 1.75f,
				TerrainType.Highlands => 1.70f,
				TerrainType.Mountains => 2.50f,
				TerrainType.HighMountains => 4.00f,
				TerrainType.AlpinePeaks => 6.00f,
				_ => 1.00f
			};

			float slope = SlopeDegrees[i];
			float slopeCost = slope switch
			{
				< 0.10f => 1.00f,
				< 0.50f => 1.05f,
				< 2.00f => 1.10f,
				< 5.00f => 1.25f,
				< 15.0f => 1.60f,
				< 30.0f => 2.30f,
				< 45.0f => 3.50f,
				_ => 5.00f
			};

			float relief = LocalReliefMeters[i];
			float reliefCost = relief switch
			{
				< 25.0f => 1.00f,
				< 75.0f => 1.05f,
				< 150.0f => 1.10f,
				< 300.0f => 1.20f,
				< 600.0f => 1.35f,
				< 1000.0f => 1.55f,
				_ => 1.80f
			};

			float combinedCost = terrainCost * slopeCost * reliefCost;
			ArmyMovementCostMultiplier[i] = Mathf.Clamp(combinedCost, 0.80f, 15.0f);
		}
	}

	/// <summary>
	/// Returns whether an ordinary land army may occupy this cell. River-bearing
	/// land cells remain traversable; ocean and inland-lake cells do not.
	/// </summary>
	public bool CanArmyTraverseCell(int index)
	{
		return index >= 0 && index < IsLand.Length && IsLand[index] && !IsLake[index];
	}

	/// <summary>
	/// Calculates the movement multiplier for one adjacent grid transition.
	/// The destination cell supplies the terrain cost. A river penalty is charged
	/// once when entering a mapped river-channel cell from ordinary land, rather
	/// than being charged on every river cell. This is a strategic grid-level
	/// approximation; exact bank-to-bank crossing geometry and bridges are not
	/// represented. The caller should account for diagonal/world-distance costs.
	/// Longitude adjacency wraps around the globe. Returns false for blocked or
	/// non-adjacent transitions.
	/// </summary>
	public bool TryGetArmyTransitionCostMultiplier(
		int fromIndex,
		int toIndex,
		out float multiplier)
	{
		multiplier = 0.0f;

		if (!DerivedLayersCalculated)
			return false;

		if (!CanArmyTraverseCell(fromIndex) || !CanArmyTraverseCell(toIndex) || fromIndex == toIndex)
			return false;

		int fromX = fromIndex % Width;
		int fromY = fromIndex / Width;
		int toX = toIndex % Width;
		int toY = toIndex / Width;

		int deltaX = Math.Abs(toX - fromX);
		deltaX = Math.Min(deltaX, Width - deltaX);
		int deltaY = Math.Abs(toY - fromY);

		if (deltaX > 1 || deltaY > 1)
			return false;

		float transitionCost = ArmyMovementCostMultiplier[toIndex];
		if (transitionCost <= 0.0f)
			return false;

		// Charge the channel's full provisional crossing class once, on the first
		// land-to-channel edge. Moving along or out of the channel adds no repeat cost.
		if (!IsRiver[fromIndex] && IsRiver[toIndex])
			transitionCost *= Mathf.Max(1.0f, RiverCrossingCostMultiplier[toIndex]);

		multiplier = Mathf.Clamp(transitionCost, 0.80f, 45.0f);
		return true;
	}

	/// <summary>
	/// Calculates a 0-100 terrain-based construction suitability index.
	/// It penalizes steep, high-relief and high-elevation locations and applies
	/// graded penalties for regional river-corridor susceptibility and river-bearing
	/// cells. Only ocean and inland-lake cells are hard-excluded. A river channel
	/// intersects a coarse land cell; it does not make the entire cell unbuildable.
	/// Actual channel footprint, flood depth, soil, geology, seismic risk, resources,
	/// access, ownership and building standards are not modelled.
	/// </summary>
	internal void CalculateConstructionSuitabilityLayer()
	{
		for (int i = 0; i < ElevationMeters.Length; i++)
		{
			// Only open water and inland-lake cells are hard exclusions. A mapped
			// river intersects this coarse land cell but does not occupy its full area.
			if (!IsLand[i] || IsLake[i])
			{
				ConstructionSuitability[i] = 0.0f;
				continue;
			}

			float score = TerrainTypes[i] switch
			{
				TerrainType.CoastalLowland => 80.0f,
				TerrainType.Lowland => 90.0f,
				TerrainType.CoastalPlain => 92.0f,
				TerrainType.Plains => 96.0f,
				TerrainType.RollingHills => 78.0f,
				TerrainType.RuggedHills => 58.0f,
				TerrainType.Highlands => 52.0f,
				TerrainType.Mountains => 28.0f,
				TerrainType.HighMountains => 12.0f,
				TerrainType.AlpinePeaks => 2.0f,
				_ => 50.0f
			};

			float slope = SlopeDegrees[i];
			float slopePenalty = slope switch
			{
				< 0.10f => 0.0f,
				< 0.50f => 2.0f,
				< 2.00f => 5.0f,
				< 5.00f => 10.0f,
				< 15.0f => 22.0f,
				< 30.0f => 40.0f,
				< 45.0f => 60.0f,
				_ => 80.0f
			};

			float relief = LocalReliefMeters[i];
			float reliefPenalty = relief switch
			{
				< 25.0f => 0.0f,
				< 75.0f => 3.0f,
				< 150.0f => 7.0f,
				< 300.0f => 15.0f,
				< 600.0f => 25.0f,
				< 1000.0f => 40.0f,
				_ => 55.0f
			};

			float elevation = ElevationMeters[i];
			float elevationPenalty = elevation switch
			{
				>= 4500.0f => 20.0f,
				>= 3500.0f => 12.0f,
				>= 2500.0f => 7.0f,
				>= 1500.0f => 3.0f,
				_ => 0.0f
			};

			// Regional susceptibility remains a graded penalty, not a hard exclusion.
			float floodplainPenalty = RegionalFloodSusceptibility[i] * 0.25f;

			// A channel-bearing cell receives a modest uncertainty penalty. At this
			// resolution we cannot identify the precise channel footprint or nearby
			// buildable ground within the same cell.
			float riverCellPenalty = IsRiver[i] ? RiverCellConstructionPenalty : 0.0f;

			ConstructionSuitability[i] = Mathf.Clamp(
				score - slopePenalty - reliefPenalty - elevationPenalty - floodplainPenalty - riverCellPenalty,
				0.0f,
				100.0f
			);
		}
	}

}

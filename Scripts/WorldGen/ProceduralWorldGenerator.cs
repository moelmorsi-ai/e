#nullable enable

using Godot;
using System;
using BronzeAge;

public partial class ProceduralWorldGenerator : Node
{
	[Export]
	public int Seed { get; set; } = 123456;

	[Export]
	public int MapWidth { get; set; } = 512;

	[Export]
	public int MapHeight { get; set; } = 256;

	[Export(PropertyHint.Range, "0.20,0.45,0.01")]
	public float TargetLandFraction { get; set; } = 0.30f;

	[Export]
	public bool GenerateOnReady { get; set; } = true;

	[Export(PropertyHint.Range, "3.0,9.0,0.1")]
	public float EnvironmentalLapseRateCPerKm { get; set; } = 6.5f;

	[Export(PropertyHint.Range, "0.0,20.0,0.5")]
	public float ITCZSeasonalAmplitudeDegrees { get; set; } = 7.5f;

	[Export(PropertyHint.Range, "0.01,0.20,0.01")]
	public float HyperAridUpperAridityIndex { get; set; } = 0.05f;

	[Export(PropertyHint.Range, "0.02,0.60,0.01")]
	public float AridUpperAridityIndex { get; set; } = 0.20f;

	[Export(PropertyHint.Range, "0.03,1.0,0.01")]
	public float SemiAridUpperAridityIndex { get; set; } = 0.50f;

	[Export(PropertyHint.Range, "0.04,1.5,0.01")]
	public float DrySubhumidUpperAridityIndex { get; set; } = 0.65f;

	[Export(PropertyHint.Range, "0.10,0.80,0.01")]
	public float RainShadowMinimumTransmission { get; set; } = 0.34f;

	[Export(PropertyHint.Range, "1,12,1")]
	public int OrographicSampleSteps { get; set; } = 6;

	public WorldData? GeneratedWorld { get; private set; }

	public override void _Ready()
	{
		if (GenerateOnReady)
			GenerateAndDisplay();
	}

	// ==========================================
	// MAIN GENERATION ENTRY POINT
	// ==========================================

	public void GenerateAndDisplay()
	{
		Node? parent = GetParent();

		Sprite2D? target =
			parent?.GetNodeOrNull<Sprite2D>("MapRenderer");

		if (target == null)
		{
			GD.PushError(
                "WorldImage Sprite2D not found in parent node!"
			);
			return;
		}

		if (MapWidth < 64 || MapHeight < 32)
		{
			GD.PushError("World resolution is too small.");
			return;
		}

		WorldGenerationSettings settings =
			new WorldGenerationSettings
			{
				Seed = Seed,
				Width = MapWidth,
				Height = MapHeight,
				TargetLandFraction = TargetLandFraction,
				ClimateSettings = new ClimateModelSettings
				{
					EnvironmentalLapseRateCPerKm = EnvironmentalLapseRateCPerKm,
					ITCZSeasonalAmplitudeDegrees = ITCZSeasonalAmplitudeDegrees,
					HyperAridUpperAridityIndex = HyperAridUpperAridityIndex,
					AridUpperAridityIndex = AridUpperAridityIndex,
					SemiAridUpperAridityIndex = SemiAridUpperAridityIndex,
					DrySubhumidUpperAridityIndex = DrySubhumidUpperAridityIndex,
					RainShadowMinimumTransmission = RainShadowMinimumTransmission,
					OrographicSampleSteps = OrographicSampleSteps
				}
			};

		GD.Print("--------------------------------");
		GD.Print("WORLD GENERATION STARTED");
		GD.Print($"Seed: {Seed}");

		GeneratedWorld = GenerateWorld(settings);
		GeneratedWorld.CalculateDerivedLayers();

		// Initialize the simulation layer from the exact same generated geography.
		// The runtime node is optional so the geography editor remains usable alone.
		SimulationRuntime? simulation = GetParent()?.GetParent()?.GetNodeOrNull<SimulationRuntime>("SimulationRuntime");
		if (simulation != null)
			simulation.Initialize(GeneratedWorld);

		bool diagnosticsPassed = WorldDiagnostics.Validate(GeneratedWorld, out string diagnosticsReport);
		GD.Print(diagnosticsReport);
		if (!diagnosticsPassed)
			GD.PushWarning("World generation completed with diagnostic errors. Review WORLD DIAGNOSTICS above.");

		Image image = RenderWorld(GeneratedWorld);

		target.Texture = ImageTexture.CreateFromImage(image);

		target.Centered = true;
		target.TextureFilter =
			CanvasItem.TextureFilterEnum.Nearest;

		GD.Print("WORLD GENERATION COMPLETE");
		GD.Print($"Resolution: {MapWidth} x {MapHeight}");
		GD.Print($"Land fraction: {TargetLandFraction:P0}");
		GD.Print("Integrated climate, hydrology, geology, soil, ecology, resources and gameplay geography generated.");
		GD.Print("--------------------------------");
	}

	// ==========================================
	// NOISE CREATION
	// ==========================================

	private FastNoiseLite CreateNoise(
		int seed,
		int octaves,
		FastNoiseLite.FractalTypeEnum fractalType)
	{
		FastNoiseLite noise = new FastNoiseLite();

		noise.Seed = seed;

		noise.NoiseType =
			FastNoiseLite.NoiseTypeEnum.SimplexSmooth;

		noise.FractalType = fractalType;

		noise.FractalOctaves = octaves;
		noise.FractalLacunarity = 2.0f;
		noise.FractalGain = 0.5f;

		noise.Frequency = 1.0f;

		return noise;
	}

	// ==========================================
	// GENERATE GLOBAL GEOGRAPHY
	// ==========================================

	private WorldData GenerateWorld(
		WorldGenerationSettings settings)
	{
		WorldData world = new WorldData(
			settings.Width,
			settings.Height,
			settings.Seed,
			settings.ClimateSettings
		);

		FastNoiseLite continentalNoise =
			CreateNoise(
				settings.Seed,
				4,
				FastNoiseLite.FractalTypeEnum.Fbm
			);

		FastNoiseLite regionalNoise =
			CreateNoise(
				settings.Seed + 1709,
				3,
				FastNoiseLite.FractalTypeEnum.Fbm
			);

		FastNoiseLite ridgeNoise =
			CreateNoise(
				settings.Seed + 7919,
				3,
				FastNoiseLite.FractalTypeEnum.Ridged
			);

		// SAMPLE THE SPHERE.
		// Using 3D coordinates prevents a longitude seam.

		for (int y = 0; y < world.Height; y++)
		{
			float latitude = Mathf.Pi *
				(0.5f - ((y + 0.5f) / world.Height));

			float cosLat = Mathf.Cos(latitude);

			for (int x = 0; x < world.Width; x++)
			{
				float longitude =
					(Mathf.Pi * 2.0f) *
					((x + 0.5f) / world.Width)
					- Mathf.Pi;

				float sx = cosLat * Mathf.Cos(longitude);
				float sy = Mathf.Sin(latitude);
				float sz = cosLat * Mathf.Sin(longitude);

				int index = world.GetIndex(x, y);

				float continentalness =
					continentalNoise.GetNoise3D(
						sx * 1.35f,
						sy * 1.35f,
						sz * 1.35f
					);

				world.Continentalness[index] =
					continentalness;
			}
		}

		// FIND A LAND THRESHOLD BASED ON
		// ACTUAL SPHERICAL SURFACE AREA.

		float threshold = FindLandThreshold(
			world,
			settings.TargetLandFraction
		);

		// GENERATE ELEVATION.

		for (int y = 0; y < world.Height; y++)
		{
			float latitude = Mathf.Pi *
				(0.5f - ((y + 0.5f) / world.Height));

			float cosLat = Mathf.Cos(latitude);

			for (int x = 0; x < world.Width; x++)
			{
				float longitude =
					(Mathf.Pi * 2.0f) *
					((x + 0.5f) / world.Width)
					- Mathf.Pi;

				float sx = cosLat * Mathf.Cos(longitude);
				float sy = Mathf.Sin(latitude);
				float sz = cosLat * Mathf.Sin(longitude);

				int index = world.GetIndex(x, y);

				float c = world.Continentalness[index];

				bool land = c >= threshold;

				world.IsLand[index] = land;

				float elevation;

				if (land)
				{
					float landRange =
						Mathf.Max(0.001f, 1.0f - threshold);

					float inland = Mathf.Clamp(
						(c - threshold) / landRange,
						0.0f,
						1.0f
					);

					float regional =
						regionalNoise.GetNoise3D(
							sx * 3.5f,
							sy * 3.5f,
							sz * 3.5f
						);

					float ridge =
						ridgeNoise.GetNoise3D(
							sx * 8.0f,
							sy * 8.0f,
							sz * 8.0f
						);

					float mountainZone = Mathf.Clamp(
						(regional + 0.15f) / 0.75f,
						0.0f,
						1.0f
					);

					float ridgeStrength = Mathf.Clamp(
						(ridge + 0.1f) / 0.9f,
						0.0f,
						1.0f
					);

					float highlandMask = Mathf.Clamp(
						(inland - 0.10f) / 0.65f,
						0.0f,
						1.0f
					);

					float baseElevation =
						30.0f +
						Mathf.Pow(inland, 1.6f) * 1400.0f;

					float mountains =
						mountainZone *
						ridgeStrength *
						highlandMask *
						4200.0f;

					elevation =
						baseElevation + mountains;
				}
				else
				{
					float oceanRange =
						Mathf.Max(0.001f, threshold + 1.0f);

					float oceanDepth = Mathf.Clamp(
						(threshold - c) / oceanRange,
						0.0f,
						1.0f
					);

					elevation = -(
						120.0f +
						Mathf.Pow(oceanDepth, 0.85f) * 5800.0f
					);
				}

				world.ElevationMeters[index] = elevation;
			}
		}

		return world;
	}

	// ==========================================
	// AREA-WEIGHTED LAND DISTRIBUTION
	// ==========================================

	private float FindLandThreshold(
		WorldData world,
		float targetLandFraction)
	{
		float low = -1.0f;
		float high = 1.0f;

		for (int iteration = 0; iteration < 28; iteration++)
		{
			float middle = (low + high) * 0.5f;

			double landArea = 0.0;
			double totalArea = 0.0;

			for (int y = 0; y < world.Height; y++)
			{
				float latitude = Mathf.Pi *
					(0.5f - ((y + 0.5f) / world.Height));

				double rowWeight = Math.Cos(latitude);

				totalArea += rowWeight * world.Width;

				for (int x = 0; x < world.Width; x++)
				{
					int index = world.GetIndex(x, y);

					if (world.Continentalness[index] >= middle)
						landArea += rowWeight;
				}
			}

			double actualFraction = landArea / totalArea;

			if (actualFraction > targetLandFraction)
				low = middle;
			else
				high = middle;
		}

		return (low + high) * 0.5f;
	}

	// ==========================================
	// RENDER ELEVATION DATA
	// ==========================================

	private Image RenderWorld(WorldData world)
	{
		Image image = Image.CreateEmpty(
			world.Width,
			world.Height,
			false,
			Image.Format.Rgba8
		);

		for (int y = 0; y < world.Height; y++)
		{
			for (int x = 0; x < world.Width; x++)
			{
				int index = world.GetIndex(x, y);

				float elevation =
					world.ElevationMeters[index];

				image.SetPixel(
					x,
					y,
					GetElevationColor(elevation)
				);
			}
		}

		return image;
	}

	// ==========================================
	// ELEVATION COLOR PALETTE
	// ==========================================

	private Color GetElevationColor(float elevation)
{
	if (elevation < 0.0f)
	{
		float depth = Mathf.Clamp(-elevation / 6300.0f, 0.0f, 1.0f);
		Color shallow = new Color(0.30f, 0.62f, 0.76f);
		Color deep = new Color(0.02f, 0.08f, 0.22f);
		return shallow.Lerp(deep, depth);
	}

	// Match MapView's elevation palette. Lowlands are sage green,
	// not gold/yellow, so they are not confused with future desert biomes.
	if (elevation < 250.0f)
		return new Color(0.57f, 0.70f, 0.49f);
	if (elevation < 900.0f)
		return new Color(0.50f, 0.70f, 0.39f);
	if (elevation < 1800.0f)
		return new Color(0.31f, 0.53f, 0.34f);
	if (elevation < 3200.0f)
		return new Color(0.49f, 0.39f, 0.31f);
	if (elevation < 5000.0f)
		return new Color(0.71f, 0.68f, 0.61f);

	return new Color(0.94f, 0.94f, 0.89f);
}
}

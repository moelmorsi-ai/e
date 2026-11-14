
using Godot;

public sealed class WorldGenerationSettings
{
	public int Seed { get; set; } = 123456;

	public int Width { get; set; } = 512;

	public int Height { get; set; } = 256;

	public float TargetLandFraction { get; set; } = 0.30f;

	public ClimateModelSettings ClimateSettings { get; set; } = new ClimateModelSettings();
}

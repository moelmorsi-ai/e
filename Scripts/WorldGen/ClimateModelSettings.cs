#nullable enable

using Godot;

/// <summary>
/// Per-world climate parameters. Values are strategic-model tuning controls,
/// not universal constants; defaults are reasonable starting references.
/// </summary>
public sealed class ClimateModelSettings
{
	public float EnvironmentalLapseRateCPerKm { get; set; } = 6.5f;
	public float ITCZSeasonalAmplitudeDegrees { get; set; } = 7.5f;
	public float HyperAridUpperAridityIndex { get; set; } = 0.05f;
	public float AridUpperAridityIndex { get; set; } = 0.20f;
	public float SemiAridUpperAridityIndex { get; set; } = 0.50f;
	public float DrySubhumidUpperAridityIndex { get; set; } = 0.65f;
	public float RainShadowMinimumTransmission { get; set; } = 0.34f;
	public int OrographicSampleSteps { get; set; } = 6;

	public ClimateModelSettings Clone()
	{
		return new ClimateModelSettings
		{
			EnvironmentalLapseRateCPerKm = EnvironmentalLapseRateCPerKm,
			ITCZSeasonalAmplitudeDegrees = ITCZSeasonalAmplitudeDegrees,
			HyperAridUpperAridityIndex = HyperAridUpperAridityIndex,
			AridUpperAridityIndex = AridUpperAridityIndex,
			SemiAridUpperAridityIndex = SemiAridUpperAridityIndex,
			DrySubhumidUpperAridityIndex = DrySubhumidUpperAridityIndex,
			RainShadowMinimumTransmission = RainShadowMinimumTransmission,
			OrographicSampleSteps = OrographicSampleSteps
		};
	}

	public void Normalize()
	{
		EnvironmentalLapseRateCPerKm = Mathf.Clamp(EnvironmentalLapseRateCPerKm, 3.0f, 9.0f);
		ITCZSeasonalAmplitudeDegrees = Mathf.Clamp(ITCZSeasonalAmplitudeDegrees, 0.0f, 20.0f);
		HyperAridUpperAridityIndex = Mathf.Clamp(HyperAridUpperAridityIndex, 0.01f, 0.20f);
		AridUpperAridityIndex = Mathf.Clamp(AridUpperAridityIndex, HyperAridUpperAridityIndex + 0.01f, 0.60f);
		SemiAridUpperAridityIndex = Mathf.Clamp(SemiAridUpperAridityIndex, AridUpperAridityIndex + 0.01f, 1.0f);
		DrySubhumidUpperAridityIndex = Mathf.Clamp(DrySubhumidUpperAridityIndex, SemiAridUpperAridityIndex + 0.01f, 1.5f);
		RainShadowMinimumTransmission = Mathf.Clamp(RainShadowMinimumTransmission, 0.10f, 0.80f);
		OrographicSampleSteps = Mathf.Clamp(OrographicSampleSteps, 1, 12);
	}
}

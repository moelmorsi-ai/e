#nullable enable

using Godot;
using System;

public partial class MapView : Node2D
{
	private enum MapMode
	{
		Elevation,
		Terrain,
		Slope,
		Aspect,
		LocalRelief,
		CoastalSurface,
		ArmyMovement,
		ConstructionSuitability,
		RiversHydrology,
		RegionalFloodSusceptibility,
		MeanTemperature,
		SeasonalTemperatureRange,
		MonthlyTemperature,
		AnnualPrecipitation,
		SeasonalPrecipitation,
		MonthlyPrecipitation,
		Aridity,
		ClimateZones,
		GrowingSeason,
		Biome,
		VegetationDensity,
		ForestCoverage,
		GrasslandCoverage,
		Wetlands,
		SoilTypes,
		SoilFertility,
		SoilInfiltration,
		GeologicalTypes,
		GeologicalStability,
		SeismicRisk,
		MineralResources,
		AnnualRunoff,
		PrevailingWinds,
		MonthlyWindDirection,
		MonthlyWindSpeed,
		RiverDischarge,
		RiverPermanence,
		GroundwaterPotential,
		WaterAvailability,
		AgriculturalSuitability,
		SettlementSuitability,
		InfantryMovement,
		CavalryMovement,
		VehicleMovement
	}

	private Sprite2D? _worldImage;
	private Label? _locationLabel;
	private Label? _legendLabel;
	private HBoxContainer? _legendSwatches;	
	private Label? _legendRangeLabel;
	private OptionButton? _mapModeSelector;
	private OptionButton? _monthSelector;
	private int _selectedMonth;
	private int _selectedDataIndex = -1;
	private Node2D? _selectionOverlay;

	private ProceduralWorldGenerator? _worldGenerator;
	private WorldData? _worldData;

	private bool _leftButtonDown;
	private bool _leftDragStarted;
	private bool _auxiliaryDragging;
	private Vector2 _pressPosition;

	private MapMode _currentMapMode = MapMode.Terrain;

	private const float ZoomStep = 1.15f;
	private const float MinZoom = 0.20f;
	private const float MaxZoom = 5.0f;
	private const float DragThreshold = 6.0f;
	private const int CellPixelSize = 4;

	private static readonly string[] AspectDirections =
	{
		"N", "NE", "E", "SE", "S", "SW", "W", "NW"
	};

	private static readonly Color[] AspectColors =
	{
		new Color(0.20f, 0.48f, 0.88f), // N
		new Color(0.15f, 0.70f, 0.78f), // NE
		new Color(0.22f, 0.68f, 0.35f), // E
		new Color(0.66f, 0.78f, 0.22f), // SE
		new Color(0.94f, 0.78f, 0.20f), // S
		new Color(0.94f, 0.48f, 0.16f), // SW
		new Color(0.82f, 0.22f, 0.20f), // W
		new Color(0.58f, 0.30f, 0.78f)  // NW
	};

	// ==========================================
	// 1. INITIALIZATION
	// ==========================================

	public override void _Ready()
	{
		CreateLocationInspector();

		_worldImage = GetNodeOrNull<Sprite2D>("MapRenderer");

		if (_worldImage == null)
		{
			GD.PushError("MapRenderer Sprite2D was not found under MapView.");
			SetInspectorText(
				"GEOGRAPHICAL INSPECTOR\n\n" +
				"MapRenderer was not found.\n" +
				"Check the WorldMap scene hierarchy."
			);
			return;
		}

		if (_worldImage.Texture == null)
		{
			GD.PushError("MapRenderer has no texture. Check WorldGenerator.");
			SetInspectorText(
				"GEOGRAPHICAL INSPECTOR\n\n" +
				"The map texture is missing.\n" +
				"Check WorldGenerator and Generate On Ready."
			);
			return;
		}

		// WorldGenerator is a sibling of MapRenderer under MapView.
		// Its _Ready() runs before this MapView _Ready().
		_worldGenerator = GetNodeOrNull<ProceduralWorldGenerator>("WorldGenerator");
		_worldData = _worldGenerator?.GeneratedWorld;

		if (_worldGenerator == null)
		{
			GD.PushError("WorldGenerator node was not found under MapView.");
		}
		else if (_worldData == null)
		{
			GD.PushError(
				"WorldGenerator exists, but GeneratedWorld is null. " +
				"Make sure Generate On Ready is enabled."
			);
		}
		else
		{
			GD.Print($"WORLD DATA CONNECTED: {_worldData.Width} x {_worldData.Height}");

			// Calculate derived layers once after the generator has populated
			// elevation and land data. Every map mode uses these same arrays.
			if (!_worldData.DerivedLayersCalculated)
				_worldData.CalculateDerivedLayers();
		}

		Position = Vector2.Zero;
		Scale = Vector2.One;

		Vector2 textureSize = _worldImage.Texture.GetSize();
		Vector2 viewportSize = GetViewportRect().Size;

		if (textureSize.X <= 0.0f || textureSize.Y <= 0.0f ||
			viewportSize.X <= 0.0f || viewportSize.Y <= 0.0f)
		{
			GD.PushError("Map or viewport has an invalid size.");
			SetInspectorText("GEOGRAPHICAL INSPECTOR\n\nInvalid map or viewport dimensions.");
			return;
		}

		float fitScale = Mathf.Min(
			viewportSize.X / textureSize.X,
			viewportSize.Y / textureSize.Y
		) * 0.90f;

		_worldImage.Centered = true;
		_worldImage.Position = viewportSize * 0.5f;
		_worldImage.Scale = Vector2.One * fitScale;
		_worldImage.Visible = true;
		_worldImage.Modulate = Colors.White;
		_worldImage.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;

		CreateSelectionOverlay();
		UpdateLegend();

		if (_worldData == null)
		{
			SetInspectorText(
				"World map loaded, but geographical data is unavailable.\n\n" +
				"Check the WorldGenerator node and its Generate On Ready setting."
			);
		}
		else
		{
			SetInspectorText(
				"World data connected.\n\n" +
				"Choose a map mode, then click a location to inspect its geography."
			);
			RenderCurrentMapMode();
		}

		GD.Print("--------------------------------");
		GD.Print("WORLD MAP INITIALIZED");
		GD.Print($"Map size: {textureSize}");
		GD.Print($"Display scale: {fitScale:F3}");
		GD.Print("GEOGRAPHICAL MAP MODES INITIALIZED");
		GD.Print("LOCATION INSPECTOR CREATED");
		GD.Print("Selection system initialized.");
		GD.Print("--------------------------------");
	}

	// ==========================================
	// 2. MAP MODE USER INTERFACE
	// ==========================================

	private void CreateLocationInspector()
	{
		CanvasLayer canvasLayer = new CanvasLayer
		{
			Name = "LocationInspectorLayer",
			Layer = 100,
			Visible = true
		};

		Vector2 viewportSize = GetViewportRect().Size;
		float panelWidth = Mathf.Min(460.0f, Mathf.Max(280.0f, viewportSize.X - 32.0f));
		float panelHeight = Mathf.Min(620.0f, Mathf.Max(360.0f, viewportSize.Y - 32.0f));

		Panel panel = new Panel
		{
			Name = "GeographyPanel",
			Position = new Vector2(16.0f, 16.0f),
			Size = new Vector2(panelWidth, panelHeight),
			CustomMinimumSize = new Vector2(panelWidth, panelHeight),
			Visible = true,
			MouseFilter = Control.MouseFilterEnum.Stop,
			ZIndex = 100
		};

		StyleBoxFlat panelStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.055f, 0.07f, 0.09f, 0.97f),
			BorderColor = new Color(0.90f, 0.73f, 0.20f, 1.0f)
		};

		panelStyle.SetBorderWidthAll(2);
		panelStyle.SetCornerRadiusAll(8);
		panelStyle.ContentMarginLeft = 12;
		panelStyle.ContentMarginRight = 12;
		panelStyle.ContentMarginTop = 12;
		panelStyle.ContentMarginBottom = 12;
		panel.AddThemeStyleboxOverride("panel", panelStyle);

		// The panel content is sized to the panel. The details section is a
		// ScrollContainer so long inspection text cannot spill outside it.
		VBoxContainer content = new VBoxContainer
		{
			Name = "GeographyPanelContent",
			Position = new Vector2(14.0f, 12.0f),
			Size = new Vector2(panelWidth - 28.0f, panelHeight - 24.0f),
			MouseFilter = Control.MouseFilterEnum.Stop
		};
		content.AddThemeConstantOverride("separation", 7);

		Label heading = new Label
		{
			Text = "GEOGRAPHICAL INSPECTOR",
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		heading.AddThemeFontSizeOverride("font_size", 16);
		heading.AddThemeColorOverride("font_color", new Color(0.98f, 0.98f, 1.0f));
		content.AddChild(heading);

		Label modeHeading = new Label
		{
			Text = "MAP MODE",
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		modeHeading.AddThemeFontSizeOverride("font_size", 12);
		modeHeading.AddThemeColorOverride("font_color", new Color(0.75f, 0.79f, 0.85f));
		content.AddChild(modeHeading);

		_mapModeSelector = new OptionButton
		{
			Name = "MapModeSelector",
			CustomMinimumSize = new Vector2(0.0f, 34.0f),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};

		_mapModeSelector.AddItem("Elevation", (int)MapMode.Elevation);
		_mapModeSelector.AddItem("Terrain", (int)MapMode.Terrain);
		_mapModeSelector.Select(1);
		_mapModeSelector.AddItem("Slope", (int)MapMode.Slope);
		_mapModeSelector.AddItem("Aspect", (int)MapMode.Aspect);
		_mapModeSelector.AddItem("Local Relief", (int)MapMode.LocalRelief);
		_mapModeSelector.AddItem("Coastal & Surface", (int)MapMode.CoastalSurface);
		_mapModeSelector.AddItem("Rivers & Hydrology", (int)MapMode.RiversHydrology);
		_mapModeSelector.AddItem("River Discharge", (int)MapMode.RiverDischarge);
		_mapModeSelector.AddItem("River Permanence", (int)MapMode.RiverPermanence);
		_mapModeSelector.AddItem("Regional Flood Susceptibility", (int)MapMode.RegionalFloodSusceptibility);
		_mapModeSelector.AddItem("Groundwater Potential", (int)MapMode.GroundwaterPotential);
		_mapModeSelector.AddItem("Water Availability", (int)MapMode.WaterAvailability);
		_mapModeSelector.AddItem("Mean Temperature", (int)MapMode.MeanTemperature);
		_mapModeSelector.AddItem("Seasonal Temperature Range", (int)MapMode.SeasonalTemperatureRange);
		_mapModeSelector.AddItem("Monthly Temperature", (int)MapMode.MonthlyTemperature);
		_mapModeSelector.AddItem("Annual Precipitation", (int)MapMode.AnnualPrecipitation);
		_mapModeSelector.AddItem("Seasonal Precipitation", (int)MapMode.SeasonalPrecipitation);
		_mapModeSelector.AddItem("Monthly Precipitation", (int)MapMode.MonthlyPrecipitation);
		_mapModeSelector.AddItem("Aridity", (int)MapMode.Aridity);
		_mapModeSelector.AddItem("Climate Zones", (int)MapMode.ClimateZones);
		_mapModeSelector.AddItem("Growing Season", (int)MapMode.GrowingSeason);
		_mapModeSelector.AddItem("Prevailing Winds", (int)MapMode.PrevailingWinds);
		_mapModeSelector.AddItem("Monthly Wind Direction", (int)MapMode.MonthlyWindDirection);
		_mapModeSelector.AddItem("Monthly Wind Speed", (int)MapMode.MonthlyWindSpeed);
		_mapModeSelector.AddItem("Annual Runoff", (int)MapMode.AnnualRunoff);
		_mapModeSelector.AddItem("Biomes", (int)MapMode.Biome);
		_mapModeSelector.AddItem("Vegetation Density", (int)MapMode.VegetationDensity);
		_mapModeSelector.AddItem("Forest Coverage", (int)MapMode.ForestCoverage);
		_mapModeSelector.AddItem("Grassland Coverage", (int)MapMode.GrasslandCoverage);
		_mapModeSelector.AddItem("Wetlands", (int)MapMode.Wetlands);
		_mapModeSelector.AddItem("Soil Types", (int)MapMode.SoilTypes);
		_mapModeSelector.AddItem("Soil Fertility", (int)MapMode.SoilFertility);
		_mapModeSelector.AddItem("Soil Infiltration", (int)MapMode.SoilInfiltration);
		_mapModeSelector.AddItem("Geological Types", (int)MapMode.GeologicalTypes);
		_mapModeSelector.AddItem("Geological Stability", (int)MapMode.GeologicalStability);
		_mapModeSelector.AddItem("Seismic Risk", (int)MapMode.SeismicRisk);
		_mapModeSelector.AddItem("Mineral Resources", (int)MapMode.MineralResources);
		_mapModeSelector.AddItem("Agricultural Suitability", (int)MapMode.AgriculturalSuitability);
		_mapModeSelector.AddItem("Settlement Suitability", (int)MapMode.SettlementSuitability);
		_mapModeSelector.AddItem("Construction Suitability", (int)MapMode.ConstructionSuitability);
		_mapModeSelector.AddItem("Army Movement", (int)MapMode.ArmyMovement);
		_mapModeSelector.AddItem("Infantry Movement", (int)MapMode.InfantryMovement);
		_mapModeSelector.AddItem("Cavalry Movement", (int)MapMode.CavalryMovement);
		_mapModeSelector.AddItem("Vehicle Movement", (int)MapMode.VehicleMovement);
		_mapModeSelector.Select(0);
		_mapModeSelector.ItemSelected += OnMapModeSelected;
		content.AddChild(_mapModeSelector);

		_monthSelector = new OptionButton
		{
			Name = "ClimateMonthSelector",
			CustomMinimumSize = new Vector2(0.0f, 30.0f),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			Visible = false
		};
		string[] monthNames = { "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" };
		for (int month = 0; month < monthNames.Length; month++)
			_monthSelector.AddItem(monthNames[month], month);
		_monthSelector.Select(_selectedMonth);
		_monthSelector.ItemSelected += OnMonthSelected;
		content.AddChild(_monthSelector);

		_legendLabel = new Label
		{
			Name = "MapModeLegend",
			CustomMinimumSize = new Vector2(0.0f, 68.0f),
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		_legendLabel.AddThemeFontSizeOverride("font_size", 12);
		_legendLabel.AddThemeColorOverride("font_color", new Color(0.78f, 0.83f, 0.89f));
		content.AddChild(_legendLabel);

		_legendSwatches = new HBoxContainer
		{
			Name = "MapLegendSwatches",
			CustomMinimumSize = new Vector2(0.0f, 11.0f),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		_legendSwatches.AddThemeConstantOverride("separation", 2);
		for (int swatch = 0; swatch < 5; swatch++)
		{
			_legendSwatches.AddChild(new ColorRect
			{
				Name = $"LegendSwatch{swatch}",
				Color = new Color(0.15f + swatch * 0.15f, 0.35f, 0.45f),
				CustomMinimumSize = new Vector2(0.0f, 9.0f),
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
				MouseFilter = Control.MouseFilterEnum.Ignore
			});
		}
		content.AddChild(_legendSwatches);

		_legendRangeLabel = new Label
		{
			Name = "MapLegendRange",
			Text = "LOW  →  HIGH",
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		_legendRangeLabel.AddThemeFontSizeOverride("font_size", 10);
		_legendRangeLabel.AddThemeColorOverride("font_color", new Color(0.70f, 0.75f, 0.82f));
		content.AddChild(_legendRangeLabel);

		content.AddChild(new HSeparator());

		ScrollContainer detailsScroll = new ScrollContainer
		{
			Name = "LocationDetailsScroll",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0.0f, 100.0f)
		};

		_locationLabel = new Label
		{
			Name = "LocationLabel",
			Text = "No location selected.\nClick anywhere on the map.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		_locationLabel.AddThemeFontSizeOverride("font_size", 14);
		_locationLabel.AddThemeColorOverride("font_color", new Color(0.96f, 0.97f, 1.0f));
		detailsScroll.AddChild(_locationLabel);
		content.AddChild(detailsScroll);

		panel.AddChild(content);
		canvasLayer.AddChild(panel);
		AddChild(canvasLayer);

		panel.Show();
		canvasLayer.Show();
		GD.Print("GEOGRAPHICAL INSPECTOR AND MAP MODE SELECTOR CREATED");
	}

	private void OnMapModeSelected(long selectedIndex)
	{
		if (_mapModeSelector == null || selectedIndex < 0 || selectedIndex >= _mapModeSelector.GetItemCount())
			return;

		int modeId = _mapModeSelector.GetItemId((int)selectedIndex);
		if (!Enum.IsDefined(typeof(MapMode), modeId))
			return;
		_currentMapMode = (MapMode)modeId;
		UpdateMonthSelectorVisibility();
		RenderCurrentMapMode();
		UpdateLegend();
		GD.Print($"MAP MODE CHANGED: {_currentMapMode}");
	}

	private void OnMonthSelected(long selectedIndex)
	{
		if (selectedIndex < 0 || selectedIndex >= WorldData.MonthsPerYear)
			return;
		_selectedMonth = (int)selectedIndex;
		RenderCurrentMapMode();
		UpdateLegend();
		RefreshSelectedMonthInspector();
	}

	/// <summary>
	/// Refreshes the selected location's month-specific inspector line without
	/// rebuilding the full location report or requiring the user to click again.
	/// </summary>
	private void RefreshSelectedMonthInspector()
	{
		if (_worldData == null || _locationLabel == null ||
			_selectedDataIndex < 0 || _selectedDataIndex >= _worldData.Width * _worldData.Height)
			return;

		const string linePrefix = "Selected month: ";
		string currentText = _locationLabel.Text;
		int lineStart = currentText.IndexOf(linePrefix, StringComparison.Ordinal);
		if (lineStart < 0)
			return;

		int lineEnd = currentText.IndexOf('\n', lineStart);
		if (lineEnd < 0)
			lineEnd = currentText.Length;

		int monthlyIndex = _worldData.GetMonthlyIndex(_selectedDataIndex, _selectedMonth);
		string[] monthNames =
		{
			"January", "February", "March", "April", "May", "June",
			"July", "August", "September", "October", "November", "December"
		};

		string selectedMonthText = _worldData.IsLand[_selectedDataIndex]
			? $"{monthNames[_selectedMonth]}: {_worldData.MonthlyTemperatureC[monthlyIndex]:F1} °C, " +
				$"{_worldData.MonthlyPrecipitationMm[monthlyIndex]:F0} mm precipitation, " +
				$"{_worldData.MonthlyWindDirectionDegrees[monthlyIndex]:F0}° wind at " +
				$"{_worldData.MonthlyWindSpeedMps[monthlyIndex]:F1} m/s"
			: $"{monthNames[_selectedMonth]}: {_worldData.MonthlyTemperatureC[monthlyIndex]:F1} °C, " +
				$"{_worldData.MonthlyPrecipitationMm[monthlyIndex]:F0} mm precipitation";

		string replacement = linePrefix + selectedMonthText;
		_locationLabel.Text = currentText.Substring(0, lineStart) +
			replacement + currentText.Substring(lineEnd);
	}

	private void UpdateMonthSelectorVisibility()
	{
		if (_monthSelector == null)
			return;
		_monthSelector.Visible = _currentMapMode == MapMode.MonthlyTemperature ||
			_currentMapMode == MapMode.MonthlyPrecipitation ||
			_currentMapMode == MapMode.MonthlyWindDirection ||
			_currentMapMode == MapMode.MonthlyWindSpeed;
	}

	private void UpdateLegend()
	{
		if (_legendLabel == null)
			return;

		string monthName = new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" }[_selectedMonth];
		_legendLabel.Text = _currentMapMode switch
		{
			MapMode.Elevation => "Hypsometric map. Ocean colors show increasing depth; land colors progress from green lowlands and plains to brown highlands and pale mountain peaks.",
			MapMode.Terrain => "Landforms: muted green lowlands and coastal plains, brighter green plains, darker green hills, then earth-brown to pale-grey high terrain. Rivers overlay land cells; lakes are water.",
			MapMode.Slope => "Actual slope angle: green <0.05°; pale green 0.05–0.1°; yellow-green 0.1–0.5°; yellow 0.5–2°; orange 2–5°; red 5–15°; dark red 15–30°; maroon above 30°.",
			MapMode.Aspect => "Direction of steepest downhill slope: N, NE, E, SE, S, SW, W and NW. Grey means the surface is too flat for a reliable direction.",
			MapMode.LocalRelief => $"Elevation range in a {WorldData.ReliefWindowSize}×{WorldData.ReliefWindowSize} cell window. Higher values indicate more rugged local terrain.",
			MapMode.CoastalSurface => "Muted teal-green: coastal land. Green: inland land. Turquoise: coastal water and rivers. Light blue: inland lakes. Dark blue: offshore water.",
			MapMode.ArmyMovement or MapMode.InfantryMovement => "Infantry-like off-road movement cost per destination cell. Lower values are easier; forests, wetlands, deserts, cold and steep terrain add penalties. Water is impassable.",
			MapMode.CavalryMovement => "Cavalry movement cost. Dense forest, wetlands, deserts, cold and steep terrain impose stronger penalties than open plains.",
			MapMode.VehicleMovement => "Vehicle movement cost. Wetlands, forests, deserts, snow and steep terrain are major obstacles. Roads and bridges are not yet represented.",
			MapMode.ConstructionSuitability => "Integrated construction suitability, 0–100. Combines terrain, slope, flood susceptibility, soil stability, geological stability, seismic risk and wetland penalties. Not a parcel-level engineering assessment.",
			MapMode.RiversHydrology => _worldData == null ? "Mapped river channels, inland lakes and land drainage." : $"Blue = ocean; cyan = mapped river channels (brighter channels have larger catchments); light blue = inland lakes; green = land. River threshold: {_worldData.RiverStartThresholdKm2:N0} km² upstream catchment.",
			MapMode.RiverDischarge => "Mean annual discharge along mapped river channels, in m³/s. This is a long-term monthly-mean-derived estimate, not a flood peak.",
			MapMode.RiverPermanence => "River regime derived from the number of months with sufficient simulated discharge: perennial, seasonal or intermittent.",
			MapMode.RegionalFloodSusceptibility => "Regional river-corridor flood susceptibility proxy. It includes river proximity, relative elevation, catchment size, slope, relief and seasonal discharge. It is not a hydraulic inundation model.",
			MapMode.GroundwaterPotential => "Relative groundwater potential from recharge, soil infiltration and simplified rock permeability. It is not a mapped aquifer or well-yield prediction.",
			MapMode.WaterAvailability => "Annual climatic water availability after actual evapotranspiration, with groundwater recharge added. River and lake access are shown separately in the inspector.",
			MapMode.MeanTemperature => "Mean annual temperature in °C. The model accounts for latitude, elevation and continentality-driven seasonal moderation.",
			MapMode.SeasonalTemperatureRange => "Difference between the warmest and coldest simulated monthly mean temperatures, in °C. Larger ranges generally occur inland and at higher latitudes.",
			MapMode.MonthlyTemperature => $"Simulated mean temperature for {monthName}, in °C. Select a different month to inspect the seasonal cycle.",
			MapMode.AnnualPrecipitation => "Total annual precipitation in mm. Values emerge from circulation belts, ocean moisture transport, orographic uplift and rain-shadow reduction.",
			MapMode.SeasonalPrecipitation => "Wettest-month to driest-month precipitation ratio. Higher values indicate stronger rainfall seasonality; this is not an annual rainfall total.",
			MapMode.MonthlyPrecipitation => $"Simulated precipitation for {monthName}, in mm. Select a different month to inspect seasonal rainfall and monsoon shifts.",
			MapMode.Aridity => "Annual precipitation divided by potential evapotranspiration (P/PET). Hyper-arid <0.05; arid <0.20; semi-arid <0.50; dry subhumid <0.65; humid ≥0.65.",
			MapMode.ClimateZones => "Broad climate families classified from monthly temperature, annual aridity and rainfall seasonality. Köppen-inspired, not an exact Köppen-Geiger dataset.",
			MapMode.GrowingSeason => "Number of months with mean temperature at or above 5°C. This is a thermal growing-season proxy; crop-specific calendars are not included.",
			MapMode.PrevailingWinds => "Mean prevailing wind direction by global circulation belt: trades, westerlies and polar easterlies. Monthly direction changes with the seasonal shift of circulation belts.",
			MapMode.MonthlyWindDirection => $"Prevailing surface-wind direction for {monthName}. Colors use the same eight compass sectors as the aspect map.",
			MapMode.MonthlyWindSpeed => $"Simplified prevailing surface-wind speed for {monthName}, in m/s. This is a circulation-strength proxy, not local weather.",
			MapMode.AnnualRunoff => "Annual surface runoff in mm after the monthly soil-water balance, infiltration, evapotranspiration and snowmelt calculations.",
			MapMode.Biome => "Potential biome derived from climate, water balance, elevation and wetland conditions. Broad strategic-scale ecology, not detailed species distribution.",
			MapMode.VegetationDensity => "Relative potential vegetation cover, 0–100, based on biome, thermal suitability and moisture availability.",
			MapMode.ForestCoverage => "Potential forest density, 0–100. Tropical, temperate and boreal forest regions are distinguished from shrubland and grassland.",
			MapMode.GrasslandCoverage => "Potential grassland coverage, 0–100, including savanna, steppe and temperate grassland regions.",
			MapMode.Wetlands => "Potential wetland coverage linked to runoff, flood susceptibility, precipitation, drainage and low-gradient terrain.",
			MapMode.SoilTypes => "Procedural soil classes inferred from climate, terrain, parent material and drainage. They are not real-world soil survey units.",
			MapMode.SoilFertility => "Relative soil fertility, 0–100, informed by soil class, moisture, temperature, parent material and slope.",
			MapMode.SoilInfiltration => "Estimated saturated infiltration rate by soil class, in mm/hour. Sandy and rocky soils generally infiltrate faster than clay and saline soils.",
			MapMode.GeologicalTypes => "Procedural parent-rock classes and broad tectonic provinces. Generated geological patterns are game data, not actual geology.",
			MapMode.GeologicalStability => "Relative geological stability, 0–100. Lower values indicate greater procedural tectonic and terrain-instability proxies.",
			MapMode.SeismicRisk => "Relative seismic-risk proxy, 0–100, derived from procedural tectonic activity, ruggedness and geological stability. Not a real seismic hazard map.",
			MapMode.MineralResources => "Highest local abundance among iron, copper, tin, gold, coal, oil and natural gas deposits. Timber, freshwater and fertile land are inspected separately as resources.",
			MapMode.AgriculturalSuitability => "Agricultural suitability, 0–100, combining soil fertility and depth, water availability, temperature, growing season, slope and elevation.",
			MapMode.SettlementSuitability => "Settlement suitability, 0–100, combining freshwater access, construction conditions, agriculture, climate comfort and hazard exposure.",
			_ => "Geographical map mode."
		};
		UpdateLegendSwatches();
	}

	private void UpdateLegendSwatches()
	{
		if (_legendSwatches == null || _legendRangeLabel == null)
			return;

		Color[] palette = _currentMapMode switch
		{
			MapMode.Slope => new[] { new Color(0.16f, 0.50f, 0.30f), new Color(0.66f, 0.78f, 0.22f), new Color(0.94f, 0.78f, 0.20f), new Color(0.94f, 0.48f, 0.16f), new Color(0.58f, 0.05f, 0.12f) },
			MapMode.Aspect or MapMode.PrevailingWinds or MapMode.MonthlyWindDirection => new[] { AspectColors[0], AspectColors[2], AspectColors[4], AspectColors[6], AspectColors[7] },
			MapMode.ClimateZones => new[] { new Color(0.10f, 0.48f, 0.26f), new Color(0.84f, 0.55f, 0.28f), new Color(0.38f, 0.65f, 0.37f), new Color(0.34f, 0.67f, 0.78f), new Color(0.88f, 0.93f, 0.96f) },
			MapMode.SoilTypes => new[] { new Color(0.82f, 0.69f, 0.43f), new Color(0.62f, 0.31f, 0.23f), new Color(0.45f, 0.58f, 0.34f), new Color(0.24f, 0.39f, 0.29f), new Color(0.57f, 0.52f, 0.49f) },
			MapMode.GeologicalTypes => new[] { new Color(0.73f, 0.60f, 0.39f), new Color(0.62f, 0.34f, 0.29f), new Color(0.52f, 0.43f, 0.66f), new Color(0.75f, 0.20f, 0.16f), new Color(0.39f, 0.48f, 0.43f) },
			MapMode.Biome => new[] { new Color(0.05f, 0.34f, 0.19f), new Color(0.72f, 0.69f, 0.30f), new Color(0.96f, 0.78f, 0.20f), new Color(0.20f, 0.48f, 0.30f), new Color(0.10f, 0.62f, 0.56f) },
			MapMode.RiversHydrology or MapMode.RiverDischarge or MapMode.RiverPermanence => new[] { new Color(0.20f, 0.32f, 0.27f), new Color(0.08f, 0.40f, 0.72f), new Color(0.10f, 0.60f, 0.90f), new Color(0.22f, 0.78f, 0.98f), new Color(0.55f, 0.95f, 1.0f) },
			_ => new[] { new Color(0.13f, 0.30f, 0.39f), new Color(0.20f, 0.52f, 0.45f), new Color(0.29f, 0.65f, 0.43f), new Color(0.68f, 0.75f, 0.32f), new Color(0.94f, 0.83f, 0.30f) }
		};

		for (int i = 0; i < _legendSwatches.GetChildCount() && i < palette.Length; i++)
		{
			if (_legendSwatches.GetChild(i) is ColorRect swatch)
				swatch.Color = palette[i];
		}

		_legendRangeLabel.Text = _currentMapMode switch
		{
			MapMode.Aspect or MapMode.PrevailingWinds or MapMode.MonthlyWindDirection => "N  •  E  •  S  •  W  •  NW",
			MapMode.ClimateZones or MapMode.SoilTypes or MapMode.GeologicalTypes or MapMode.Biome => "CATEGORIES  —  SEE DESCRIPTION",
			MapMode.RiversHydrology or MapMode.RiverDischarge or MapMode.RiverPermanence => "LAND / SMALL CHANNEL  →  MAJOR / PERENNIAL CHANNEL",
			MapMode.MeanTemperature or MapMode.MonthlyTemperature => "COLDER  →  HOTTER",
			MapMode.AnnualPrecipitation or MapMode.MonthlyPrecipitation or MapMode.AnnualRunoff => "DRIER  →  WETTER",
			MapMode.Aridity => "HYPER-ARID  →  HUMID",
			MapMode.ConstructionSuitability or MapMode.AgriculturalSuitability or MapMode.SettlementSuitability => "LOW SUITABILITY  →  HIGH SUITABILITY",
			MapMode.SeismicRisk => "LOW RISK  →  HIGH RISK",
			_ => "LOW  →  HIGH"
		};
	}

	private void SetInspectorText(string message)
	{
		if (_locationLabel != null)
			_locationLabel.Text = message;
	}

	// ==========================================
	// 3. MAP MODE RENDERING
	// ==========================================

	private void RenderCurrentMapMode()
	{
		if (_worldImage == null || _worldData == null)
			return;

		Image image = Image.CreateEmpty(
			_worldData.Width,
			_worldData.Height,
			false,
			Image.Format.Rgba8
		);

		for (int y = 0; y < _worldData.Height; y++)
		{
			for (int x = 0; x < _worldData.Width; x++)
			{
				int index = _worldData.GetIndex(x, y);
				image.SetPixel(x, y, GetMapModeColor(index));
			}
		}

		_worldImage.Texture = ImageTexture.CreateFromImage(image);
		_worldImage.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
	}

	private Color GetMapModeColor(int index)
	{
		if (_worldData == null)
			return Colors.Magenta;

		WorldData world = _worldData;
		int monthlyIndex = world.GetMonthlyIndex(index, _selectedMonth);
		return _currentMapMode switch
		{
			MapMode.Elevation => world.IsLake[index] ? GetHydrologyColor(index) : GetElevationColor(world.ElevationMeters[index]),
			MapMode.Terrain => GetTerrainWithHydrologyColor(index),
			MapMode.Slope => GetSlopeColor(index),
			MapMode.Aspect => GetAspectColor(index),
			MapMode.LocalRelief => GetReliefColor(index),
			MapMode.CoastalSurface => GetCoastalSurfaceColor(index),
			MapMode.ArmyMovement or MapMode.InfantryMovement => GetMovementCostColor(world.InfantryMovementCostMultiplier[index], index),
			MapMode.CavalryMovement => GetMovementCostColor(world.CavalryMovementCostMultiplier[index], index),
			MapMode.VehicleMovement => GetMovementCostColor(world.VehicleMovementCostMultiplier[index], index),
			MapMode.ConstructionSuitability => GetSuitabilityColor(world.ConstructionSuitability[index], index),
			MapMode.AgriculturalSuitability => GetSuitabilityColor(world.AgriculturalSuitability[index], index),
			MapMode.SettlementSuitability => GetSuitabilityColor(world.SettlementSuitability[index], index),
			MapMode.RiversHydrology => GetHydrologyColor(index),
			MapMode.RiverDischarge => GetRiverDischargeColor(index),
			MapMode.RiverPermanence => GetRiverPermanenceColor(index),
			MapMode.RegionalFloodSusceptibility => GetRegionalFloodSusceptibilityColor(index),
			MapMode.GroundwaterPotential => GetScalarLandColor(world.GroundwaterPotential[index], index, 0.0f, 100.0f),
			MapMode.WaterAvailability => GetScalarLandColor(world.WaterAvailabilityMm[index], index, 0.0f, 1600.0f),
			MapMode.MeanTemperature => GetTemperatureColor(world.MeanAnnualTemperatureC[index]),
			MapMode.SeasonalTemperatureRange => GetScalarLandColor(world.TemperatureRangeC[index], index, 0.0f, 45.0f),
			MapMode.MonthlyTemperature => GetTemperatureColor(world.MonthlyTemperatureC[monthlyIndex]),
			MapMode.AnnualPrecipitation => GetPrecipitationColor(index),
			MapMode.SeasonalPrecipitation => GetScalarLandColor(world.PrecipitationSeasonalityIndex[index], index, 1.0f, 16.0f),
			MapMode.MonthlyPrecipitation => GetGlobalScalarColor(world.MonthlyPrecipitationMm[monthlyIndex], 0.0f, 260.0f),
			MapMode.Aridity => GetAridityColor(index),
			MapMode.ClimateZones => GetClimateZoneColor(index),
			MapMode.GrowingSeason => GetScalarLandColor(world.GrowingSeasonMonths[index], index, 0.0f, 12.0f),
			MapMode.Biome => GetBiomeColor(index),
			MapMode.VegetationDensity => GetScalarLandColor(world.VegetationDensity[index], index, 0.0f, 100.0f),
			MapMode.ForestCoverage => GetScalarLandColor(world.ForestDensity[index], index, 0.0f, 100.0f),
			MapMode.GrasslandCoverage => GetScalarLandColor(world.GrasslandCoverage[index], index, 0.0f, 100.0f),
			MapMode.Wetlands => GetWetlandColor(index),
			MapMode.SoilTypes => GetSoilColor(index),
			MapMode.SoilFertility => GetScalarLandColor(world.SoilFertility[index], index, 0.0f, 100.0f),
			MapMode.SoilInfiltration => GetScalarLandColor(world.SoilInfiltrationRateMmPerHour[index], index, 0.0f, 45.0f),
			MapMode.GeologicalTypes => GetRockColor(index),
			MapMode.GeologicalStability => GetSuitabilityColor(world.GeologicalStability[index], index),
			MapMode.SeismicRisk => GetRiskColor(world.SeismicRisk[index], index),
			MapMode.MineralResources => GetMineralResourceColor(index),
			MapMode.AnnualRunoff => GetRunoffColor(index),
			MapMode.PrevailingWinds => GetWindColor(index),
			MapMode.MonthlyWindDirection => GetMonthlyWindDirectionColor(world.MonthlyWindDirectionDegrees[monthlyIndex]),
			MapMode.MonthlyWindSpeed => GetGlobalScalarColor(world.MonthlyWindSpeedMps[monthlyIndex], 0.0f, 16.0f),
			_ => Colors.Magenta
		};
	}

	private Color GetElevationColor(float elevation)
	{
		if (elevation < 0.0f)
			return GetOceanColor(elevation);

		if (elevation < 250.0f)
			return new Color(0.57f, 0.70f, 0.49f); // lowlands: sage green
		if (elevation < 900.0f)
			return new Color(0.50f, 0.70f, 0.39f); // plains
		if (elevation < 1800.0f)
			return new Color(0.31f, 0.53f, 0.34f); // highlands
		if (elevation < 3200.0f)
			return new Color(0.49f, 0.39f, 0.31f); // mountains
		if (elevation < 5000.0f)
			return new Color(0.71f, 0.68f, 0.61f); // high mountains

		return new Color(0.94f, 0.94f, 0.89f);
	}

	private Color GetOceanColor(float elevation)
	{
		float depth = Mathf.Clamp(-elevation / 6300.0f, 0.0f, 1.0f);
		Color shallow = new Color(0.30f, 0.62f, 0.76f);
		Color deep = new Color(0.02f, 0.08f, 0.22f);
		return shallow.Lerp(deep, depth);
	}

	private Color GetTerrainColor(TerrainType terrain)
	{
		return terrain switch
		{
			TerrainType.CoastalWater => new Color(0.18f, 0.57f, 0.70f),
			TerrainType.ShallowSea => new Color(0.34f, 0.68f, 0.79f),
			TerrainType.ContinentalShelf => new Color(0.12f, 0.39f, 0.61f),
			TerrainType.DeepOcean => new Color(0.025f, 0.10f, 0.25f),
			TerrainType.CoastalLowland => new Color(0.49f, 0.66f, 0.61f),
			TerrainType.Lowland => new Color(0.57f, 0.70f, 0.48f),
			TerrainType.CoastalPlain => new Color(0.65f, 0.78f, 0.55f),
			TerrainType.Plains => new Color(0.51f, 0.71f, 0.40f),
			TerrainType.RollingHills => new Color(0.36f, 0.60f, 0.33f),
			TerrainType.RuggedHills => new Color(0.20f, 0.44f, 0.28f),
			TerrainType.Highlands => new Color(0.54f, 0.47f, 0.36f),
			TerrainType.Mountains => new Color(0.46f, 0.39f, 0.35f),
			TerrainType.HighMountains => new Color(0.70f, 0.67f, 0.62f),
			TerrainType.AlpinePeaks => new Color(0.95f, 0.94f, 0.91f),
			_ => Colors.Magenta
		};
	}

	private Color GetTerrainWithHydrologyColor(int index)
	{
		if (_worldData == null)
			return Colors.Magenta;

		// Lakes are water cells. River markers are blended over the landform color
		// because a channel intersects a coarse land cell without filling it.
		if (_worldData.IsLake[index])
			return GetHydrologyColor(index);

		// Dry desert biomes use a clear sand-yellow overlay on the main terrain map.
		// This makes arid regions legible without confusing them with green lowlands.
		Color terrainColor = _worldData.Biomes[index] switch
		{
			BiomeType.HotDesert => new Color(0.96f, 0.78f, 0.20f),
			BiomeType.ColdDesert => new Color(0.86f, 0.76f, 0.43f),
			_ => GetTerrainColor(_worldData.TerrainTypes[index])
		};
		if (_worldData.IsRiver[index])
			return terrainColor.Lerp(GetHydrologyColor(index), 0.72f);

		return terrainColor;
	}

	private Color GetSlopeColor(int index)
	{
		if (_worldData == null)
			return Colors.Magenta;

		if (_worldData.IsLake[index])
			return GetHydrologyColor(index);

		if (!_worldData.IsLand[index])
			return GetOceanColor(_worldData.ElevationMeters[index]);

		float slope = _worldData.SlopeDegrees[index];

		// Fine low-angle bands make the global map readable without
		// exaggerating the stored slope values.
		if (slope < 0.05f)
			return new Color(0.12f, 0.43f, 0.25f);
		if (slope < 0.10f)
			return new Color(0.28f, 0.65f, 0.35f);
		if (slope < 0.50f)
			return new Color(0.61f, 0.77f, 0.30f);
		if (slope < 2.0f)
			return new Color(0.91f, 0.82f, 0.24f);
		if (slope < 5.0f)
			return new Color(0.95f, 0.57f, 0.18f);
		if (slope < 15.0f)
			return new Color(0.87f, 0.30f, 0.17f);
		if (slope < 30.0f)
			return new Color(0.62f, 0.12f, 0.15f);

		return new Color(0.34f, 0.035f, 0.10f);
	}

	private Color GetAspectColor(int index)
	{
		if (_worldData == null)
			return Colors.Magenta;

		if (_worldData.IsLake[index])
			return GetHydrologyColor(index);

		if (!_worldData.IsLand[index])
			return GetOceanColor(_worldData.ElevationMeters[index]);

		float aspect = _worldData.AspectDegrees[index];
		if (aspect < 0.0f)
			return new Color(0.67f, 0.68f, 0.68f);

		int sector = Mathf.FloorToInt((aspect + 22.5f) / 45.0f) % 8;
		return AspectColors[sector];
	}

	private Color GetReliefColor(int index)
	{
		if (_worldData == null)
			return Colors.Magenta;

		if (_worldData.IsLake[index])
			return GetHydrologyColor(index);

		if (!_worldData.IsLand[index])
			return GetOceanColor(_worldData.ElevationMeters[index]);

		float relief = _worldData.LocalReliefMeters[index];

		if (relief < 25.0f)
			return new Color(0.18f, 0.52f, 0.31f);
		if (relief < 75.0f)
			return new Color(0.42f, 0.70f, 0.32f);
		if (relief < 150.0f)
			return new Color(0.75f, 0.78f, 0.25f);
		if (relief < 300.0f)
			return new Color(0.94f, 0.67f, 0.19f);
		if (relief < 600.0f)
			return new Color(0.88f, 0.36f, 0.16f);
		if (relief < 1000.0f)
			return new Color(0.68f, 0.14f, 0.15f);

		return new Color(0.39f, 0.045f, 0.12f);
	}

	private Color GetCoastalSurfaceColor(int index)
	{
		if (_worldData == null)
			return Colors.Magenta;

		if (_worldData.IsLake[index])
			return new Color(0.25f, 0.72f, 0.90f);

		if (_worldData.IsRiver[index])
			return GetHydrologyColor(index);

		if (_worldData.IsLand[index])
		{
			return _worldData.IsCoastal[index]
				? new Color(0.49f, 0.66f, 0.61f)
				: new Color(0.29f, 0.53f, 0.35f);
		}

		return _worldData.IsCoastal[index]
			? new Color(0.20f, 0.61f, 0.72f)
			: new Color(0.025f, 0.10f, 0.25f);
	}

	private Color GetArmyMovementColor(int index)
	{
		if (_worldData == null)
			return Colors.Magenta;

		// Ocean and inland lakes are not traversable by ordinary land armies.
		if (!_worldData.IsLand[index] || _worldData.IsLake[index])
			return new Color(0.025f, 0.10f, 0.25f);

		float cost = _worldData.ArmyMovementCostMultiplier[index];
		Color terrainCostColor;
		if (cost <= 1.10f)
			terrainCostColor = new Color(0.13f, 0.52f, 0.31f);
		else if (cost <= 1.50f)
			terrainCostColor = new Color(0.43f, 0.70f, 0.32f);
		else if (cost <= 2.20f)
			terrainCostColor = new Color(0.83f, 0.79f, 0.24f);
		else if (cost <= 3.50f)
			terrainCostColor = new Color(0.94f, 0.55f, 0.18f);
		else if (cost <= 5.50f)
			terrainCostColor = new Color(0.82f, 0.25f, 0.16f);
		else
			terrainCostColor = new Color(0.43f, 0.055f, 0.12f);

		// A river-bearing cell remains traversable. Cyan tint identifies the
		// channel; its crossing penalty is handled by the edge-transition API.
		return _worldData.IsRiver[index]
			? terrainCostColor.Lerp(GetHydrologyColor(index), 0.62f)
			: terrainCostColor;
	}

	private Color GetConstructionSuitabilityColor(int index)
	{
		if (_worldData == null)
			return Colors.Magenta;

		// Only ocean and inland-lake cells are hard exclusions. River-bearing
		// land cells retain their suitability score and receive a cyan overlay.
		if (!_worldData.IsLand[index] || _worldData.IsLake[index])
			return new Color(0.025f, 0.10f, 0.25f);

		float score = _worldData.ConstructionSuitability[index];
		Color suitabilityColor;
		if (score >= 80.0f)
			suitabilityColor = new Color(0.13f, 0.52f, 0.31f);
		else if (score >= 60.0f)
			suitabilityColor = new Color(0.43f, 0.70f, 0.32f);
		else if (score >= 40.0f)
			suitabilityColor = new Color(0.88f, 0.78f, 0.22f);
		else if (score >= 20.0f)
			suitabilityColor = new Color(0.94f, 0.52f, 0.17f);
		else
			suitabilityColor = new Color(0.70f, 0.12f, 0.15f);

		return _worldData.IsRiver[index]
			? suitabilityColor.Lerp(GetHydrologyColor(index), 0.20f)
			: suitabilityColor;
	}

	private Color GetHydrologyColor(int index)
	{
		if (_worldData == null)
			return Colors.Magenta;

		if (_worldData.IsLake[index])
			return new Color(0.25f, 0.72f, 0.90f);

		if (!_worldData.IsLand[index])
			return GetOceanColor(_worldData.ElevationMeters[index]);

		if (_worldData.IsRiver[index])
		{
			float catchment = _worldData.FlowAccumulationKm2[index];

			if (catchment >= _worldData.MajorRiverThresholdKm2 * 3.0f)
				return new Color(0.55f, 0.95f, 1.0f);

			if (catchment >= _worldData.MajorRiverThresholdKm2)
				return new Color(0.22f, 0.78f, 0.98f);

			if (catchment >= _worldData.RiverStartThresholdKm2 * 3.0f)
				return new Color(0.10f, 0.60f, 0.90f);

			return new Color(0.08f, 0.40f, 0.72f);
		}

		return new Color(0.25f, 0.34f, 0.27f);
	}

	private Color GetRegionalFloodSusceptibilityColor(int index)
	{
		if (_worldData == null)
			return Colors.Magenta;

		if (!_worldData.IsLand[index])
			return GetOceanColor(_worldData.ElevationMeters[index]);

		if (_worldData.IsLake[index] || _worldData.IsRiver[index])
			return GetHydrologyColor(index);

		float susceptibility = _worldData.RegionalFloodSusceptibility[index];
		if (susceptibility < 10.0f)
			return new Color(0.16f, 0.43f, 0.29f); // very low
		if (susceptibility < 20.0f)
			return new Color(0.35f, 0.65f, 0.36f); // low
		if (susceptibility < 35.0f)
			return new Color(0.70f, 0.74f, 0.28f); // moderate
		if (susceptibility < 50.0f)
			return new Color(0.91f, 0.73f, 0.20f); // elevated
		if (susceptibility < 65.0f)
			return new Color(0.93f, 0.43f, 0.16f); // high

		return new Color(0.66f, 0.10f, 0.14f); // very high
	}


	private Color GetPrecipitationColor(int index)
	{
		float rain = _worldData!.AnnualPrecipitationMm[index];
		if (rain < 100.0f) return new Color(0.88f, 0.82f, 0.63f);
		if (rain < 250.0f) return new Color(0.78f, 0.75f, 0.53f);
		if (rain < 500.0f) return new Color(0.66f, 0.72f, 0.43f);
		if (rain < 800.0f) return new Color(0.40f, 0.68f, 0.40f);
		if (rain < 1200.0f) return new Color(0.20f, 0.58f, 0.43f);
		if (rain < 1800.0f) return new Color(0.12f, 0.48f, 0.57f);
		return new Color(0.10f, 0.30f, 0.68f);
	}

	private Color GetAridityColor(int index)
	{
		if (!_worldData!.IsLand[index]) return GetOceanColor(_worldData.ElevationMeters[index]);
		float a = _worldData.AridityIndex[index];
		if (a < 0.20f) return new Color(0.72f, 0.31f, 0.20f);
		if (a < 0.35f) return new Color(0.86f, 0.53f, 0.25f);
		if (a < 0.65f) return new Color(0.83f, 0.76f, 0.39f);
		if (a < 1.0f) return new Color(0.55f, 0.70f, 0.40f);
		if (a < 1.5f) return new Color(0.28f, 0.62f, 0.42f);
		return new Color(0.10f, 0.42f, 0.43f);
	}

	private Color GetBiomeColor(int index)
	{
		if (_worldData!.IsLake[index]) return GetHydrologyColor(index);
		if (!_worldData.IsLand[index]) return GetOceanColor(_worldData.ElevationMeters[index]);
		return _worldData.Biomes[index] switch
		{
			BiomeType.TropicalRainforest => new Color(0.05f, 0.34f, 0.19f),
			BiomeType.TropicalSeasonalForest => new Color(0.16f, 0.53f, 0.27f),
			BiomeType.Savanna => new Color(0.72f, 0.69f, 0.30f),
			BiomeType.HotDesert => new Color(0.96f, 0.78f, 0.20f),
			BiomeType.ColdDesert => new Color(0.86f, 0.76f, 0.43f),
			BiomeType.Steppe => new Color(0.72f, 0.76f, 0.43f),
			BiomeType.Mediterranean => new Color(0.49f, 0.67f, 0.37f),
			BiomeType.TemperateGrassland => new Color(0.62f, 0.72f, 0.39f),
			BiomeType.TemperateForest => new Color(0.20f, 0.48f, 0.30f),
			BiomeType.BorealForest => new Color(0.20f, 0.39f, 0.43f),
			BiomeType.Tundra => new Color(0.68f, 0.75f, 0.75f),
			BiomeType.Alpine => new Color(0.84f, 0.84f, 0.80f),
			BiomeType.Wetland => new Color(0.10f, 0.62f, 0.56f),
			_ => new Color(0.35f, 0.40f, 0.32f)
		};
	}

	private Color GetRunoffColor(int index)
	{
		if (!_worldData!.IsLand[index]) return GetOceanColor(_worldData.ElevationMeters[index]);
		float r = _worldData.AnnualRunoffMm[index];
		if (r < 5.0f) return new Color(0.72f, 0.35f, 0.23f);
		if (r < 25.0f) return new Color(0.83f, 0.59f, 0.27f);
		if (r < 75.0f) return new Color(0.80f, 0.76f, 0.34f);
		if (r < 150.0f) return new Color(0.53f, 0.70f, 0.39f);
		if (r < 300.0f) return new Color(0.24f, 0.62f, 0.43f);
		if (r < 600.0f) return new Color(0.12f, 0.52f, 0.59f);
		return new Color(0.12f, 0.34f, 0.72f);
	}

	private Color GetWindColor(int index)
	{
		if (_worldData == null)
			return Colors.Magenta;
		int sector = ((int)Mathf.Round(_worldData.PrevailingWindDirectionDegrees[index] / 45.0f)) % 8;
		return AspectColors[sector];
	}

	private Color GetTemperatureColor(float temperature)
	{
		if (_worldData == null)
			return Colors.Magenta;
		if (temperature < -20.0f) return new Color(0.90f, 0.96f, 1.0f);
		if (temperature < -10.0f) return new Color(0.63f, 0.82f, 0.96f);
		if (temperature < 0.0f) return new Color(0.32f, 0.61f, 0.86f);
		if (temperature < 10.0f) return new Color(0.30f, 0.70f, 0.62f);
		if (temperature < 20.0f) return new Color(0.53f, 0.76f, 0.39f);
		if (temperature < 28.0f) return new Color(0.91f, 0.76f, 0.27f);
		if (temperature < 35.0f) return new Color(0.94f, 0.45f, 0.18f);
		return new Color(0.70f, 0.12f, 0.14f);
	}

	private Color GetScalarLandColor(float value, int index, float minimum, float maximum)
	{
		if (_worldData == null)
			return Colors.Magenta;
		if (!_worldData.IsLand[index])
			return GetOceanColor(_worldData.ElevationMeters[index]);
		if (_worldData.IsLake[index])
			return GetHydrologyColor(index);
		float t = Mathf.Clamp((value - minimum) / Mathf.Max(0.001f, maximum - minimum), 0.0f, 1.0f);
		Color low = new Color(0.13f, 0.30f, 0.39f);
		Color middle = new Color(0.29f, 0.65f, 0.43f);
		Color high = new Color(0.94f, 0.83f, 0.30f);
		return t < 0.55f ? low.Lerp(middle, t / 0.55f) : middle.Lerp(high, (t - 0.55f) / 0.45f);
	}

	private Color GetGlobalScalarColor(float value, float minimum, float maximum)
	{
		float t = Mathf.Clamp((value - minimum) / Mathf.Max(0.001f, maximum - minimum), 0.0f, 1.0f);
		Color low = new Color(0.10f, 0.20f, 0.42f);
		Color middle = new Color(0.12f, 0.62f, 0.78f);
		Color high = new Color(0.91f, 0.91f, 0.73f);
		return t < 0.55f ? low.Lerp(middle, t / 0.55f) : middle.Lerp(high, (t - 0.55f) / 0.45f);
	}

	private Color GetMonthlyWindDirectionColor(float directionDegrees)
	{
		int sector = ((int)Mathf.Round(directionDegrees / 45.0f)) % 8;
		return AspectColors[sector];
	}

	private Color GetSuitabilityColor(float score, int index)
	{
		if (_worldData == null)
			return Colors.Magenta;
		if (!_worldData.IsLand[index] || _worldData.IsLake[index])
			return new Color(0.025f, 0.10f, 0.25f);
		Color color = score >= 80.0f ? new Color(0.13f, 0.52f, 0.31f)
			: score >= 60.0f ? new Color(0.43f, 0.70f, 0.32f)
			: score >= 40.0f ? new Color(0.88f, 0.78f, 0.22f)
			: score >= 20.0f ? new Color(0.94f, 0.52f, 0.17f)
			: new Color(0.70f, 0.12f, 0.15f);
		return _worldData.IsRiver[index] ? color.Lerp(GetHydrologyColor(index), 0.18f) : color;
	}

	private Color GetRiskColor(float risk, int index)
	{
		if (_worldData == null || !_worldData.IsLand[index])
			return _worldData == null ? Colors.Magenta : GetOceanColor(_worldData.ElevationMeters[index]);
		if (_worldData.IsLake[index])
			return GetHydrologyColor(index);
		Color color = risk < 20.0f ? new Color(0.16f, 0.48f, 0.30f)
			: risk < 40.0f ? new Color(0.48f, 0.70f, 0.31f)
			: risk < 60.0f ? new Color(0.86f, 0.77f, 0.22f)
			: risk < 80.0f ? new Color(0.93f, 0.43f, 0.16f)
			: new Color(0.63f, 0.08f, 0.14f);
		return _worldData.IsRiver[index] ? color.Lerp(GetHydrologyColor(index), 0.16f) : color;
	}

	private Color GetMovementCostColor(float cost, int index)
	{
		if (_worldData == null || !_worldData.IsLand[index] || _worldData.IsLake[index])
			return new Color(0.025f, 0.10f, 0.25f);
		Color color = cost <= 1.10f ? new Color(0.13f, 0.52f, 0.31f)
			: cost <= 1.50f ? new Color(0.43f, 0.70f, 0.32f)
			: cost <= 2.20f ? new Color(0.83f, 0.79f, 0.24f)
			: cost <= 3.50f ? new Color(0.94f, 0.55f, 0.18f)
			: cost <= 5.50f ? new Color(0.82f, 0.25f, 0.16f)
			: new Color(0.43f, 0.055f, 0.12f);
		return _worldData.IsRiver[index] ? color.Lerp(GetHydrologyColor(index), 0.55f) : color;
	}

	private Color GetClimateZoneColor(int index)
	{
		if (_worldData == null || !_worldData.IsLand[index])
			return _worldData == null ? Colors.Magenta : GetOceanColor(_worldData.ElevationMeters[index]);
		return _worldData.ClimateZones[index] switch
		{
			ClimateZoneType.Tropical => new Color(0.10f, 0.48f, 0.26f),
			ClimateZoneType.Arid => new Color(0.84f, 0.55f, 0.28f),
			ClimateZoneType.SemiArid => new Color(0.82f, 0.72f, 0.36f),
			ClimateZoneType.DrySubhumid => new Color(0.62f, 0.72f, 0.38f),
			ClimateZoneType.Mediterranean => new Color(0.38f, 0.65f, 0.37f),
			ClimateZoneType.TemperateOceanic => new Color(0.20f, 0.58f, 0.57f),
			ClimateZoneType.TemperateContinental => new Color(0.34f, 0.67f, 0.78f),
			ClimateZoneType.Subpolar => new Color(0.55f, 0.70f, 0.79f),
			ClimateZoneType.Polar => new Color(0.88f, 0.93f, 0.96f),
			ClimateZoneType.Highland => new Color(0.58f, 0.48f, 0.42f),
			_ => new Color(0.35f, 0.40f, 0.32f)
		};
	}

	private Color GetSoilColor(int index)
	{
		if (_worldData == null)
			return Colors.Magenta;
		if (_worldData.IsLake[index])
			return GetHydrologyColor(index);
		if (!_worldData.IsLand[index])
			return GetOceanColor(_worldData.ElevationMeters[index]);
		return _worldData.SoilTypes[index] switch
		{
			SoilType.Sandy => new Color(0.82f, 0.69f, 0.43f),
			SoilType.Clay => new Color(0.62f, 0.31f, 0.23f),
			SoilType.Loamy => new Color(0.45f, 0.58f, 0.34f),
			SoilType.SiltRich => new Color(0.72f, 0.68f, 0.42f),
			SoilType.Rocky => new Color(0.48f, 0.46f, 0.43f),
			SoilType.Peaty => new Color(0.24f, 0.39f, 0.29f),
			SoilType.Saline => new Color(0.78f, 0.76f, 0.65f),
			SoilType.ShallowMountain => new Color(0.57f, 0.52f, 0.49f),
			_ => new Color(0.30f, 0.36f, 0.31f)
		};
	}

	private Color GetRockColor(int index)
	{
		if (_worldData == null)
			return Colors.Magenta;
		if (_worldData.IsLake[index])
			return GetHydrologyColor(index);
		if (!_worldData.IsLand[index])
			return GetOceanColor(_worldData.ElevationMeters[index]);
		return _worldData.RockTypes[index] switch
		{
			RockType.Sedimentary => new Color(0.73f, 0.60f, 0.39f),
			RockType.Igneous => new Color(0.62f, 0.34f, 0.29f),
			RockType.Metamorphic => new Color(0.52f, 0.43f, 0.66f),
			RockType.Volcanic => new Color(0.75f, 0.20f, 0.16f),
			RockType.Cratonic => new Color(0.39f, 0.48f, 0.43f),
			RockType.Alluvial => new Color(0.73f, 0.76f, 0.50f),
			_ => new Color(0.30f, 0.34f, 0.34f)
		};
	}

	private Color GetRiverDischargeColor(int index)
	{
		if (_worldData == null)
			return Colors.Magenta;
		if (_worldData.IsLake[index] || !_worldData.IsLand[index])
			return GetHydrologyColor(index);
		if (!_worldData.IsRiver[index])
			return new Color(0.20f, 0.32f, 0.27f);
		float discharge = _worldData.MeanAnnualDischargeM3PerSecond[index];
		float intensity = Mathf.Clamp(Mathf.Log(discharge + 1.0f) / 12.0f, 0.0f, 1.0f);
		return new Color(0.06f, 0.30f, 0.66f).Lerp(new Color(0.48f, 0.92f, 1.0f), intensity);
	}

	private Color GetRiverPermanenceColor(int index)
	{
		if (_worldData == null)
			return Colors.Magenta;
		if (_worldData.IsLake[index] || !_worldData.IsLand[index])
			return GetHydrologyColor(index);
		if (!_worldData.IsRiver[index])
			return new Color(0.20f, 0.32f, 0.27f);
		return _worldData.RiverFlowRegimes[index] switch
		{
			RiverFlowRegime.Perennial => new Color(0.20f, 0.78f, 0.96f),
			RiverFlowRegime.Seasonal => new Color(0.26f, 0.55f, 0.90f),
			RiverFlowRegime.Intermittent => new Color(0.82f, 0.78f, 0.30f),
			_ => new Color(0.35f, 0.40f, 0.32f)
		};
	}

	private Color GetWetlandColor(int index)
	{
		if (_worldData == null)
			return Colors.Magenta;
		if (!_worldData.IsLand[index] || _worldData.IsLake[index])
			return GetHydrologyColor(index);
		float wetland = _worldData.WetlandCoverage[index] / 100.0f;
		return new Color(0.20f, 0.34f, 0.28f).Lerp(new Color(0.10f, 0.72f, 0.65f), wetland);
	}

	private Color GetMineralResourceColor(int index)
	{
		if (_worldData == null)
			return Colors.Magenta;
		if (_worldData.IsLake[index])
			return GetHydrologyColor(index);
		if (!_worldData.IsLand[index])
			return GetOceanColor(_worldData.ElevationMeters[index]);
		float maximum = 0.0f;
		for (int resource = 0; resource <= (int)NaturalResourceType.Tin; resource++)
			maximum = Mathf.Max(maximum, _worldData.ResourceAbundance[resource * _worldData.Width * _worldData.Height + index]);
		return GetScalarLandColor(maximum, index, 0.0f, 100.0f);
	}

	// ==========================================
	// 4. VISUAL SELECTION OVERLAY
	// ==========================================

	private void CreateSelectionOverlay()
	{
		Node2D overlay = new Node2D
		{
			Name = "SelectionOverlay",
			Visible = false,
			ZIndex = 5
		};

		Line2D ring = new Line2D
		{
			Name = "SelectionRing",
			Width = 2.5f,
			DefaultColor = Colors.Yellow,
			Antialiased = true,
			Closed = true
		};

		for (int i = 0; i < 48; i++)
		{
			float angle = Mathf.Tau * i / 48.0f;
			ring.AddPoint(new Vector2(
				Mathf.Cos(angle) * 10.0f,
				Mathf.Sin(angle) * 10.0f
			));
		}

		overlay.AddChild(ring);
		overlay.AddChild(CreateLine("LeftLine", new Vector2(-17, 0), new Vector2(-7, 0)));
		overlay.AddChild(CreateLine("RightLine", new Vector2(7, 0), new Vector2(17, 0)));
		overlay.AddChild(CreateLine("TopLine", new Vector2(0, -17), new Vector2(0, -7)));
		overlay.AddChild(CreateLine("BottomLine", new Vector2(0, 7), new Vector2(0, 17)));

		Line2D center = new Line2D
		{
			Name = "SelectionCenter",
			Width = 2.0f,
			DefaultColor = Colors.White,
			Antialiased = true,
			Closed = true
		};

		for (int i = 0; i < 16; i++)
		{
			float angle = Mathf.Tau * i / 16.0f;
			center.AddPoint(new Vector2(
				Mathf.Cos(angle) * 2.5f,
				Mathf.Sin(angle) * 2.5f
			));
		}

		overlay.AddChild(center);
		_selectionOverlay = overlay;
		UpdateSelectionOverlayScale();
		AddChild(overlay);
	}

	private Line2D CreateLine(string nodeName, Vector2 start, Vector2 end)
	{
		Line2D line = new Line2D
		{
			Name = nodeName,
			Width = 2.5f,
			DefaultColor = Colors.Yellow,
			Antialiased = true
		};

		line.AddPoint(start);
		line.AddPoint(end);
		return line;
	}

	private void UpdateSelectionOverlayScale()
	{
		if (_selectionOverlay == null)
			return;

		float zoom = Mathf.Max(Mathf.Abs(Scale.X), 0.0001f);
		_selectionOverlay.Scale = Vector2.One / zoom;
	}

	// ==========================================
	// 5. INPUT HANDLING
	// ==========================================

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton button)
		{
			if (button.ButtonIndex == MouseButton.Left)
			{
				if (button.Pressed)
				{
					_leftButtonDown = true;
					_leftDragStarted = false;
					_pressPosition = button.Position;
				}
				else if (_leftButtonDown)
				{
					if (!_leftDragStarted)
						SelectMapLocation();

					_leftButtonDown = false;
					_leftDragStarted = false;
				}

				GetViewport().SetInputAsHandled();
				return;
			}

			if (button.ButtonIndex == MouseButton.Middle ||
				button.ButtonIndex == MouseButton.Right)
			{
				_auxiliaryDragging = button.Pressed;
				GetViewport().SetInputAsHandled();
				return;
			}

			if (button.Pressed && button.ButtonIndex == MouseButton.WheelUp)
			{
				ZoomAtCursor(ZoomStep);
				GetViewport().SetInputAsHandled();
				return;
			}

			if (button.Pressed && button.ButtonIndex == MouseButton.WheelDown)
			{
				ZoomAtCursor(1.0f / ZoomStep);
				GetViewport().SetInputAsHandled();
				return;
			}
		}

		if (@event is InputEventMouseMotion motion)
		{
			if (_auxiliaryDragging)
			{
				// Node2D.Position is in parent coordinates. Do not divide by
				// the map zoom; doing so makes panning slower as the map zooms in.
				Position += motion.Relative;
				GetViewport().SetInputAsHandled();
				return;
			}

			if (_leftButtonDown)
			{
				if (!_leftDragStarted &&
					motion.Position.DistanceTo(_pressPosition) >= DragThreshold)
				{
					_leftDragStarted = true;
				}

				if (_leftDragStarted)
					Position += motion.Relative;

				GetViewport().SetInputAsHandled();
			}
		}
	}

	// ==========================================
	// 6. ZOOM SYSTEM
	// ==========================================

	private void ZoomAtCursor(float factor)
	{
		float currentZoom = Scale.X;
		float newZoom = Mathf.Clamp(currentZoom * factor, MinZoom, MaxZoom);

		if (Mathf.IsEqualApprox(currentZoom, newZoom))
			return;

		Vector2 mouseGlobal = GetGlobalMousePosition();
		Vector2 localBefore = ToLocal(mouseGlobal);

		Scale = Vector2.One * newZoom;

		Vector2 localAfter = ToLocal(mouseGlobal);
		Position += (localAfter - localBefore) * newZoom;

		UpdateSelectionOverlayScale();
		GD.Print($"MAP ZOOM: {newZoom:F2}x");
	}

	// ==========================================
	// 7. GEOGRAPHICAL LOCATION SELECTION
	// ==========================================

	private void SelectMapLocation()
	{
		if (_worldImage?.Texture == null)
			return;

		Vector2 localPosition = _worldImage.ToLocal(GetGlobalMousePosition());
		Vector2 textureSize = _worldImage.Texture.GetSize();

		int imageWidth = Mathf.RoundToInt(textureSize.X);
		int imageHeight = Mathf.RoundToInt(textureSize.Y);

		float offsetX = _worldImage.Centered ? imageWidth * 0.5f : 0.0f;
		float offsetY = _worldImage.Centered ? imageHeight * 0.5f : 0.0f;

		int pixelX = Mathf.FloorToInt(localPosition.X + offsetX);
		int pixelY = Mathf.FloorToInt(localPosition.Y + offsetY);

		if (pixelX < 0 || pixelX >= imageWidth ||
			pixelY < 0 || pixelY >= imageHeight)
		{
			GD.Print("Outside map boundaries.");
			return;
		}

		Vector2I imageDimensions = new Vector2I(imageWidth, imageHeight);
		Vector2 coordinates = WorldCoordinateSystem.ImagePixelToLatLon(
			new Vector2(pixelX + 0.5f, pixelY + 0.5f),
			imageDimensions
		);

		float longitude = coordinates.X;
		float latitude = coordinates.Y;
		string latitudeDirection = latitude >= 0.0f ? "N" : "S";
		string longitudeDirection = longitude >= 0.0f ? "E" : "W";

		if (_worldData == null)
		{
			UpdateSelectionMarker(localPosition);
			SetInspectorText(
				$"Latitude: {Mathf.Abs(latitude):F4}° {latitudeDirection}\n" +
				$"Longitude: {Mathf.Abs(longitude):F4}° {longitudeDirection}\n\n" +
				"World data: Unavailable. Check the WorldGenerator node."
			);
			return;
		}

		int dataX = Mathf.Clamp(
			Mathf.FloorToInt(((pixelX + 0.5f) / imageWidth) * _worldData.Width),
			0,
			_worldData.Width - 1
		);

		int dataY = Mathf.Clamp(
			Mathf.FloorToInt(((pixelY + 0.5f) / imageHeight) * _worldData.Height),
			0,
			_worldData.Height - 1
		);

		int index = _worldData.GetIndex(dataX, dataY);
		_selectedDataIndex = index;

		float elevation = _worldData.ElevationMeters[index];
		bool isLand = _worldData.IsLand[index];
		bool isCoastal = _worldData.IsCoastal[index];
		float localRelief = _worldData.LocalReliefMeters[index];
		float slope = _worldData.SlopeDegrees[index];
		float aspect = _worldData.AspectDegrees[index];
		TerrainType terrainType = _worldData.TerrainTypes[index];
		bool isRiver = _worldData.IsRiver[index];
		bool isLake = _worldData.IsLake[index];
		float catchmentArea = _worldData.FlowAccumulationKm2[index];
		float depressionDepth = _worldData.DepressionDepthMeters[index];
		int riverOrder = _worldData.RiverOrder[index];
		float riverCrossingCost = _worldData.RiverCrossingCostMultiplier[index];
		int downstreamCell = _worldData.FlowToCell[index];
		float floodSusceptibility = _worldData.RegionalFloodSusceptibility[index];
		bool isElevatedFloodSusceptibility = _worldData.IsElevatedFloodSusceptibility[index];
		float floodplainRiverDistance = _worldData.FloodplainReferenceRiverDistanceKm[index];
		float heightAboveReferenceRiver = _worldData.HeightAboveReferenceRiverMeters[index];

		int cellX = dataX / CellPixelSize;
		int cellY = dataY / CellPixelSize;

		string terrain = GetTerrainName(terrainType);
		string surface = isLake ? "Inland lake water" : !isLand ? "Ocean" : isRiver ? "Land (mapped river channel)" : "Land";
		string elevationText = elevation >= 0.0f
			? $"{elevation:N0} m above sea level"
			: $"{Mathf.Abs(elevation):N0} m below sea level";

		string aspectText = FormatAspect(aspect, slope);
		string slopeClass = ClassifySlope(slope);
		Vector2 reliefWindowKm = _worldData.GetReliefWindowDimensionsKm(dataY);

		string armyMovementText = isLand && !isLake
			? $"{_worldData.ArmyMovementCostMultiplier[index]:F2}× terrain cost ({ClassifyArmyMovement(_worldData.ArmyMovementCostMultiplier[index])})"
			: "Impassable to ordinary land armies (ocean/lake)";

		float constructionScore = _worldData.ConstructionSuitability[index];
		string constructionText = isLand && !isLake
			? $"{constructionScore:F0}/100 ({ClassifyConstructionSuitability(constructionScore)}; includes regional flood and river-cell adjustments)" +
				(isRiver ? "; mapped channel intersects this land cell; exact footprint unresolved" : "")
			: "Not buildable at this grid scale (ocean/lake)";

		string hydrologyText = isLake
			? $"Inland lake basin (depression depth: {depressionDepth:N0} m)"
			: isRiver
				? $"Mapped river channel intersects this land cell (Strahler order {riverOrder})"
				: isLand
					? "Land drainage area; no mapped river channel"
					: "Ocean / receiving water";

		string catchmentText = isLand
			? $"{catchmentArea:N0} km² upstream catchment"
			: "Not applicable";
		float meanTemperature = _worldData.MeanAnnualTemperatureC[index];
		float annualRainfall = _worldData.AnnualPrecipitationMm[index];
		float annualPet = _worldData.AnnualPotentialEvapotranspirationMm[index];
		float aridity = _worldData.AridityIndex[index];
		float annualRunoff = _worldData.AnnualRunoffMm[index];
		float windDirection = _worldData.PrevailingWindDirectionDegrees[index];
		float meanDischarge = _worldData.MeanAnnualDischargeM3PerSecond[index];
		string biomeText = isLake ? "Lake" : _worldData.Biomes[index].ToString();
		string riverRegimeText = _worldData.RiverFlowRegimes[index].ToString();
		string monthlyClimateText = FormatMonthlyClimate(index);
		int wettestMonth = _worldData.WettestMonth[index] + 1;
		int driestMonth = _worldData.DriestMonth[index] + 1;
		int selectedMonthlyIndex = _worldData.GetMonthlyIndex(index, _selectedMonth);
		string[] monthNames = { "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" };
		string selectedMonthText = isLand
			? $"{monthNames[_selectedMonth]}: {_worldData.MonthlyTemperatureC[selectedMonthlyIndex]:F1} °C, {_worldData.MonthlyPrecipitationMm[selectedMonthlyIndex]:F0} mm precipitation, {_worldData.MonthlyWindDirectionDegrees[selectedMonthlyIndex]:F0}° wind at {_worldData.MonthlyWindSpeedMps[selectedMonthlyIndex]:F1} m/s"
			: $"{monthNames[_selectedMonth]}: {_worldData.MonthlyTemperatureC[selectedMonthlyIndex]:F1} °C, {_worldData.MonthlyPrecipitationMm[selectedMonthlyIndex]:F0} mm precipitation";
		string soilText = isLand && !isLake ? _worldData.SoilTypes[index].ToString() : "N/A (water)";
		string rockText = isLand && !isLake ? _worldData.RockTypes[index].ToString() : "N/A (water)";
		string provinceText = isLand && !isLake ? _worldData.TectonicProvinceIds[index].ToString() : "N/A (water)";
		string vegetationText = isLand && !isLake ? _worldData.VegetationTypes[index].ToString() : "N/A (water)";
		string resourceText = isLand && !isLake ? FormatTopResources(index) : "N/A (water)";
		string agricultureText = isLand && !isLake ? $"{_worldData.AgriculturalSuitability[index]:F0}/100" : "N/A";
		string settlementText = isLand && !isLake ? $"{_worldData.SettlementSuitability[index]:F0}/100" : "N/A";
		string freshwaterText = isLake ? "Inland lake" : isLand ? $"{_worldData.FreshwaterAccess[index]:F0}/100" : "N/A";
		string groundwaterText = isLand && !isLake ? $"{_worldData.GroundwaterPotential[index]:F0}/100" : "N/A";
		string monthlyDischargeText = isRiver ? $"Peak month: {_worldData.PeakMonthlyDischargeM3PerSecond[index]:F1} m³/s; seasonality {_worldData.DischargeSeasonalityIndex[index]:F2}" : "No mapped river";

		string flowDirectionText = isLand && downstreamCell >= 0
			? $"Downstream cell X={downstreamCell % _worldData.Width}, Y={downstreamCell / _worldData.Width}"
			: "No downstream land cell";

		string riverCrossingText = isRiver
			? $"{riverCrossingCost:F2}× once on the first land-to-channel transition (provisional)"
			: isLake
				? "Impassable to ordinary land armies"
				: "No mapped river channel in this cell";

		string floodSusceptibilityText = isLand && !isRiver && !isLake
			? $"{floodSusceptibility:F0}/100 ({ClassifyRegionalFloodSusceptibility(floodSusceptibility)})"
			: isRiver
				? "Channel-bearing land cell; not scored separately"
				: isLake
					? "Inland lake cell; not applicable"
					: "Not applicable to ocean cells";

		string susceptibilityStatus = !isLand
			? "Open water / not applicable"
			: isLake
				? "Inland lake cell"
				: isRiver
					? "Mapped channel intersects land cell; exact footprint unresolved"
					: isElevatedFloodSusceptibility
						? "Elevated regional river-corridor susceptibility (threshold met)"
						: "Below the current regional indicator threshold";

		string referenceRiverText = !isLand
			? "Not applicable to ocean cells"
			: isRiver
				? "Mapped river channel intersects the selected land cell (grid-scale distance: 0 km)"
				: isLake
					? "Lake basin; adjacent susceptibility is not calculated here"
					: float.IsInfinity(floodplainRiverDistance)
						? $"No mapped river within {WorldData.RegionalFloodSusceptibilitySearchRadiusCells}-cell analysis radius"
						: $"{floodplainRiverDistance:N0} km away; {heightAboveReferenceRiver:N0} m above channel elevation";

		UpdateSelectionMarker(localPosition);

		SetInspectorText(
			$"Latitude: {Mathf.Abs(latitude):F4}° {latitudeDirection}\n" +
			$"Longitude: {Mathf.Abs(longitude):F4}° {longitudeDirection}\n\n" +
			$"Image X: {pixelX} | Y: {pixelY}\n" +
			$"Grid X: {cellX} | Y: {cellY}\n" +
			$"Data cell: X={dataX} | Y={dataY}\n\n" +
			$"Surface: {surface}\n" +
			$"Terrain: {terrain}\n" +
			$"Elevation: {elevationText}\n" +
			$"Coastal: {(isCoastal ? "Yes" : "No")}\n" +
			$"Local relief ({WorldData.ReliefWindowSize}×{WorldData.ReliefWindowSize} cells): {localRelief:N0} m\n" +
			$"Relief window: approx. {reliefWindowKm.X:N0} km E–W × {reliefWindowKm.Y:N0} km N–S\n\n" +
			$"Slope: {slope:F3}° ({slopeClass})\n" +
			$"Aspect: {aspectText}\n\n" +
			$"Hydrology: {hydrologyText}\n" +
			$"Catchment: {catchmentText}\n" +
			$"Biome: {biomeText}\n" +
			$"Mean annual temperature: {meanTemperature:F1} °C\n" +
			$"Annual precipitation: {annualRainfall:N0} mm\n" +
			$"Annual potential evapotranspiration: {annualPet:N0} mm\n" +
			$"Aridity index (P/PET): {aridity:F2}\n" +
			$"Prevailing wind direction: {windDirection:F0}° (clockwise from north)\n" +
			$"Wettest / driest month: {wettestMonth} / {driestMonth}\n" +
			$"Monthly temperature (Jan–Dec): {monthlyClimateText}\n" +
			$"Annual local runoff: {annualRunoff:N0} mm\n" +
			$"Mean annual downstream discharge: {meanDischarge:F1} m³/s\n" +
			$"River flow regime: {riverRegimeText}\n" +
			$"Flow routing: {flowDirectionText}\n" +
			$"River crossing: {riverCrossingText}\n" +
			$"Regional flood susceptibility: {floodSusceptibilityText}\n" +
			$"Regional susceptibility status: {susceptibilityStatus}\n" +
			$"Nearest mapped river: {referenceRiverText}\n\n" +
			$"Land army movement: {armyMovementText}\n" +
			$"Infantry / cavalry / vehicle movement: {(isLand && !isLake ? _worldData.InfantryMovementCostMultiplier[index].ToString("F2") + "× / " + _worldData.CavalryMovementCostMultiplier[index].ToString("F2") + "× / " + _worldData.VehicleMovementCostMultiplier[index].ToString("F2") + "×" : "Impassable") }\n" +
			$"Construction suitability: {constructionText}\n" +
			$"Agricultural suitability: {agricultureText}\n" +
			$"Settlement suitability: {settlementText}\n" +
			$"Selected month: {selectedMonthText}\n" +
			$"Climate zone: {(isLand ? _worldData.ClimateZones[index].ToString() : "Ocean")}; aridity class: {(isLand ? _worldData.AridityZones[index].ToString() : "N/A")}; pressure belt: {(isLand ? _worldData.PressureBelts[index].ToString() : "N/A")}\n" +
			$"Growing season: {(isLand ? _worldData.GrowingSeasonMonths[index].ToString() + " months" : "N/A")}\n" +
			$"Annual actual evapotranspiration: {(isLand && !isLake ? _worldData.AnnualActualEvapotranspirationMm[index].ToString("N0") + " mm" : "N/A")}\n" +
			$"Annual groundwater recharge: {(isLand && !isLake ? _worldData.AnnualGroundwaterRechargeMm[index].ToString("N0") + " mm" : "N/A")}\n" +
			$"Groundwater potential / freshwater access: {groundwaterText} / {freshwaterText}\n" +
			$"Annual water availability / lake balance: {(isLand && !isLake ? _worldData.WaterAvailabilityMm[index].ToString("N0") + " mm" : isLake ? _worldData.LakeWaterBalanceMm[index].ToString("N0") + " mm lake-balance proxy" : "N/A")}\n" +
			$"Peak monthly river discharge: {monthlyDischargeText}\n" +
			$"Soil: {soilText}; fertility: {(isLand && !isLake ? _worldData.SoilFertility[index].ToString("F0") + "/100" : "N/A")}; depth: {(isLand && !isLake ? _worldData.SoilDepthMeters[index].ToString("F2") + " m" : "N/A")}\n" +
			$"Soil infiltration: {(isLand && !isLake ? _worldData.SoilInfiltrationRateMmPerHour[index].ToString("F1") + " mm/h" : "N/A")}; drainage: {(isLand && !isLake ? _worldData.SoilDrainage[index].ToString("F0") + "/100" : "N/A")}\n" +
			$"Soil erosion risk / stability: {(isLand && !isLake ? _worldData.SoilErosionRisk[index].ToString("F0") + "/100" : "N/A")} / {(isLand && !isLake ? _worldData.SoilStability[index].ToString("F0") + "/100" : "N/A")}\n" +
			$"Geology: {rockText}; tectonic province {provinceText}; stability: {(isLand && !isLake ? _worldData.GeologicalStability[index].ToString("F0") + "/100" : "N/A")}; seismic risk: {(isLand && !isLake ? _worldData.SeismicRisk[index].ToString("F0") + "/100" : "N/A")}\n" +
			$"Vegetation: {vegetationText}; density: {(isLand && !isLake ? _worldData.VegetationDensity[index].ToString("F0") + "/100" : "N/A")}; forest: {(isLand && !isLake ? _worldData.ForestDensity[index].ToString("F0") + "/100" : "N/A")}; grassland: {(isLand && !isLake ? _worldData.GrasslandCoverage[index].ToString("F0") + "/100" : "N/A")}; wetland: {(isLand && !isLake ? _worldData.WetlandCoverage[index].ToString("F0") + "/100" : "N/A")}\n" +
			$"Resources (top local potential): {resourceText}\n" +
			"Note: all climate, geology, soil, ecology and resource layers are deterministic strategic-scale procedural estimates, not observed or surveyed data. Flood susceptibility is not a hydraulic flood-depth model; groundwater potential is not a well-yield prediction. River crossing remains provisional because channel-bank geometry, roads and bridges are not represented."
		);

		GD.Print("--------------------------------");
		GD.Print("GEOGRAPHICAL LOCATION SELECTED");
		GD.Print($"World data cell: X={dataX}, Y={dataY}");
		GD.Print($"Latitude: {latitude:F4}");
		GD.Print($"Longitude: {longitude:F4}");
		GD.Print($"Surface: {surface}");
		GD.Print($"Terrain: {terrain}");
		GD.Print($"Elevation: {elevationText}");
		GD.Print($"Local relief: {localRelief:N0} m");
		GD.Print($"Slope: {slope:F3} degrees");
		GD.Print($"Aspect: {aspectText}");
		GD.Print($"Hydrology: {hydrologyText}");
		GD.Print($"Catchment area: {catchmentText}");
		GD.Print($"Biome: {biomeText}");
		GD.Print($"Mean annual temperature: {meanTemperature:F1} °C");
		GD.Print($"Annual precipitation: {annualRainfall:N0} mm");
		GD.Print($"Annual runoff: {annualRunoff:N0} mm");
		GD.Print($"Mean annual discharge: {meanDischarge:F1} m³/s");
		GD.Print($"Flow routing: {flowDirectionText}");
		GD.Print($"River crossing: {riverCrossingText}");
		GD.Print($"Regional flood susceptibility: {floodSusceptibilityText}");
		GD.Print($"Regional susceptibility status: {susceptibilityStatus}");
		GD.Print($"Land army movement: {armyMovementText}");
		GD.Print($"Construction suitability: {constructionText}");
		GD.Print("--------------------------------");
	}

	private string FormatTopResources(int cellIndex)
	{
		if (_worldData == null)
			return "Unavailable";
		int size = _worldData.Width * _worldData.Height;
		float[] bestValues = { -1.0f, -1.0f, -1.0f };
		NaturalResourceType[] bestTypes = new NaturalResourceType[3];
		for (int resource = 0; resource < WorldData.NaturalResourceCount; resource++)
		{
			float value = _worldData.ResourceAbundance[resource * size + cellIndex];
			for (int slot = 0; slot < bestValues.Length; slot++)
			{
				if (value <= bestValues[slot])
					continue;
				for (int move = bestValues.Length - 1; move > slot; move--)
				{
					bestValues[move] = bestValues[move - 1];
					bestTypes[move] = bestTypes[move - 1];
				}
				bestValues[slot] = value;
				bestTypes[slot] = (NaturalResourceType)resource;
				break;
			}
		}
		string[] formatted = new string[bestValues.Length];
		for (int slot = 0; slot < bestValues.Length; slot++)
		{
			int resourceIndex = _worldData.GetResourceIndex(bestTypes[slot], cellIndex);
			formatted[slot] = $"{bestTypes[slot]} {Mathf.Max(0.0f, bestValues[slot]):F0}/100 (access {_worldData.ResourceAccessibility[resourceIndex]:F0}/100)";
		}
		return string.Join("; ", formatted);
	}

	private string FormatMonthlyClimate(int cellIndex)
	{
		if (_worldData == null)
			return "Unavailable";

		string[] monthNames = { "Jan", "Feb", "Mar", "Apr", "May", "Jun",
			"Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
		string[] values = new string[WorldData.MonthsPerYear];

		for (int month = 0; month < WorldData.MonthsPerYear; month++)
		{
			int monthlyIndex = _worldData.GetMonthlyIndex(cellIndex, month);
			values[month] = $"{monthNames[month]} {_worldData.MonthlyTemperatureC[monthlyIndex]:F0}°C/{_worldData.MonthlyPrecipitationMm[monthlyIndex]:F0}mm";
		}

		return string.Join(", ", values);
	}

	private string GetTerrainName(TerrainType terrain)
	{
		return terrain switch
		{
			TerrainType.CoastalWater => "Coastal water",
			TerrainType.ShallowSea => "Shallow sea",
			TerrainType.ContinentalShelf => "Continental shelf / upper ocean",
			TerrainType.DeepOcean => "Deep ocean",
			TerrainType.CoastalLowland => "Coastal lowland",
			TerrainType.Lowland => "Lowland",
			TerrainType.CoastalPlain => "Coastal plain",
			TerrainType.Plains => "Plains",
			TerrainType.RollingHills => "Rolling hills",
			TerrainType.RuggedHills => "Rugged hills",
			TerrainType.Highlands => "Highlands",
			TerrainType.Mountains => "Mountains",
			TerrainType.HighMountains => "High mountains",
			TerrainType.AlpinePeaks => "Alpine peaks",
			_ => "Unknown"
		};
	}

	private string ClassifySlope(float slope)
	{
		if (slope < 0.05f)
			return "Effectively flat";
		if (slope < 0.10f)
			return "Very gentle";
		if (slope < 0.50f)
			return "Gentle";
		if (slope < 2.0f)
			return "Moderate";
		if (slope < 5.0f)
			return "Noticeable";
		if (slope < 15.0f)
			return "Steep";
		if (slope < 30.0f)
			return "Very steep";
		return "Extreme";
	}

	private string ClassifyArmyMovement(float multiplier)
	{
		if (multiplier <= 1.10f)
			return "Easy";
		if (multiplier <= 1.50f)
			return "Light resistance";
		if (multiplier <= 2.20f)
			return "Moderate resistance";
		if (multiplier <= 3.50f)
			return "Difficult";
		if (multiplier <= 5.50f)
			return "Very difficult";
		return "Severe terrain";
	}

	private string ClassifyRegionalFloodSusceptibility(float score)
	{
		if (score < 10.0f)
			return "Very low";
		if (score < 20.0f)
			return "Low";
		if (score < 35.0f)
			return "Moderate";
		if (score < 50.0f)
			return "Elevated";
		if (score < 65.0f)
			return "High";
		return "Very high";
	}

	private string ClassifyConstructionSuitability(float score)
	{
		if (score >= 80.0f)
			return "High";
		if (score >= 60.0f)
			return "Good";
		if (score >= 40.0f)
			return "Moderate";
		if (score >= 20.0f)
			return "Low";
		return "Very low";
	}

	private string FormatAspect(float aspect, float slope)
	{
		if (aspect < 0.0f || slope < 0.05f)
			return "Flat / no reliable direction";

		int sector = Mathf.FloorToInt((aspect + 22.5f) / 45.0f) % 8;
		return $"{AspectDirections[sector]} ({aspect:F0}°)";
	}

	private void UpdateSelectionMarker(Vector2 spriteLocalPosition)
	{
		if (_selectionOverlay == null || _worldImage == null)
			return;

		Vector2 markerPosition = ToLocal(_worldImage.ToGlobal(spriteLocalPosition));
		_selectionOverlay.Position = markerPosition;
		UpdateSelectionOverlayScale();
		_selectionOverlay.Visible = true;
	}
}

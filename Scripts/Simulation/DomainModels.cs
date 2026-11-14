#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BronzeAge
{
    public enum LandUse { Wilderness, Cropland, Pasture, Forest, Wetland, Settlement, Mine, Quarry, SacredSite }
    public enum SettlementKind { Hamlet, Village, Town, City, CeremonialCenter }
    public enum Occupation { Farmers, Herders, Fishers, Gatherers, Miners, Artisans, Merchants, Priests, Administrators, Soldiers, Laborers, Scholars }
    public enum SocialClass { Subsistence, Commoner, Specialist, Elite, Ruling }
    public enum GameResource { Grain, Livestock, Fish, Timber, Stone, Clay, Salt, CopperOre, TinOre, IronOre, Gold, Silver, Obsidian, Coal, Oil, NaturalGas, FertileLand, Wool, Linen, Pottery, Tools, Weapons, Bronze, Fuel, FreshWater }
    public enum GovernmentType { Clan, Tribe, Confederation, Chiefdom, CityState, Kingdom, Empire }
    public enum ReligionType { Animism, AncestorVeneration, Polytheism, SolarCult, RiverCult, LocalTradition }
    public enum EventKind { Drought, Flood, Locusts, Epidemic, Famine, Discovery, Migration, Rebellion, Succession, TradeDisruption, War, CulturalExchange }
    public enum TreatyType { Trade, NonAggression, Alliance, Tribute, Vassalage, Access }

    public sealed class CellState
    {
        public int Index { get; set; }
        public float ArablePotentialKm2 { get; set; }
        public float AnnualRainfallMm { get; set; }
        public float MeanTemperatureC { get; set; }
        public float AridityIndex { get; set; }
        public float SoilFertility { get; set; }
        public float FreshwaterAccess { get; set; }
        public float FloodSusceptibility { get; set; }
        public float AgriculturalSuitability { get; set; }
        public float SettlementSuitability { get; set; }
        public float InfantryMovementCost { get; set; } = 1;
        public float CavalryMovementCost { get; set; } = 1;
        public float VehicleMovementCost { get; set; } = 1;
        public bool IsRiver { get; set; }
        public bool IsCoastal { get; set; }
        public double IrrigationCapacity { get; set; }
        public float CultivatedKm2 { get; set; }
        public float PastureKm2 { get; set; }
        public float ForestUseKm2 { get; set; }
        public float BuiltUpKm2 { get; set; }
        public float RuralPopulation { get; set; }
        public float CarryingCapacity { get; set; }
        public float Urbanization { get; set; }
        public float FoodReserve { get; set; }
        public float WaterReserve { get; set; }
        public int OwnerId { get; set; } = -1;
        public int ControllerId { get; set; } = -1;
        public int DominantCultureId { get; set; } = -1;
        public int DominantReligionId { get; set; } = -1;
        public LandUse PrimaryLandUse { get; set; }
        public List<int> SettlementIds { get; set; } = new();
        public Dictionary<GameResource, double> Stockpiles { get; set; } = new();
        public Dictionary<GameResource, double> DepositPotential { get; set; } = new();
    }

    public sealed class PopulationCohort
    {
        public int Id { get; set; }
        public int SettlementId { get; set; } = -1;
        public int CellIndex { get; set; }
        public double People { get; set; }
        public Occupation Occupation { get; set; }
        public SocialClass Class { get; set; }
        public int CultureId { get; set; } = -1;
        public int ReligionId { get; set; } = -1;
        public double Wealth { get; set; }
        public double Health { get; set; } = 0.8;
        public double LiteracyOrSpecialistKnowledge { get; set; }
        public double Morale { get; set; } = 0.65;
        public double FoodNeedPerPersonMonth { get; set; } = 0.035;
        public double WaterNeedPerPersonMonth { get; set; } = 0.09;
        public double ClothingNeedPerPersonMonth { get; set; } = 0.01;
        public double FuelNeedPerPersonMonth { get; set; } = 0.015;
        public double BirthRateAnnual { get; set; } = 0.025;
        public double MortalityRateAnnual { get; set; } = 0.018;
        public double FoodSatisfaction { get; set; } = 1.0;
        public double WaterSatisfaction { get; set; } = 1.0;
    }

    public sealed class Settlement
    {
        public int Id { get; set; }
        public string Name { get; set; } = "Settlement";
        public int CellIndex { get; set; }
        public int PolityId { get; set; } = -1;
        public SettlementKind Kind { get; set; }
        public double Population { get; set; }
        public double FoodStore { get; set; }
        public double WaterStore { get; set; }
        public double Prosperity { get; set; } = 0.5;
        public double Stability { get; set; } = 0.7;
        public double Infrastructure { get; set; } = 0.1;
        public Dictionary<CropType, double> CropAreaKm2 { get; set; } = new();
        public Dictionary<CropType, double> CropYieldMultiplier { get; set; } = new();
        public double Irrigation { get; set; }
        public bool HasMarket { get; set; }
        public bool HasTemple { get; set; }
        public bool HasWorkshop { get; set; }
        public Dictionary<GameResource, double> Inventory { get; set; } = new();
        public List<int> CohortIds { get; set; } = new();
    }

    public sealed class Culture
    {
        public int Id { get; set; }
        public string Name { get; set; } = "Culture";
        public int LanguageFamilyId { get; set; }
        public double Prestige { get; set; } = 0.2;
        public double Diffusion { get; set; } = 0.2;
        public HashSet<int> Traits { get; set; } = new();
    }

    public sealed class Religion
    {
        public int Id { get; set; }
        public string Name { get; set; } = "Local tradition";
        public ReligionType Type { get; set; }
        public double Prestige { get; set; } = 0.2;
        public double InstitutionalStrength { get; set; } = 0.1;
    }

    public sealed class Polity
    {
        public int Id { get; set; }
        public string Name { get; set; } = "Chiefdom";
        public GovernmentType Government { get; set; } = GovernmentType.Chiefdom;
        public int RulerId { get; set; } = -1;
        public int CultureId { get; set; } = -1;
        public int ReligionId { get; set; } = -1;
        public double Legitimacy { get; set; } = 0.65;
        public double Authority { get; set; } = 0.5;
        public double Treasury { get; set; }
        public double Stability { get; set; } = 0.7;
        public double MilitaryPower { get; set; }
        public double Aggression { get; set; } = 0.35;
        public double Expansionism { get; set; } = 0.3;
        public List<int> SettlementIds { get; set; } = new();
        public Dictionary<int, double> Relations { get; set; } = new();
        public List<int> Claims { get; set; } = new();
        public List<Technology> Technologies { get; set; } = new();
        public double TaxRate { get; set; } = 0.05;
        public double TributeDemand { get; set; }
    }

    public sealed class Ruler
    {
        public int Id { get; set; }
        public int PolityId { get; set; }
        public string Name { get; set; } = "Ruler";
        public double Diplomacy { get; set; } = 0.5;
        public double Ambition { get; set; } = 0.5;
        public double Caution { get; set; } = 0.5;
        public double Generosity { get; set; } = 0.5;
        public double StrategicSkill { get; set; } = 0.5;
        public int Age { get; set; } = 35;
        public int HeirId { get; set; } = -1;
    }

    public sealed class Treaty
    {
        public int Id { get; set; }
        public int PolityA { get; set; }
        public int PolityB { get; set; }
        public TreatyType Type { get; set; }
        public int MonthsRemaining { get; set; } = 12;
        public double TributePerMonth { get; set; }
    }

    public sealed class TradeRoute
    {
        public int Id { get; set; }
        public int FromSettlementId { get; set; }
        public int ToSettlementId { get; set; }
        public List<int> CellPath { get; set; } = new();
        public double CapacityPerMonth { get; set; } = 100;
        public double Risk { get; set; } = 0.1;
        public bool Active { get; set; } = true;
    }

    public sealed class Army
    {
        public int Id { get; set; }
        public int PolityId { get; set; }
        public int CellIndex { get; set; }
        public int TargetCellIndex { get; set; } = -1;
        public double Soldiers { get; set; }
        public double Morale { get; set; } = 0.8;
        public double Supply { get; set; } = 1.0;
        public double Training { get; set; } = 0.2;
        public double Equipment { get; set; } = 0.2;
        public string UnitType { get; set; } = "Infantry";
    }

    public sealed class WorldEvent
    {
        public long Id { get; set; }
        public EventKind Kind { get; set; }
        public int CellIndex { get; set; } = -1;
        public int PolityId { get; set; } = -1;
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public int MonthsRemaining { get; set; }
        public double Severity { get; set; }
    }

    public enum CropType { EmmerWheat, Barley, Lentils, Peas, Flax, Dates, Millet, Rice, Grapes, Olives }
    public enum BuildingType { Granary, IrrigationCanal, Farmstead, Pasture, Workshop, Kiln, Smelter, Temple, Wall, Market, Dock, Warehouse, RoadStation }
    public sealed class Technology
    {
        public string Key { get; set; } = "";
        public double Progress { get; set; }
        public bool Unlocked { get; set; }
    }
    public sealed class DiplomaticMessage
    {
        public int FromPolityId { get; set; }
        public int ToPolityId { get; set; }
        public string Subject { get; set; } = "";
        public string Text { get; set; } = "";
        public long DaySent { get; set; }
        public bool Read { get; set; }
    }
    public sealed class Building
    {
        public int Id { get; set; }
        public int SettlementId { get; set; }
        public BuildingType Type { get; set; }
        public int Level { get; set; } = 1;
        public double Condition { get; set; } = 1;
    }

    public sealed class WorldState
    {
        public int Width { get; }
        public int Height { get; }
        public int Seed { get; }
        public long Day { get; set; }
        public int Year { get; set; } = -3000;
        public int StartYear { get; set; } = -3000;
        public int Month { get; set; } = 1;
        public bool Paused { get; set; }
        public double Treasury { get; set; }
        public List<CellState> Cells { get; set; }
        public List<Settlement> Settlements { get; set; } = new();
        public List<PopulationCohort> Cohorts { get; set; } = new();
        public List<Culture> Cultures { get; set; } = new();
        public List<Religion> Religions { get; set; } = new();
        public List<Polity> Polities { get; set; } = new();
        public List<Ruler> Rulers { get; set; } = new();
        public List<Treaty> Treaties { get; set; } = new();
        public List<TradeRoute> TradeRoutes { get; set; } = new();
        public List<Army> Armies { get; set; } = new();
        public List<WorldEvent> Events { get; set; } = new();
        public List<Building> Buildings { get; set; } = new();
        public List<DiplomaticMessage> Messages { get; set; } = new();
        public long NextEventId { get; set; } = 1;
        [JsonIgnore] public Random Random { get; private set; }

        public WorldState(int width, int height, int seed)
        {
            Width = width; Height = height; Seed = seed; StartYear = Year;
            Cells = Enumerable.Range(0, checked(width * height)).Select(i => new CellState { Index = i }).ToList();
            Random = new Random(seed);
        }
        public void RecreateRandom() => Random = new Random(unchecked(Seed * 397 ^ (int)Day));
        public int Index(int x, int y) => ((Math.Clamp(y, 0, Height - 1) * Width) + ((x % Width + Width) % Width));
    }
}

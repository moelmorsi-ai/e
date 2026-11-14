#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace BronzeAge
{
    public static class WorldBootstrap
    {
        public static WorldState Create(WorldData geo, int startYear = -3000)
        {
            var state = new WorldState(geo.Width, geo.Height, geo.Seed) { Year = startYear, StartYear = startYear };
            int n = geo.Width * geo.Height;
            for (int i = 0; i < n; i++)
            {
                if (!geo.IsLand[i] || geo.IsLake[i]) continue;
                double area = EstimateCellAreaKm2(geo, i);
                var c = state.Cells[i];
                c.ArablePotentialKm2 = area * Math.Clamp(geo.AgriculturalSuitability[i] / 100.0 * 0.62, 0, 0.85);
                c.AnnualRainfallMm = geo.AnnualPrecipitationMm[i];
                c.MeanTemperatureC = geo.MeanAnnualTemperatureC[i];
                c.AridityIndex = geo.AridityIndex[i];
                c.SoilFertility = geo.SoilFertility[i];
                c.FreshwaterAccess = geo.FreshwaterAccess[i];
                c.FloodSusceptibility = geo.RegionalFloodSusceptibility[i];
                c.AgriculturalSuitability = geo.AgriculturalSuitability[i];
                c.SettlementSuitability = geo.SettlementSuitability[i];
                c.InfantryMovementCost = geo.InfantryMovementCostMultiplier[i];
                c.CavalryMovementCost = geo.CavalryMovementCostMultiplier[i];
                c.VehicleMovementCost = geo.VehicleMovementCostMultiplier[i];
                c.IsRiver = geo.IsRiver[i];
                c.IsCoastal = geo.IsCoastal[i];
                for (int r = 0; r < WorldData.NaturalResourceCount; r++)
                {
                    var resource = (NaturalResourceType)r;
                    var gameResource = resource switch
                    {
                        NaturalResourceType.Iron => GameResource.IronOre,
                        NaturalResourceType.Copper => GameResource.CopperOre,
                        NaturalResourceType.Tin => GameResource.TinOre,
                        NaturalResourceType.Gold => GameResource.Gold,
                        NaturalResourceType.Coal => GameResource.Coal,
                        NaturalResourceType.Oil => GameResource.Oil,
                        NaturalResourceType.NaturalGas => GameResource.NaturalGas,
                        NaturalResourceType.Stone => GameResource.Stone,
                        NaturalResourceType.Timber => GameResource.Timber,
                        NaturalResourceType.Freshwater => GameResource.FreshWater,
                        NaturalResourceType.FertileLand => GameResource.FertileLand,
                        _ => (GameResource?)null
                    };
                    if (gameResource.HasValue)
                        c.DepositPotential[gameResource.Value] = geo.ResourceAbundance[r * n + i];
                }
                c.CarryingCapacity = c.ArablePotentialKm2 * 18.0 + geo.FreshwaterAccess[i] * 4.0;
                c.PrimaryLandUse = geo.ForestDensity[i] > 60 ? LandUse.Forest :
                    geo.AgriculturalSuitability[i] > 35 ? LandUse.Wilderness : LandUse.Wilderness;
                c.WaterReserve = Math.Max(0, geo.WaterAvailabilityMm[i]) * area * 0.001;
            }
            return state;
        }

        public static WorldState CreateGame(WorldData geo, int civilizationCount = 8, int startYear = -3000)
        {
            var state = Create(geo, startYear);
            var religion = new Religion { Id = 0, Name = "Ancestor and Nature Traditions", Type = ReligionType.Animism, Prestige = 0.4 };
            var riverReligion = new Religion { Id = 1, Name = "River and Fertility Cults", Type = ReligionType.RiverCult, Prestige = 0.35 };
            state.Religions.Add(religion);
            state.Religions.Add(riverReligion);
            var candidates = Enumerable.Range(0, state.Cells.Count)
                .Where(i => geo.IsLand[i] && !geo.IsLake[i] && geo.SettlementSuitability[i] >= 34)
                .OrderByDescending(i => geo.SettlementSuitability[i] + geo.FreshwaterAccess[i] * 0.25 + geo.AgriculturalSuitability[i] * 0.2)
                .ToList();
            var chosen = new List<int>();
            int separation = Math.Max(4, geo.Width / 18);
            foreach (int candidate in candidates)
            {
                int x = candidate % geo.Width, y = candidate / geo.Width;
                bool near = chosen.Any(other =>
                {
                    int ox = other % geo.Width, oy = other / geo.Width;
                    int dx = Math.Abs(x - ox); dx = Math.Min(dx, geo.Width - dx);
                    return dx * dx + (y - oy) * (y - oy) < separation * separation;
                });
                if (near) continue;
                chosen.Add(candidate);
                if (chosen.Count >= Math.Max(1, civilizationCount)) break;
            }

            for (int i = 0; i < chosen.Count; i++)
            {
                int cell = chosen[i];
                int cultureId = state.Cultures.Count;
                state.Cultures.Add(new Culture { Id = cultureId, Name = $"Culture {i + 1}", LanguageFamilyId = i / 2,
                    Prestige = 0.25 + state.Random.NextDouble() * 0.35, Diffusion = 0.15 + state.Random.NextDouble() * 0.3 });
                int religionId = i % 3 == 0 ? 1 : 0;
                var polity = new Polity { Id = i, Name = $"Early Kingdom {i + 1}", Government = GovernmentType.Chiefdom,
                    CultureId = cultureId, ReligionId = religionId, Treasury = 250, MilitaryPower = 100, Aggression = 0.2 + state.Random.NextDouble() * 0.5 };
                state.Polities.Add(polity);
                var ruler = new Ruler { Id = i, PolityId = i, Name = $"Ruler {i + 1}",
                    Diplomacy = state.Random.NextDouble(), Ambition = state.Random.NextDouble(),
                    Caution = state.Random.NextDouble(), Generosity = state.Random.NextDouble(),
                    StrategicSkill = state.Random.NextDouble() };
                state.Rulers.Add(ruler);
                var settlement = SettlementSystem.Found(state, cell, $"Settlement {i + 1}",
                    geo.SettlementSuitability[cell] > 72 ? SettlementKind.Town : SettlementKind.Village, i,
                    700 + state.Random.Next(0, 1800));
                var c = state.Cells[cell];
                c.OwnerId = i; c.ControllerId = i; c.DominantCultureId = cultureId; c.DominantReligionId = religionId;
                c.CultivatedKm2 = Math.Min(c.ArablePotentialKm2, Math.Max(5, settlement.Population / 80));
                c.PrimaryLandUse = LandUse.Settlement;
                settlement.FoodStore += settlement.Population * 0.3;
                settlement.WaterStore += settlement.Population * 0.4;
                PopulationSystem.AddCohort(state, cell, settlement.Id, Occupation.Farmers, settlement.Population * 0.62, cultureId, religionId);
                PopulationSystem.AddCohort(state, cell, settlement.Id, Occupation.Herders, settlement.Population * 0.12, cultureId, religionId);
                PopulationSystem.AddCohort(state, cell, settlement.Id, Occupation.Artisans, settlement.Population * 0.10, cultureId, religionId);
                PopulationSystem.AddCohort(state, cell, settlement.Id, Occupation.Administrators, settlement.Population * 0.04, cultureId, religionId);
                PopulationSystem.AddCohort(state, cell, settlement.Id, Occupation.Soldiers, settlement.Population * 0.08, cultureId, religionId);
                PopulationSystem.AddCohort(state, cell, settlement.Id, Occupation.Priests, settlement.Population * 0.04, cultureId, religionId);
            }
            return state;
        }

        private static double EstimateCellAreaKm2(WorldData geo, int index)
        {
            int y = index / geo.Width;
            double lat = Math.PI * (0.5 - (y + 0.5) / geo.Height);
            double dLat = Math.PI / geo.Height;
            double dLon = 2 * Math.PI / geo.Width;
            const double radiusKm = 6371.0;
            return radiusKm * radiusKm * dLon * (Math.Sin(lat + dLat / 2) - Math.Sin(lat - dLat / 2));
        }
    }

    public sealed class SimulationClock
    {
        public double DaysPerSecond { get; set; } = 1;
        public double Accumulator { get; private set; }
        public void Advance(WorldState state, double realSeconds, Action dailyTick)
        {
            if (state.Paused || realSeconds <= 0) return;
            Accumulator += realSeconds * DaysPerSecond;
            while (Accumulator >= 1.0)
            {
                Accumulator -= 1.0;
                state.Day++;
                dailyTick();
                MilitarySystem.DailyUpdate(state);
                if (state.Day % 30 == 0) SimulationScheduler.MonthlyTick(state);
                if (state.Day % 360 == 0) SimulationScheduler.AnnualTick(state);
                int month = (int)((state.Day % 360) / 30) + 1;
                state.Month = Math.Clamp(month, 1, 12);
                state.Year = state.StartYear + (int)(state.Day / 360);
            }
        }
    }

    public static class SimulationScheduler
    {
        public static void MonthlyTick(WorldState s)
        {
            PopulationSystem.MonthlyUpdate(s);
            AgricultureSystem.MonthlyUpdate(s);
            ProductionSystem.MonthlyUpdate(s);
            EconomySystem.MonthlyUpdate(s);
            TradeSystem.MonthlyUpdate(s);
            DiplomacySystem.MonthlyUpdate(s);
            PoliticalSystem.MonthlyUpdate(s);
            MilitarySystem.MonthlyUpdate(s);
            SettlementDevelopmentSystem.MonthlyUpdate(s);
            CultureSystem.MonthlyUpdate(s);
            EventSystem.MonthlyUpdate(s);
            RulerAiSystem.MonthlyUpdate(s);
        }
        public static void AnnualTick(WorldState s)
        {
            PopulationSystem.AnnualUpdate(s);
            TechnologySystem.AnnualUpdate(s);
            PoliticalSystem.AnnualUpdate(s);
            CultureSystem.AnnualUpdate(s);
        }
    }

    public static class SettlementSystem
    {
        public static Settlement Found(WorldState s, int cellIndex, string name, SettlementKind kind, int polityId = -1, double initialPopulation = 500)
        {
            if (cellIndex < 0 || cellIndex >= s.Cells.Count) throw new ArgumentOutOfRangeException(nameof(cellIndex));
            var cell = s.Cells[cellIndex];
            if (cell.ArablePotentialKm2 <= 0 && cell.WaterReserve <= 0) throw new InvalidOperationException("Cell has no modeled subsistence potential.");
            var settlement = new Settlement { Id = s.Settlements.Count, Name = name, CellIndex = cellIndex, Kind = kind, PolityId = polityId, Population = initialPopulation, FoodStore = initialPopulation * 0.12, WaterStore = initialPopulation * 0.2, HasMarket = (int)kind >= (int)SettlementKind.Town };
            s.Settlements.Add(settlement);
            cell.SettlementIds.Add(settlement.Id);
            cell.BuiltUpKm2 += Math.Max(0.05, initialPopulation / 10000.0);
            cell.Urbanization = Math.Clamp(cell.BuiltUpKm2 / Math.Max(0.1, cell.ArablePotentialKm2 + cell.BuiltUpKm2), 0, 1);
            cell.PrimaryLandUse = LandUse.Settlement;
            if (polityId >= 0 && polityId < s.Polities.Count) s.Polities[polityId].SettlementIds.Add(settlement.Id);
            return settlement;
        }

        public static void RecalculatePopulation(WorldState s)
        {
            foreach (var settlement in s.Settlements)
                settlement.Population = s.Cohorts.Where(c => c.SettlementId == settlement.Id).Sum(c => c.People);
            foreach (var cell in s.Cells)
                cell.RuralPopulation = s.Cohorts.Where(c => c.SettlementId < 0 && c.CellIndex == cell.Index).Sum(c => c.People);
        }
    }

    public static class PopulationSystem
    {
        public static PopulationCohort AddCohort(WorldState s, int cell, int settlement, Occupation job, double people, int culture = -1, int religion = -1)
        {
            var cohort = new PopulationCohort { Id = s.Cohorts.Count, CellIndex = cell, SettlementId = settlement, Occupation = job, People = Math.Max(0, people), CultureId = culture, ReligionId = religion };
            s.Cohorts.Add(cohort);
            if (settlement >= 0 && settlement < s.Settlements.Count) s.Settlements[settlement].CohortIds.Add(cohort.Id);
            return cohort;
        }

        public static void MonthlyUpdate(WorldState s)
        {
            foreach (var cohort in s.Cohorts)
            {
                var cell = s.Cells[cohort.CellIndex];
                double food = cohort.SettlementId >= 0 ? s.Settlements[cohort.SettlementId].FoodStore : cell.FoodReserve;
                double water = cohort.SettlementId >= 0 ? s.Settlements[cohort.SettlementId].WaterStore : cell.WaterReserve;
                double foodNeed = cohort.People * cohort.FoodNeedPerPersonMonth;
                double waterNeed = cohort.People * cohort.WaterNeedPerPersonMonth;
                double foodRatio = foodNeed <= 0 ? 1 : Math.Clamp(food / foodNeed, 0, 1);
                double waterRatio = waterNeed <= 0 ? 1 : Math.Clamp(water / waterNeed, 0, 1);
                cohort.FoodSatisfaction = foodRatio;
                cohort.WaterSatisfaction = waterRatio;
                cohort.Health = Math.Clamp(cohort.Health + (Math.Min(foodRatio, waterRatio) - 0.75) * 0.035, 0.05, 1);
                cohort.Morale = Math.Clamp(cohort.Morale + (Math.Min(foodRatio, waterRatio) - 0.65) * 0.025, 0, 1);
                if (cohort.SettlementId >= 0)
                {
                    var st = s.Settlements[cohort.SettlementId];
                    st.FoodStore = Math.Max(0, st.FoodStore - foodNeed);
                    st.WaterStore = Math.Max(0, st.WaterStore - waterNeed);
                }
                else
                {
                    cell.FoodReserve = Math.Max(0, cell.FoodReserve - foodNeed);
                    cell.WaterReserve = Math.Max(0, cell.WaterReserve - waterNeed);
                }
            }
        }

        public static void AnnualUpdate(WorldState s)
        {
            foreach (var c in s.Cohorts)
            {
                double stress = 1.0 - Math.Min(c.FoodSatisfaction, c.WaterSatisfaction);
                double birth = c.People * c.BirthRateAnnual * Math.Clamp(c.Health + 0.15, 0.1, 1.1);
                double deaths = c.People * (c.MortalityRateAnnual + stress * 0.12 + (1 - c.Health) * 0.035);
                c.People = Math.Max(0, c.People + birth - deaths);
            }
            s.Cohorts.RemoveAll(c => c.People < 0.5);
            foreach (var cohort in s.Cohorts.Where(c => c.FoodSatisfaction < 0.25).ToArray())
            {
                int x = cohort.CellIndex % s.Width, y = cohort.CellIndex / s.Width;
                int[] candidates = { s.Index(x - 1, y), s.Index(x + 1, y), y > 0 ? s.Index(x, y - 1) : cohort.CellIndex, y < s.Height - 1 ? s.Index(x, y + 1) : cohort.CellIndex };
                int best = candidates.Where(i => i != cohort.CellIndex).OrderByDescending(i => s.Cells[i].FoodReserve + s.Cells[i].WaterReserve + s.Cells[i].AgriculturalSuitability).FirstOrDefault(cohort.CellIndex);
                if (best != cohort.CellIndex) Migrate(cohort, s, best, -1, 0.1);
            }
            SettlementSystem.RecalculatePopulation(s);
        }

        public static bool Migrate(PopulationCohort cohort, WorldState s, int destinationCell, int destinationSettlement = -1, double fraction = 1)
        {
            if (destinationCell < 0 || destinationCell >= s.Cells.Count || destinationCell == cohort.CellIndex) return false;
            fraction = Math.Clamp(fraction, 0.01, 1);
            double people = cohort.People * fraction;
            cohort.People -= people;
            AddCohort(s, destinationCell, destinationSettlement, cohort.Occupation, people, cohort.CultureId, cohort.ReligionId);
            return true;
        }
    }

    public static class AgricultureSystem
    {
        public static void MonthlyUpdate(WorldState s)
        {
            foreach (var cell in s.Cells)
            {
                if (cell.CultivatedKm2 <= 0) continue;
                double season = s.Month is >= 4 and <= 9 ? 1.0 : 0.18;
                double moisture = Math.Clamp(cell.AnnualRainfallMm / 900.0, 0.12, 1.25);
                double thermal = Math.Clamp(1.0 - Math.Abs(cell.MeanTemperatureC - 21.0) / 38.0, 0.1, 1.0);
                double fertility = Math.Clamp(cell.SoilFertility / 70.0, 0.15, 1.25);
                double yield = cell.CultivatedKm2 * season * 0.12 * moisture * thermal * fertility;
                var localSettlements = s.Settlements.Where(x => x.CellIndex == cell.Index).ToArray();
                if (localSettlements.Length == 0) cell.FoodReserve += yield;
                else
                {
                    double share = yield / localSettlements.Length;
                    foreach (var settlement in localSettlements)
                    {
                        settlement.FoodStore += share;
                        settlement.Inventory[GameResource.Grain] = settlement.Inventory.GetValueOrDefault(GameResource.Grain) + share;
                    }
                }
                // Strategic monthly recharge proxy; the climate layer remains the source
                // of long-run rainfall while this is the dynamic local storage pool.
                cell.WaterReserve += cell.AnnualRainfallMm / 12.0 * 0.001;
                if (cell.AridityIndex < 0.18 && cell.WaterReserve < cell.RuralPopulation * 0.05 && s.Random.NextDouble() < 0.02)
                    EventSystem.Add(s, EventKind.Drought, cell.Index, "Drought stress", "Water scarcity is reducing local agricultural output.", 0.45);
            }
        }
        public static void SetCultivation(WorldState s, int cellIndex, double areaKm2)
        {
            var c = s.Cells[cellIndex];
            c.CultivatedKm2 = Math.Clamp(areaKm2, 0, c.ArablePotentialKm2);
            if (c.CultivatedKm2 > 0) c.PrimaryLandUse = LandUse.Cropland;
        }
    }

    public static class ProductionSystem
    {
        public static readonly IReadOnlyDictionary<GameResource, double> BronzeRecipe = new Dictionary<GameResource, double>
        {
            [GameResource.CopperOre] = 0.88, [GameResource.TinOre] = 0.12, [GameResource.Fuel] = 0.25
        };
        public static double SmeltBronze(Settlement s, double oreInput)
        {
            if (oreInput <= 0) return 0;
            double batches = Math.Min(s.Inventory.GetValueOrDefault(GameResource.CopperOre) / (oreInput * 0.88),
                Math.Min(s.Inventory.GetValueOrDefault(GameResource.TinOre) / (oreInput * 0.12), s.Inventory.GetValueOrDefault(GameResource.Fuel) / (oreInput * 0.25)));
            double input = Math.Min(oreInput, batches);
            if (input <= 0) return 0;
            s.Inventory[GameResource.CopperOre] -= input * 0.88;
            s.Inventory[GameResource.TinOre] -= input * 0.12;
            s.Inventory[GameResource.Fuel] -= input * 0.25;
            s.Inventory[GameResource.Bronze] = s.Inventory.GetValueOrDefault(GameResource.Bronze) + input * 0.82;
            return input * 0.82;
        }
        public static void MonthlyUpdate(WorldState s)
        {
            foreach (var settlement in s.Settlements)
                settlement.Prosperity = Math.Clamp(settlement.Prosperity + (settlement.HasWorkshop ? 0.002 : 0) - (settlement.Stability < 0.3 ? 0.005 : 0), 0, 1);
        }
    }

    public static class EconomySystem
    {
        public static void MonthlyUpdate(WorldState s)
        {
            foreach (var p in s.Polities)
            {
                double taxable = p.SettlementIds.Where(i => i >= 0 && i < s.Settlements.Count).Sum(i => s.Settlements[i].Population * 0.002);
                p.Treasury += taxable + s.Treaties.Where(t => t.PolityB == p.Id && t.Type == TreatyType.Tribute).Sum(t => t.TributePerMonth);
                p.Stability = Math.Clamp(p.Stability + (p.Legitimacy - 0.5) * 0.01, 0, 1);
            }
        }
        public static double Price(double demand, double supply, double basePrice = 1)
            => Math.Clamp(basePrice * Math.Pow((demand + 1) / (supply + 1), 0.65), basePrice * 0.1, basePrice * 20);
    }

    public static class TradeSystem
    {
        public static TradeRoute? CreateRoute(WorldState s, int fromSettlementId, int toSettlementId, double capacity = 100, double risk = 0.1)
        {
            if (fromSettlementId < 0 || toSettlementId < 0 || fromSettlementId >= s.Settlements.Count || toSettlementId >= s.Settlements.Count) return null;
            var from = s.Settlements[fromSettlementId]; var to = s.Settlements[toSettlementId];
            var path = WorldPathfinder.FindPath(s, from.CellIndex, to.CellIndex);
            if (path.Count < 2) return null;
            var route = new TradeRoute { Id = s.TradeRoutes.Count, FromSettlementId = fromSettlementId, ToSettlementId = toSettlementId, CellPath = path, CapacityPerMonth = capacity, Risk = risk };
            s.TradeRoutes.Add(route);
            from.HasMarket = true; to.HasMarket = true;
            return route;
        }
        public static void MonthlyUpdate(WorldState s)
        {
            foreach (var route in s.TradeRoutes.Where(r => r.Active))
            {
                if (route.FromSettlementId >= s.Settlements.Count || route.ToSettlementId >= s.Settlements.Count) continue;
                var a = s.Settlements[route.FromSettlementId]; var b = s.Settlements[route.ToSettlementId];
                double capacity = route.CapacityPerMonth * (1 - Math.Clamp(route.Risk, 0, 0.95));
                foreach (var resource in a.Inventory.Keys.ToArray())
                {
                    double surplus = Math.Max(0, a.Inventory[resource] - a.Population * 0.01);
                    double amount = Math.Min(surplus, capacity);
                    if (amount <= 0) continue;
                    a.Inventory[resource] -= amount;
                    b.Inventory[resource] = b.Inventory.GetValueOrDefault(resource) + amount;
                    capacity -= amount;
                    if (capacity <= 0) break;
                }
            }
        }
    }

    public static class PoliticalSystem
    {
        public static void MonthlyUpdate(WorldState s)
        {
            foreach (var p in s.Polities)
            {
                p.Legitimacy = Math.Clamp(p.Legitimacy + (p.Stability - 0.5) * 0.004, 0, 1);
                if (p.Stability < 0.12 && p.SettlementIds.Count > 1)
                {
                    int breakaway = p.SettlementIds[^1];
                    p.SettlementIds.RemoveAt(p.SettlementIds.Count - 1);
                    if (breakaway < s.Settlements.Count) s.Settlements[breakaway].PolityId = -1;
                }
            }
        }
        public static void ExpandOrganicControl(WorldState s, int cellsPerPolity = 1)
        {
            foreach (var polity in s.Polities)
            {
                var frontier = s.Cells.Where(c => c.OwnerId == polity.Id).Select(c => c.Index).ToArray();
                foreach (int cellIndex in frontier)
                {
                    int x = cellIndex % s.Width, y = cellIndex / s.Width;
                    int[] adjacent = { s.Index(x - 1, y), s.Index(x + 1, y), y > 0 ? s.Index(x, y - 1) : cellIndex, y < s.Height - 1 ? s.Index(x, y + 1) : cellIndex };
                    foreach (int n in adjacent.Where(n => n != cellIndex && s.Cells[n].OwnerId < 0).Distinct().Take(Math.Max(0, cellsPerPolity)))
                    {
                        double pressure = polity.Authority * 0.35 + polity.Legitimacy * 0.25 + polity.Stability * 0.2 + polity.Expansionism * 0.2;
                        if (s.Random.NextDouble() < pressure * 0.12)
                        {
                            s.Cells[n].OwnerId = polity.Id;
                            s.Cells[n].ControllerId = polity.Id;
                            if (polity.CultureId >= 0) s.Cells[n].DominantCultureId = polity.CultureId;
                            if (polity.ReligionId >= 0) s.Cells[n].DominantReligionId = polity.ReligionId;
                        }
                    }
                }
            }
        }
        public static bool TransferSettlement(WorldState s, int settlementId, int newPolityId)
        {
            if (settlementId < 0 || settlementId >= s.Settlements.Count || newPolityId < 0 || newPolityId >= s.Polities.Count) return false;
            var st = s.Settlements[settlementId];
            if (st.PolityId >= 0 && st.PolityId < s.Polities.Count) s.Polities[st.PolityId].SettlementIds.Remove(settlementId);
            st.PolityId = newPolityId; s.Polities[newPolityId].SettlementIds.Add(settlementId);
            s.Cells[st.CellIndex].OwnerId = newPolityId; s.Cells[st.CellIndex].ControllerId = newPolityId;
            return true;
        }
        public static void AnnualUpdate(WorldState s)
        {
            ExpandOrganicControl(s, 1);
            foreach (var p in s.Polities)
                if (p.Government == GovernmentType.Chiefdom && p.SettlementIds.Count >= 4 && p.Treasury > 1000)
                    p.Government = GovernmentType.Kingdom;
        }
    }

    public static class CultureSystem
    {
        public static void MonthlyUpdate(WorldState s)
        {
            foreach (var cohort in s.Cohorts)
            {
                if (cohort.SettlementId < 0) continue;
                var st = s.Settlements[cohort.SettlementId];
                if (st.PolityId < 0 || st.PolityId >= s.Polities.Count) continue;
                var polity = s.Polities[st.PolityId];
                if (polity.CultureId >= 0 && cohort.CultureId != polity.CultureId && s.Cultures.Count > polity.CultureId)
                {
                    double pressure = s.Cultures[polity.CultureId].Prestige * 0.0005;
                    if (s.Random.NextDouble() < pressure) cohort.CultureId = polity.CultureId;
                }
            }
        }
        public static void AnnualUpdate(WorldState s)
        {
            foreach (var c in s.Cultures) c.Prestige = Math.Clamp(c.Prestige + 0.002 * s.Cohorts.Count(x => x.CultureId == c.Id) / Math.Max(1, s.Cohorts.Count), 0, 1);
            foreach (var r in s.Religions) r.InstitutionalStrength = Math.Clamp(r.InstitutionalStrength + 0.001, 0, 1);
        }
    }

    public static class TechnologySystem
    {
        public static readonly string[] BronzeAgeTechnologies = { "Copper Smelting", "Tin Bronze", "Pottery Kilns", "Irrigation", "Animal Husbandry", "Wheel Transport", "Sailing", "Writing", "Accounting", "Monumental Architecture", "Bronze Casting" };
        public static void AnnualUpdate(WorldState s)
        {
            foreach (var p in s.Polities)
            {
                // Knowledge grows from specialist cohorts and institutional capacity.
                double specialists = s.Cohorts.Where(c => c.SettlementId >= 0 && s.Settlements[c.SettlementId].PolityId == p.Id &&
                    (c.Occupation == Occupation.Artisans || c.Occupation == Occupation.Administrators || c.Occupation == Occupation.Scholars)).Sum(c => c.People);
                p.Authority = Math.Clamp(p.Authority + specialists / Math.Max(1, p.SettlementIds.Sum(id => s.Settlements[id].Population) + 1) * 0.01, 0, 1);
            }
        }
    }

    public static class DiplomacySystem
    {
        public static bool SendMessage(WorldState s, int from, int to, string subject, string text)
        {
            if (from < 0 || to < 0 || from >= s.Polities.Count || to >= s.Polities.Count || from == to) return false;
            s.Messages.Add(new DiplomaticMessage { FromPolityId = from, ToPolityId = to, Subject = subject, Text = text, DaySent = s.Day });
            return true;
        }
        public static bool ProposeTreaty(WorldState s, int a, int b, TreatyType type, int durationMonths = 12, double tribute = 0)
        {
            if (a == b || a < 0 || b < 0 || a >= s.Polities.Count || b >= s.Polities.Count) return false;
            var pa = s.Polities[a]; var pb = s.Polities[b];
            double relation = pa.Relations.GetValueOrDefault(b, 0);
            if ((type is TreatyType.Alliance or TreatyType.NonAggression) && relation < -0.4) return false;
            s.Treaties.Add(new Treaty { Id = s.Treaties.Count, PolityA = a, PolityB = b, Type = type, MonthsRemaining = durationMonths, TributePerMonth = tribute });
            pa.Relations[b] = Math.Clamp(relation + 0.15, -1, 1);
            pb.Relations[a] = Math.Clamp(pb.Relations.GetValueOrDefault(a, 0) + 0.15, -1, 1);
            return true;
        }
        public static void MonthlyUpdate(WorldState s)
        {
            foreach (var t in s.Treaties) t.MonthsRemaining--;
            s.Treaties.RemoveAll(t => t.MonthsRemaining <= 0);
        }
    }

    public static class RulerAiSystem
    {
        public static void MonthlyUpdate(WorldState s)
        {
            foreach (var ruler in s.Rulers)
            {
                if (ruler.PolityId < 0 || ruler.PolityId >= s.Polities.Count) continue;
                var p = s.Polities[ruler.PolityId];
                foreach (var other in s.Polities)
                {
                    if (other.Id == p.Id) continue;
                    double relation = p.Relations.GetValueOrDefault(other.Id, 0);
                    if (relation < -0.65 && p.MilitaryPower > other.MilitaryPower * (1.25 + ruler.Caution))
                        p.Claims.AddRange(other.SettlementIds.Where(id => !p.Claims.Contains(id)).Take(1));
                    else if (relation > 0.45 && ruler.Diplomacy > 0.55 && !s.Treaties.Any(t => t.PolityA == p.Id && t.PolityB == other.Id))
                    {
                        DiplomacySystem.ProposeTreaty(s, p.Id, other.Id, TreatyType.Trade);
                        DiplomacySystem.SendMessage(s, p.Id, other.Id, "Trade proposal", $"{ruler.Name} proposes stronger exchange and safer passage between our settlements.");
                    }
                }
            }
        }
    }

    public static class WorldPathfinder
    {
        public static List<int> FindPath(WorldState s, int start, int goal, string unit = "Infantry", int maxVisited = 50000)
        {
            if (start < 0 || goal < 0 || start >= s.Cells.Count || goal >= s.Cells.Count) return new();
            var open = new PriorityQueue<int, double>();
            var cameFrom = new Dictionary<int, int>();
            var cost = new Dictionary<int, double> { [start] = 0 };
            open.Enqueue(start, 0);
            int visited = 0;
            while (open.Count > 0 && visited++ < maxVisited)
            {
                int current = open.Dequeue();
                if (current == goal) return Reconstruct(cameFrom, current);
                foreach (int next in Neighbors(s, current))
                {
                    var c = s.Cells[next];
                    double move = unit switch { "Cavalry" => c.CavalryMovementCost, "Vehicle" => c.VehicleMovementCost, _ => c.InfantryMovementCost };
                    if (move <= 0) continue;
                    double candidate = cost[current] + move;
                    if (!cost.TryGetValue(next, out double old) || candidate < old)
                    {
                        cost[next] = candidate; cameFrom[next] = current;
                        open.Enqueue(next, candidate + Heuristic(s, next, goal));
                    }
                }
            }
            return new();
        }
        private static IEnumerable<int> Neighbors(WorldState s, int i)
        {
            int x = i % s.Width, y = i / s.Width;
            yield return s.Index(x - 1, y); yield return s.Index(x + 1, y);
            if (y > 0) yield return s.Index(x, y - 1);
            if (y < s.Height - 1) yield return s.Index(x, y + 1);
        }
        private static double Heuristic(WorldState s, int a, int b)
        {
            int ax = a % s.Width, ay = a / s.Width, bx = b % s.Width, by = b / s.Width;
            int dx = Math.Abs(ax - bx); dx = Math.Min(dx, s.Width - dx);
            return dx + Math.Abs(ay - by);
        }
        private static List<int> Reconstruct(Dictionary<int, int> previous, int current)
        {
            var path = new List<int> { current };
            while (previous.TryGetValue(current, out int p)) { current = p; path.Add(current); }
            path.Reverse(); return path;
        }
    }

    public static class MilitarySystem
    {
        public static bool OrderMove(WorldState s, Army army, int targetCell)
        {
            var path = WorldPathfinder.FindPath(s, army.CellIndex, targetCell, army.UnitType);
            if (path.Count < 2) return false;
            army.TargetCellIndex = targetCell;
            // Strategic path is represented as a deterministic target; movement is advanced per day.
            return true;
        }
        public static void DailyUpdate(WorldState s)
        {
            foreach (var army in s.Armies.Where(a => a.TargetCellIndex >= 0 && a.TargetCellIndex != a.CellIndex))
            {
                var path = WorldPathfinder.FindPath(s, army.CellIndex, army.TargetCellIndex, army.UnitType, 4000);
                if (path.Count > 1) army.CellIndex = path[1];
                if (army.CellIndex == army.TargetCellIndex) army.TargetCellIndex = -1;
            }
        }
        public static void MonthlyUpdate(WorldState s)
        {
            foreach (var a in s.Armies)
            {
                a.Supply = Math.Clamp(a.Supply - a.Soldiers * 0.000001, 0, 1);
                a.Morale = Math.Clamp(a.Morale + (a.Supply - 0.5) * 0.02, 0, 1);
            }
        }
        public static double CombatPower(Army a) => a.Soldiers * (0.5 + a.Morale * 0.25 + a.Training * 0.15 + a.Equipment * 0.1);
    }

    public static class EventSystem
    {
        public static WorldEvent Add(WorldState s, EventKind kind, int cell, string title, string description, double severity)
        {
            var e = new WorldEvent { Id = s.NextEventId++, Kind = kind, CellIndex = cell, Title = title, Description = description, Severity = Math.Clamp(severity, 0, 1), MonthsRemaining = 1 };
            s.Events.Add(e); return e;
        }
        public static void MonthlyUpdate(WorldState s)
        {
            foreach (var e in s.Events) e.MonthsRemaining--;
            s.Events.RemoveAll(e => e.MonthsRemaining <= 0);
        }
    }


    public static class ResourceExtractionSystem
    {
        public static double Extract(WorldState s, int cellIndex, GameResource resource, double requested, double efficiency = 1)
        {
            if (cellIndex < 0 || cellIndex >= s.Cells.Count || requested <= 0) return 0;
            var cell = s.Cells[cellIndex];
            double reserve = cell.DepositPotential.GetValueOrDefault(resource);
            double extracted = Math.Min(requested, reserve * Math.Clamp(efficiency, 0, 1));
            if (extracted <= 0) return 0;
            cell.DepositPotential[resource] = Math.Max(0, reserve - extracted * 0.0001);
            cell.Stockpiles[resource] = cell.Stockpiles.GetValueOrDefault(resource) + extracted;
            return extracted;
        }
    }

    public static class InfrastructureSystem
    {
        public static Building? Construct(WorldState s, int settlementId, BuildingType type, double materialCost)
        {
            if (settlementId < 0 || settlementId >= s.Settlements.Count) return null;
            var st = s.Settlements[settlementId];
            if (st.Inventory.GetValueOrDefault(GameResource.Tools) < materialCost) return null;
            st.Inventory[GameResource.Tools] -= materialCost;
            var building = new Building { Id = s.Buildings.Count, SettlementId = settlementId, Type = type };
            s.Buildings.Add(building);
            switch (type)
            {
                case BuildingType.Market: st.HasMarket = true; break;
                case BuildingType.Temple: st.HasTemple = true; break;
                case BuildingType.Workshop:
                case BuildingType.Kiln:
                case BuildingType.Smelter: st.HasWorkshop = true; break;
                case BuildingType.IrrigationCanal: st.Irrigation = Math.Clamp(st.Irrigation + 0.2, 0, 1); s.Cells[st.CellIndex].IrrigationCapacity = Math.Clamp(s.Cells[st.CellIndex].IrrigationCapacity + 0.2, 0, 1); break;
                case BuildingType.Granary:
                case BuildingType.Warehouse: st.FoodStore += st.Population * 0.05; break;
            }
            st.Infrastructure = Math.Clamp(st.Infrastructure + 0.04, 0, 1);
            return building;
        }
    }

    public static class CropSystem
    {
        public static double GetYieldMultiplier(CropType crop, CellState cell, int month)
        {
            double water = Math.Clamp(cell.AnnualRainfallMm / 700.0 + cell.IrrigationLevel(), 0.1, 1.5);
            double heat = crop switch
            {
                CropType.Dates or CropType.Millet => Math.Clamp((cell.MeanTemperatureC + 5) / 35, 0.1, 1.3),
                CropType.Barley or CropType.EmmerWheat => Math.Clamp(1.2 - Math.Abs(cell.MeanTemperatureC - 17) / 35, 0.1, 1.2),
                _ => Math.Clamp(1.1 - Math.Abs(cell.MeanTemperatureC - 20) / 35, 0.1, 1.2)
            };
            double season = month is >= 3 and <= 9 ? 1.0 : 0.15;
            return water * heat * Math.Clamp(cell.SoilFertility / 65.0, 0.15, 1.3) * season;
        }
        public static void Harvest(Settlement settlement, CellState cell, CropType crop, int month)
        {
            double area = settlement.CropAreaKm2.GetValueOrDefault(crop);
            if (area <= 0) return;
            double yield = area * GetYieldMultiplier(crop, cell, month) * 0.18;
            settlement.FoodStore += yield;
            settlement.Inventory[GameResource.Grain] = settlement.Inventory.GetValueOrDefault(GameResource.Grain) + yield;
        }
    }

    internal static class CellStateExtensions
    {
        public static double IrrigationLevel(this CellState cell) => Math.Clamp(cell.WaterReserve / Math.Max(1, cell.RuralPopulation + 1) * 0.01 + cell.IrrigationCapacity, 0, 0.8);
    }

    public static class MarketSystem
    {
        public static double GetLocalPrice(Settlement settlement, GameResource resource, double basePrice = 1)
        {
            double stock = settlement.Inventory.GetValueOrDefault(resource);
            double monthlyDemand = Math.Max(1, settlement.Population * 0.01);
            return EconomySystem.Price(monthlyDemand, stock, basePrice);
        }
        public static bool Buy(Settlement buyer, Settlement seller, GameResource resource, double amount, double unitPrice)
        {
            if (amount <= 0 || seller.Inventory.GetValueOrDefault(resource) < amount) return false;
            double cost = amount * unitPrice;
            if (buyer.Inventory.GetValueOrDefault(GameResource.Gold) < cost) return false;
            seller.Inventory[resource] -= amount;
            buyer.Inventory[resource] = buyer.Inventory.GetValueOrDefault(resource) + amount;
            buyer.Inventory[GameResource.Gold] -= cost;
            seller.Inventory[GameResource.Gold] = seller.Inventory.GetValueOrDefault(GameResource.Gold) + cost;
            return true;
        }
    }

    public static class WorldEventSimulation
    {
        public static void TriggerDrought(WorldState s, int cellIndex, double severity)
        {
            if (cellIndex < 0 || cellIndex >= s.Cells.Count) return;
            var cell = s.Cells[cellIndex];
            cell.WaterReserve *= 1 - Math.Clamp(severity, 0, 0.95);
            cell.FoodReserve *= 1 - Math.Clamp(severity * 0.65, 0, 0.9);
            EventSystem.Add(s, EventKind.Drought, cellIndex, "Drought", "Rainfall and available water have fallen below local needs.", severity);
        }
        public static void TriggerFlood(WorldState s, int cellIndex, double severity)
        {
            if (cellIndex < 0 || cellIndex >= s.Cells.Count) return;
            var cell = s.Cells[cellIndex];
            cell.CultivatedKm2 *= 1 - Math.Clamp(severity * 0.5, 0, 0.8);
            foreach (var st in s.Settlements.Where(x => x.CellIndex == cellIndex))
                st.Stability = Math.Clamp(st.Stability - severity * 0.2, 0, 1);
            EventSystem.Add(s, EventKind.Flood, cellIndex, "Flood", "Flooding has damaged local land use and settlement stability.", severity);
        }
        public static void TriggerEpidemic(WorldState s, int cellIndex, double severity)
        {
            foreach (var cohort in s.Cohorts.Where(c => c.CellIndex == cellIndex))
                cohort.Health = Math.Clamp(cohort.Health - severity * 0.35, 0.05, 1);
            EventSystem.Add(s, EventKind.Epidemic, cellIndex, "Epidemic", "Disease is reducing health across local population cohorts.", severity);
        }
    }

    public static class SettlementDevelopmentSystem
    {
        public static void MonthlyUpdate(WorldState s)
        {
            foreach (var st in s.Settlements)
            {
                double pop = st.Population;
                if (pop > 8000) st.Kind = SettlementKind.City;
                else if (pop > 2500) st.Kind = SettlementKind.Town;
                else if (pop > 500) st.Kind = SettlementKind.Village;
                else st.Kind = SettlementKind.Hamlet;
                st.Stability = Math.Clamp(st.Stability + (st.FoodStore > pop * 0.08 ? 0.004 : -0.012), 0, 1);
                st.Prosperity = Math.Clamp(st.Prosperity + (st.Inventory.Count > 4 ? 0.003 : -0.001), 0, 1);
            }
        }
    }

    public static class SaveManager
    {
        private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, IncludeFields = true };
        public static string Serialize(WorldState state) => JsonSerializer.Serialize(state, Options);
        public static WorldState Deserialize(string json)
        {
            var state = JsonSerializer.Deserialize<WorldState>(json, Options) ?? throw new InvalidOperationException("Save data is empty or invalid.");
            state.RecreateRandom();
            return state;
        }
    }
}

using System.Collections.Generic;
using System.Globalization;
using BigAmbitions.Items;
using Entities;

namespace ChaoticCEO
{
    public static class LiveDump
    {
        public static string Dump(string localAppData)
        {
            var current = SaveGameManager.Current;
            var line = StatementDump.Write(
                localAppData,
                current == null ? 0 : current.Day,
                Days(current));
            RouteDump.Write(localAppData, Plans(current), Holds(current));
            return line;
        }

        public static bool InCity()
        {
            return SaveGameManager.Current != null;
        }

        public static IReadOnlyList<OverlayRow> OverlayRows()
        {
            var current = SaveGameManager.Current;
            return Overlay.Build(Days(current), Portfolio(current), Holds(current));
        }

        public static CityBoard Board()
        {
            var current = SaveGameManager.Current;
            return Roadshow.Compose(
                Days(current),
                Plans(current),
                Holds(current),
                Asks(current),
                Portfolio(current),
                Shut(current));
        }

        public static IReadOnlyList<BuildingAsk> Asks(GameInstance? current)
        {
            return Roadshow.Prices(SaleAsks(current), EstateAsks(current));
        }

        public static string? Roll(DayWatch watch, string localAppData)
        {
            if (watch == null || string.IsNullOrEmpty(localAppData))
            {
                return null;
            }

            var current = SaveGameManager.Current;
            if (current == null || !watch.Due(current.Day))
            {
                return null;
            }

            return watch.Tick(localAppData, current.Day, Days(current), Plans(current), Holds(current));
        }

        public static IReadOnlyList<FinancialDay> Days(GameInstance? current)
        {
            if (current == null || current.financialSummaries == null)
            {
                return new FinancialDay[0];
            }

            var days = new List<FinancialDay>();
            foreach (var summary in current.financialSummaries)
            {
                days.Add(ToDay(summary));
            }

            return days;
        }

        private static FinancialDay ToDay(FinancialSummary summary)
        {
            if (summary == null)
            {
                return new FinancialDay(0, new StatementRow[0]);
            }

            var rows = new List<StatementRow>();
            if (summary.businessIncomeStatements != null)
            {
                foreach (var business in summary.businessIncomeStatements)
                {
                    if (business == null)
                    {
                        continue;
                    }

                    var address = business.Address;
                    var street = address == null || address.streetName == null ? "" : address.streetName;
                    var number = address == null ? 0 : address.streetNumber;
                    rows.Add(new StatementRow(
                        street,
                        number,
                        business.TotalSales,
                        business.TotalResources,
                        business.SalaryExpenses,
                        business.RentExpenses,
                        business.MarketingExpenses,
                        business.Theft,
                        business.TotalProfit));
                }
            }

            return new FinancialDay(summary.dayNumber, rows);
        }

        private static List<DeliveryStop> Plans(GameInstance? current)
        {
            var routes = new List<DeliveryStop>();
            if (current == null)
            {
                return routes;
            }

            if (current.DeliveryContracts != null)
            {
                foreach (var contract in current.DeliveryContracts)
                {
                    AddContract(routes, contract);
                }
            }

            if (current.logisticsManagerPlans != null)
            {
                foreach (var plan in current.logisticsManagerPlans)
                {
                    AddPlan(routes, plan);
                }
            }

            return routes;
        }

        private static void AddContract(List<DeliveryStop> routes, DeliveryContract contract)
        {
            if (contract == null || contract.items == null)
            {
                return;
            }

            var warehouse = Place(contract.wholesaleAddress);
            var destination = Place(contract.businessAddress);
            foreach (var item in contract.items)
            {
                if (item == null)
                {
                    continue;
                }

                routes.Add(new DeliveryStop(
                    warehouse,
                    destination,
                    item.itemName,
                    contract.nextDeliveryDay,
                    ""));
            }
        }

        private static void AddPlan(List<DeliveryStop> routes, Buildings.Office.Headquarters.LogisticsManagerPlan plan)
        {
            if (plan == null || plan.destinations == null)
            {
                return;
            }

            var warehouse = Place(plan.targetAddress);
            foreach (var destination in plan.destinations)
            {
                if (destination == null || destination.stockTargets == null)
                {
                    continue;
                }

                var label = Place(destination.deliveryTargetAddress);
                foreach (var target in destination.stockTargets)
                {
                    if (target == null)
                    {
                        continue;
                    }

                    routes.Add(new DeliveryStop(warehouse, label, target.itemName, null, ""));
                }
            }
        }

        public static IReadOnlyList<PortfolioBusiness> Shut(GameInstance? current)
        {
            var rows = new List<PortfolioBusiness>();
            if (current == null || current.BuildingRegistrations == null)
            {
                return rows;
            }

            var buildings = new List<(string Street, int Number, bool Named)>();
            foreach (var building in current.BuildingRegistrations)
            {
                if (building == null)
                {
                    continue;
                }

                buildings.Add((
                    building.StreetName ?? "",
                    building.StreetNumber,
                    !string.IsNullOrEmpty(building.BusinessName)));
            }

            return WithoutABusiness(buildings);
        }

        public static IReadOnlyList<PortfolioBusiness> WithoutABusiness(
            IReadOnlyList<(string Street, int Number, bool Named)>? buildings)
        {
            var rows = new List<PortfolioBusiness>();
            var named = new Dictionary<(string Street, int Number), bool>();
            if (buildings != null)
            {
                foreach (var building in buildings)
                {
                    if (string.IsNullOrEmpty(building.Street) || building.Number <= 0)
                    {
                        continue;
                    }

                    var key = (building.Street, building.Number);
                    if (!named.ContainsKey(key))
                    {
                        named[key] = building.Named;
                    }
                    else if (building.Named)
                    {
                        named[key] = true;
                    }
                }
            }

            foreach (var pair in named)
            {
                if (pair.Value)
                {
                    continue;
                }

                rows.Add(new PortfolioBusiness("", "", pair.Key.Street, pair.Key.Number));
            }

            return rows;
        }

        public static IReadOnlyList<PortfolioBusiness> Portfolio(GameInstance? current)
        {
            var rows = new List<PortfolioBusiness>();
            if (current == null || current.BuildingRegistrations == null)
            {
                return rows;
            }

            foreach (var building in current.BuildingRegistrations)
            {
                AddBusiness(rows, building);
            }

            return rows;
        }

        public static List<WarehouseHold> Holds(GameInstance? current)
        {
            var rows = new List<WarehouseHold>();
            if (current == null || current.BuildingRegistrations == null)
            {
                return rows;
            }

            foreach (var building in current.BuildingRegistrations)
            {
                AddWarehouse(rows, building);
            }

            return rows;
        }

        private static List<BuildingAsk> SaleAsks(GameInstance? current)
        {
            var rows = new List<BuildingAsk>();
            if (current == null || current.buildingsForSale == null)
            {
                return rows;
            }

            foreach (var sale in current.buildingsForSale)
            {
                if (sale == null || sale.address == null)
                {
                    continue;
                }

                rows.Add(new BuildingAsk(
                    sale.address.streetName ?? "",
                    sale.address.streetNumber,
                    sale.buildingPrice));
            }

            return rows;
        }

        private static List<BuildingAsk> EstateAsks(GameInstance? current)
        {
            var rows = new List<BuildingAsk>();
            if (current == null || current.realEstate == null)
            {
                return rows;
            }

            foreach (var estate in current.realEstate)
            {
                if (estate == null || estate.address == null)
                {
                    continue;
                }

                rows.Add(new BuildingAsk(
                    estate.address.streetName ?? "",
                    estate.address.streetNumber,
                    estate.purchasePrice));
            }

            return rows;
        }

        private static void AddBusiness(List<PortfolioBusiness> rows, BuildingRegistration building)
        {
            if (building == null)
            {
                return;
            }

            var family = StoreFamily.For(building.businessTypeName);
            if (family == null || string.IsNullOrEmpty(building.BusinessName) || string.IsNullOrEmpty(building.StreetName))
            {
                return;
            }

            rows.Add(new PortfolioBusiness(
                building.BusinessName,
                family,
                building.StreetName,
                building.StreetNumber));
        }

        private static void AddWarehouse(List<WarehouseHold> rows, BuildingRegistration building)
        {
            if (building == null || building.businessTypeName != "ba:businesstype_warehouse")
            {
                return;
            }

            if (string.IsNullOrEmpty(building.BusinessName) || string.IsNullOrEmpty(building.StreetName))
            {
                return;
            }

            if (building.itemInstances == null)
            {
                return;
            }

            var placed = new List<string>();
            var cargo = new List<string>();
            foreach (var item in building.itemInstances.Values)
            {
                CollectCargo(item, placed, cargo);
            }

            rows.AddRange(RouteDump.CountHolds(
                building.BusinessName,
                building.StreetName,
                building.StreetNumber,
                placed,
                cargo));
        }

        private static void CollectCargo(ItemInstance item, List<string> placed, List<string> cargo)
        {
            if (item == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(item.itemName))
            {
                placed.Add(item.itemName);
            }

            if (item.cargoInstances == null)
            {
                return;
            }

            foreach (var box in item.cargoInstances)
            {
                if (box == null)
                {
                    continue;
                }

                cargo.Add(box.itemName);
                if (box.nestedCargoInstances == null)
                {
                    continue;
                }

                foreach (var nested in box.nestedCargoInstances)
                {
                    if (nested == null)
                    {
                        continue;
                    }

                    cargo.Add(nested.itemName);
                }
            }
        }

        private static string Place(Address address)
        {
            if (address == null)
            {
                return "";
            }

            var street = address.streetName ?? "";
            return street + " " + address.streetNumber.ToString(CultureInfo.InvariantCulture);
        }
    }
}

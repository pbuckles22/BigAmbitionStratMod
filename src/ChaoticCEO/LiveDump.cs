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

        private static IReadOnlyList<FinancialDay> Days(GameInstance? current)
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

        private static List<WarehouseHold> Holds(GameInstance? current)
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

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Buildings.Office.Headquarters;
using Entities;

namespace ChaoticCEO
{
    public sealed class RedoButton
    {
        public RedoButton(string destination, string from, string to, string family, string call)
        {
            Destination = destination ?? "";
            From = from ?? "";
            To = to ?? "";
            Family = family ?? "";
            Call = call ?? "";
        }

        public string Label
        {
            get { return "Redo"; }
        }

        public string Destination { get; }

        public string From { get; }

        public string To { get; }

        public string Family { get; }

        public string Call { get; }
    }

    public static class RedoPress
    {
        public static IReadOnlyList<string> Controls(bool armed)
        {
            if (armed)
            {
                return new[] { "Confirm", "Cancel" };
            }

            return new[] { "Redo" };
        }

        public static IReadOnlyList<RedoButton> Buttons(ApiNote? api, CityBoard? board)
        {
            var buttons = new List<RedoButton>();
            var move = board == null ? null : board.Review.Move;
            if (api == null || api.Delivery.Length == 0 || move == null || move.Destination.Length == 0)
            {
                return buttons;
            }

            buttons.Add(new RedoButton(move.Destination, move.From, move.To, move.Family, api.Delivery));
            return buttons;
        }

        public static StoreDelivery Press(
            string localAppData,
            ApiNote? api,
            CityBoard? board,
            string? destination,
            bool confirmed,
            Action<string, string, string, string, string>? bridge = null)
        {
            var redone = Delivery.Confirm(
                api,
                board == null ? null : board.Review,
                destination,
                confirmed);
            if (!redone.Moved)
            {
                return redone;
            }

            if (bridge != null)
            {
                bridge(redone.Call, redone.Destination, redone.From, redone.To, redone.Family);
                Delivery.Record(localAppData ?? "", redone);
                return redone;
            }

            if (RedoBridge.Live(redone) || !RedoBridge.HasSave())
            {
                Delivery.Record(localAppData ?? "", redone);
                return redone;
            }

            RedoBridge.NoteMiss(localAppData ?? "", redone, "noplan");
            return redone;
        }
    }

    public static class RedoBridge
    {
        public static bool HasSave()
        {
            return SaveGameManager.Current != null;
        }

        public static void NoteMiss(string localAppData, StoreDelivery redone, string reason)
        {
            var path = LoadLog.CeoLogPath(localAppData ?? "");
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var line = "logistics.missed version=" + LoadLog.Version
                + " destination=" + redone.Destination
                + " from=" + redone.From
                + " to=" + redone.To
                + " family=" + redone.Family
                + " reason=" + (reason ?? "");
            File.AppendAllText(path, line + Environment.NewLine);
        }

        public static bool Live(StoreDelivery? redone)
        {
            if (redone == null || !redone.Moved)
            {
                return false;
            }

            var current = SaveGameManager.Current;
            if (current == null || current.logisticsManagerPlans == null)
            {
                return false;
            }

            Split(redone.Destination, out var street, out var number);
            if (street.Length == 0 || number <= 0)
            {
                return false;
            }

            var names = WarehouseNames(current);
            var plans = new List<LogisticsManagerPlan>();
            var warehouses = new List<string>();
            var stops = new List<IReadOnlyList<DeskStop>>();
            foreach (var plan in current.logisticsManagerPlans)
            {
                if (plan == null)
                {
                    continue;
                }

                plans.Add(plan);
                warehouses.Add(NameOf(names, plan.targetAddress));
                var rows = new List<DeskStop>();
                if (plan.destinations != null)
                {
                    foreach (var destination in plan.destinations)
                    {
                        if (destination == null || destination.deliveryTargetAddress == null)
                        {
                            rows.Add(new DeskStop("", 0));
                            continue;
                        }

                        rows.Add(new DeskStop(
                            destination.deliveryTargetAddress.streetName ?? "",
                            destination.deliveryTargetAddress.streetNumber));
                    }
                }

                stops.Add(rows);
            }

            if (RedoDesk.Pick(warehouses, stops, redone.From, redone.To, street, number, out var sourceAt, out var targetAt, out var stopAt))
            {
                var source = plans[sourceAt];
                var target = plans[targetAt];
                var chosen = source.destinations[stopAt];
                if (!ReferenceEquals(source, target))
                {
                    source.destinations.RemoveAt(stopAt);
                    if (target.destinations == null)
                    {
                        target.destinations = new List<LogisticsManagerPlanDestination>();
                    }

                    target.destinations.Add(chosen);
                }

                SaveGameManager.MarkChange();
                return true;
            }

            targetAt = RedoDesk.Warehouse(warehouses, redone.To);
            var building = NamedWarehouse(current, redone.To);
            if (targetAt < 0)
            {
                targetAt = PlanAt(plans, building);
            }

            LogisticsManagerPlan home;
            switch (RedoDesk.Attach(building != null, targetAt))
            {
                case "add":
                    home = plans[targetAt];
                    break;
                case "create":
                    if (building == null)
                    {
                        return false;
                    }

                    var opened = OpenPlan(current, plans, building);
                    if (opened == null)
                    {
                        return false;
                    }

                    home = opened;
                    current.logisticsManagerPlans.Add(home);
                    break;
                default:
                    return false;
            }

            return AddDestination(current, home, redone.Family, street, number);
        }

        private static bool AddDestination(
            GameInstance current,
            LogisticsManagerPlan home,
            string family,
            string street,
            int number)
        {
            if (home.destinations == null)
            {
                home.destinations = new List<LogisticsManagerPlanDestination>();
            }

            var created = new LogisticsManagerPlanDestination
            {
                isUiCollapsed = false,
                deliveryTargetAddress = new Address(street, number),
            };
            if (created.stockTargets == null)
            {
                created.stockTargets = new List<BigAmbitions.Items.ItemAmountTarget>();
            }

            Fill(current, created, family, street, number);
            home.destinations.Add(created);
            SaveGameManager.MarkChange();
            return true;
        }

        private static int PlanAt(List<LogisticsManagerPlan> plans, BuildingRegistration? building)
        {
            if (building == null || string.IsNullOrEmpty(building.StreetName) || building.StreetNumber <= 0)
            {
                return -1;
            }

            var found = -1;
            for (var plan = 0; plan < plans.Count; plan++)
            {
                var target = plans[plan] == null ? null : plans[plan].targetAddress;
                if (target == null || plans[plan].isFactory)
                {
                    continue;
                }

                if (target.streetName == building.StreetName && target.streetNumber == building.StreetNumber)
                {
                    found = plan;
                }
            }

            return found;
        }

        private static LogisticsManagerPlan? OpenPlan(
            GameInstance current,
            List<LogisticsManagerPlan> plans,
            BuildingRegistration warehouse)
        {
            var headquarters = Headquarters(current, plans);
            if (headquarters == null)
            {
                return null;
            }

            return new LogisticsManagerPlan
            {
                headquartersAddress = headquarters,
                isFactory = false,
                targetAddress = new Address(warehouse.StreetName, warehouse.StreetNumber),
                destinations = new List<LogisticsManagerPlanDestination>(),
            };
        }

        private static Address? Headquarters(GameInstance current, List<LogisticsManagerPlan> plans)
        {
            foreach (var plan in plans)
            {
                if (plan != null
                    && plan.headquartersAddress != null
                    && !string.IsNullOrEmpty(plan.headquartersAddress.streetName))
                {
                    return plan.headquartersAddress;
                }
            }

            if (current.BuildingRegistrations == null)
            {
                return null;
            }

            foreach (var building in current.BuildingRegistrations)
            {
                if (building == null || building.businessTypeName != "ba:businesstype_headquarters")
                {
                    continue;
                }

                if (string.IsNullOrEmpty(building.StreetName) || building.StreetNumber <= 0)
                {
                    continue;
                }

                return new Address(building.StreetName, building.StreetNumber);
            }

            return null;
        }

        private static BuildingRegistration? NamedWarehouse(GameInstance current, string name)
        {
            if (current.BuildingRegistrations == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            BuildingRegistration? fallback = null;
            foreach (var building in current.BuildingRegistrations)
            {
                if (building == null || building.BusinessName != name)
                {
                    continue;
                }

                if (building.businessTypeName == "ba:businesstype_warehouse")
                {
                    return building;
                }

                if (fallback == null)
                {
                    fallback = building;
                }
            }

            return fallback;
        }

        private static void Fill(
            GameInstance current,
            LogisticsManagerPlanDestination created,
            string family,
            string street,
            int number)
        {
            if (current.DeliveryContracts == null || created.stockTargets == null)
            {
                return;
            }

            var finished = new List<DeliveryContract>();
            foreach (var contract in current.DeliveryContracts)
            {
                if (contract == null || contract.items == null || contract.businessAddress == null)
                {
                    continue;
                }

                if (contract.businessAddress.streetName != street || contract.businessAddress.streetNumber != number)
                {
                    continue;
                }

                var leaving = new List<DeliveryContractItem>();
                foreach (var item in contract.items)
                {
                    if (item == null || string.IsNullOrEmpty(item.itemName) || Advisor.CargoFamily(item.itemName) != family)
                    {
                        continue;
                    }

                    var amount = item.amount;
                    if (amount <= 0)
                    {
                        amount = 1;
                    }

                    created.stockTargets.Add(new BigAmbitions.Items.ItemAmountTarget(item.itemName, amount));
                    leaving.Add(item);
                }

                foreach (var item in leaving)
                {
                    contract.items.Remove(item);
                }

                if (contract.items.Count == 0)
                {
                    finished.Add(contract);
                }
            }

            foreach (var contract in finished)
            {
                contract.Remove();
            }
        }

        private static Dictionary<string, string> WarehouseNames(GameInstance current)
        {
            var names = new Dictionary<string, string>();
            if (current.BuildingRegistrations == null)
            {
                return names;
            }

            foreach (var building in current.BuildingRegistrations)
            {
                if (building == null || string.IsNullOrEmpty(building.StreetName) || building.StreetNumber <= 0)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(building.BusinessName))
                {
                    continue;
                }

                var key = building.StreetName + "\n" + building.StreetNumber.ToString(CultureInfo.InvariantCulture);
                var warehouse = building.businessTypeName == "ba:businesstype_warehouse";
                if (!names.ContainsKey(key) || warehouse)
                {
                    names[key] = building.BusinessName;
                }
            }

            return names;
        }

        private static string NameOf(Dictionary<string, string> names, Address address)
        {
            if (address == null || string.IsNullOrEmpty(address.streetName))
            {
                return "";
            }

            var key = address.streetName + "\n" + address.streetNumber.ToString(CultureInfo.InvariantCulture);
            return names.TryGetValue(key, out var name) ? name : "";
        }

        private static void Split(string destination, out string street, out int number)
        {
            street = destination ?? "";
            number = 0;
            var split = street.LastIndexOf(' ');
            if (split <= 0)
            {
                return;
            }

            if (!int.TryParse(
                street.Substring(split + 1),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var parsed)
                || parsed <= 0)
            {
                return;
            }

            number = parsed;
            street = street.Substring(0, split);
        }
    }

    public sealed class DeskStop
    {
        public DeskStop(string street, int number)
        {
            Street = street ?? "";
            Number = number;
        }

        public string Street { get; }

        public int Number { get; }
    }

    public static class RedoDesk
    {
        public static int Warehouse(IReadOnlyList<string>? warehouses, string? name)
        {
            if (warehouses == null || string.IsNullOrEmpty(name))
            {
                return -1;
            }

            var found = -1;
            for (var plan = 0; plan < warehouses.Count; plan++)
            {
                if (warehouses[plan] == name)
                {
                    found = plan;
                }
            }

            return found;
        }

        public static string Attach(bool warehouseFound, int planIndex)
        {
            if (planIndex >= 0)
            {
                return "add";
            }

            if (warehouseFound)
            {
                return "create";
            }

            return "miss";
        }

        public static bool Pick(
            IReadOnlyList<string>? warehouses,
            IReadOnlyList<IReadOnlyList<DeskStop>>? stops,
            string? from,
            string? to,
            string? street,
            int number,
            out int source,
            out int target,
            out int stop)
        {
            source = -1;
            target = -1;
            stop = -1;
            if (warehouses == null || stops == null || number <= 0 || string.IsNullOrEmpty(street) || string.IsNullOrEmpty(to))
            {
                return false;
            }

            var count = warehouses.Count < stops.Count ? warehouses.Count : stops.Count;
            for (var plan = 0; plan < count; plan++)
            {
                if (warehouses[plan] == to)
                {
                    target = plan;
                }

                var rows = stops[plan];
                if (rows == null)
                {
                    continue;
                }

                for (var index = 0; index < rows.Count; index++)
                {
                    var row = rows[index];
                    if (row == null || row.Street != street || row.Number != number)
                    {
                        continue;
                    }

                    if (warehouses[plan] == from || source < 0)
                    {
                        source = plan;
                        stop = index;
                    }
                }
            }

            if (source < 0 || target < 0 || stop < 0)
            {
                source = -1;
                target = -1;
                stop = -1;
                return false;
            }

            return true;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace ChaoticCEO
{
    public sealed class PortfolioBusiness
    {
        public PortfolioBusiness(string business, string family, string street, int number)
        {
            Business = business ?? "";
            Family = family ?? "";
            Street = street ?? "";
            Number = number;
        }

        public string Business { get; }

        public string Family { get; }

        public string Street { get; }

        public int Number { get; }
    }

    public sealed class RankedBusiness
    {
        public RankedBusiness(string business, string family, string street, int number, double profit)
        {
            Business = business ?? "";
            Family = family ?? "";
            Street = street ?? "";
            Number = number;
            Profit = profit;
        }

        public string Business { get; }

        public string Family { get; }

        public string Street { get; }

        public int Number { get; }

        public double Profit { get; }
    }

    public sealed class StreetCluster
    {
        public StreetCluster(string street, string family, int count)
        {
            Street = street ?? "";
            Family = family ?? "";
            Count = count;
        }

        public string Street { get; }

        public string Family { get; }

        public int Count { get; }
    }

    public sealed class WarehouseMix
    {
        public WarehouseMix(string warehouse, string street, int number, string families)
        {
            Warehouse = warehouse ?? "";
            Street = street ?? "";
            Number = number;
            Families = families ?? "";
        }

        public string Warehouse { get; }

        public string Street { get; }

        public int Number { get; }

        public string Families { get; }
    }

    public static class Advisor
    {
        private const int CloseLimit = 10;

        private const int ClusterMinimum = 3;

        public static IReadOnlyList<RankedBusiness> Rank(
            IReadOnlyList<FinancialDay> days,
            IReadOnlyList<PortfolioBusiness> portfolio)
        {
            return Order(Averages(days, portfolio));
        }

        public static IReadOnlyList<RankedBusiness> CloseCandidates(
            IReadOnlyList<FinancialDay> days,
            IReadOnlyList<PortfolioBusiness> portfolio)
        {
            var ranked = Rank(days, portfolio);
            if (ranked.Count <= CloseLimit)
            {
                return ranked;
            }

            return ranked.Skip(ranked.Count - CloseLimit).ToList();
        }

        public static IReadOnlyList<StreetCluster> StreetClusters(IReadOnlyList<PortfolioBusiness> portfolio)
        {
            var counts = new Dictionary<(string Street, string Family), int>();
            if (portfolio != null)
            {
                foreach (var business in portfolio)
                {
                    if (business == null || business.Street.Length == 0 || business.Family.Length == 0)
                    {
                        continue;
                    }

                    var key = (business.Street, business.Family);
                    counts[key] = counts.TryGetValue(key, out var count) ? count + 1 : 1;
                }
            }

            return counts
                .Where(pair => pair.Value >= ClusterMinimum)
                .OrderBy(pair => pair.Key.Street, StringComparer.Ordinal)
                .ThenBy(pair => pair.Key.Family, StringComparer.Ordinal)
                .Select(pair => new StreetCluster(pair.Key.Street, pair.Key.Family, pair.Value))
                .ToList();
        }

        public static IReadOnlyList<WarehouseMix> WarehouseFamilyMix(IReadOnlyList<WarehouseHold> holds)
        {
            var groups = new Dictionary<(string Warehouse, string Street, int Number), HashSet<string>>();
            if (holds != null)
            {
                foreach (var hold in holds)
                {
                    if (hold == null)
                    {
                        continue;
                    }

                    var family = ItemFamily(hold.Item);
                    if (family == null)
                    {
                        continue;
                    }

                    var key = (hold.Warehouse, hold.Street, hold.Number);
                    if (!groups.TryGetValue(key, out var families))
                    {
                        families = new HashSet<string>();
                        groups[key] = families;
                    }

                    families.Add(family);
                }
            }

            return groups
                .Where(pair => pair.Value.Count >= 2)
                .Select(pair => new WarehouseMix(
                    pair.Key.Warehouse,
                    pair.Key.Street,
                    pair.Key.Number,
                    string.Join(", ", pair.Value.OrderBy(name => name, StringComparer.Ordinal))))
                .OrderBy(row => WarehousePrefix(row.Warehouse), StringComparer.Ordinal)
                .ThenBy(row => WarehouseDigits(row.Warehouse))
                .ThenBy(row => row.Warehouse, StringComparer.Ordinal)
                .ThenBy(row => row.Street, StringComparer.Ordinal)
                .ThenBy(row => row.Number)
                .ToList();
        }

        public static IReadOnlyList<RankedBusiness> RankFile(string statementsCsv)
        {
            var days = ReadStatementDays(statementsCsv);
            return Rank(days, PortfolioFrom(days));
        }

        public static IReadOnlyList<RankedBusiness> CloseCandidatesFile(string statementsCsv)
        {
            var days = ReadStatementDays(statementsCsv);
            return CloseCandidates(days, PortfolioFrom(days));
        }

        public static IReadOnlyList<WarehouseMix> WarehouseFamilyMixFile(string warehousesCsv)
        {
            return WarehouseFamilyMix(ReadHolds(warehousesCsv));
        }

        private static List<RankedBusiness> Averages(
            IReadOnlyList<FinancialDay> days,
            IReadOnlyList<PortfolioBusiness> portfolio)
        {
            var index = new Dictionary<(string Street, int Number), PortfolioBusiness>();
            if (portfolio != null)
            {
                foreach (var business in portfolio)
                {
                    if (business == null || business.Business.Length == 0)
                    {
                        continue;
                    }

                    index[(business.Street, business.Number)] = business;
                }
            }

            var buckets = new Dictionary<(string Street, int Number), List<double>>();
            if (days != null)
            {
                foreach (var day in days)
                {
                    if (day?.Rows == null)
                    {
                        continue;
                    }

                    foreach (var row in day.Rows)
                    {
                        if (row == null)
                        {
                            continue;
                        }

                        var key = (row.Street, row.Number);
                        if (!index.ContainsKey(key))
                        {
                            continue;
                        }

                        if (!buckets.TryGetValue(key, out var profits))
                        {
                            profits = new List<double>();
                            buckets[key] = profits;
                        }

                        profits.Add(row.Profit);
                    }
                }
            }

            var ranked = new List<RankedBusiness>();
            foreach (var pair in buckets)
            {
                var business = index[pair.Key];
                var profits = pair.Value;
                ranked.Add(new RankedBusiness(
                    business.Business,
                    business.Family,
                    pair.Key.Street,
                    pair.Key.Number,
                    profits.Sum() / profits.Count));
            }

            return ranked;
        }

        private static List<RankedBusiness> Order(List<RankedBusiness> rows)
        {
            return rows
                .OrderByDescending(row => row.Profit)
                .ThenBy(row => row.Business, StringComparer.Ordinal)
                .ThenBy(row => row.Street, StringComparer.Ordinal)
                .ThenBy(row => row.Number)
                .ToList();
        }

        private static List<PortfolioBusiness> PortfolioFrom(IReadOnlyList<FinancialDay> days)
        {
            var portfolio = new List<PortfolioBusiness>();
            foreach (var day in days)
            {
                foreach (var row in day.Rows)
                {
                    portfolio.Add(new PortfolioBusiness(row.Street, "", row.Street, row.Number));
                }
            }

            return portfolio;
        }

        private static List<FinancialDay> ReadStatementDays(string path)
        {
            var rowsByDay = new Dictionary<int, List<StatementRow>>();
            var order = new List<int>();
            foreach (var line in File.ReadAllLines(path))
            {
                if (line.Length == 0 || line.StartsWith("day,", StringComparison.Ordinal))
                {
                    continue;
                }

                var cells = line.Split(',');
                if (cells.Length < 10)
                {
                    continue;
                }

                if (!int.TryParse(cells[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var day)
                    || !int.TryParse(cells[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
                    || !float.TryParse(cells[9], NumberStyles.Float, CultureInfo.InvariantCulture, out var profit))
                {
                    continue;
                }

                if (!rowsByDay.TryGetValue(day, out var rows))
                {
                    rows = new List<StatementRow>();
                    rowsByDay[day] = rows;
                    order.Add(day);
                }

                rows.Add(new StatementRow(cells[1], number, 0f, 0f, 0f, 0f, 0f, 0f, profit));
            }

            var days = new List<FinancialDay>();
            foreach (var day in order)
            {
                days.Add(new FinancialDay(day, rowsByDay[day]));
            }

            return days;
        }

        private static List<WarehouseHold> ReadHolds(string path)
        {
            var holds = new List<WarehouseHold>();
            foreach (var line in File.ReadAllLines(path))
            {
                if (line.Length == 0 || line.StartsWith("warehouse,", StringComparison.Ordinal))
                {
                    continue;
                }

                var cells = line.Split(',');
                if (cells.Length < 5)
                {
                    continue;
                }

                if (!int.TryParse(cells[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
                    || !int.TryParse(cells[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount))
                {
                    continue;
                }

                holds.Add(new WarehouseHold(cells[0], cells[1], number, cells[3], amount));
            }

            return holds;
        }

        private static string? ItemFamily(string item)
        {
            if (string.IsNullOrEmpty(item) || IgnoredCargo.Contains(item))
            {
                return null;
            }

            return ItemFamilies.TryGetValue(item, out var family) ? family : "unknown";
        }

        private static string WarehousePrefix(string name)
        {
            var prefix = "";
            foreach (var ch in name)
            {
                if (!char.IsDigit(ch))
                {
                    prefix += ch;
                }
            }

            return prefix;
        }

        private static int WarehouseDigits(string name)
        {
            var digits = "";
            foreach (var ch in name)
            {
                if (char.IsDigit(ch))
                {
                    digits += ch;
                }
            }

            return digits.Length == 0 ? 0 : int.Parse(digits, CultureInfo.InvariantCulture);
        }

        private static readonly HashSet<string> IgnoredCargo = new HashSet<string>
        {
            "ba:itemname_ceilinglampwarehouse",
            "ba:itemname_closedcardboardbox",
            "ba:itemname_deliveryspot",
            "ba:itemname_flatbedspawner",
            "ba:itemname_handtruckspawner",
            "ba:itemname_palletshelf",
            "ba:itemname_paperbag",
        };

        private static readonly Dictionary<string, string> ItemFamilies = BuildFamilies();

        private static Dictionary<string, string> BuildFamilies()
        {
            var map = new Dictionary<string, string>();
            Add(map, "food", "apple", "banana", "beer", "bottleofwine", "burger", "carrot", "cigar", "cigarette", "croissant", "cupcake", "cupofcoffee", "donut", "frenchfries", "freshfood", "frozenfood", "hotdog", "icecream", "kabob", "lettuce", "margarita", "martini", "pear", "pizza", "salad", "sodacan", "tomato", "whisky");
            Add(map, "durables", "cheapjewelry", "classiccheapfemaleclothing", "classiccheapmaleclothing", "classicexpensivefemaleclothing", "classicexpensivemaleclothing", "earbuds01", "expensivejewelry", "headphones01", "moderncheapfemaleclothing", "moderncheapmaleclothing", "modernexpensivefemaleclothing", "modernexpensivemaleclothing", "smartphone1", "smartphone2", "smartwatch1", "smartwatch2");
            Add(map, "specialty", "cheapflower", "cheapgift", "expensiveflower", "expensivegift", "limitededitionbook", "motivationalbook", "novel", "picturebook", "technicalmanual", "youngnovel");
            Add(map, "service", "haircareproduct");
            return map;
        }

        private static void Add(Dictionary<string, string> map, string family, params string[] names)
        {
            foreach (var name in names)
            {
                map["ba:itemname_" + name] = family;
            }
        }
    }
}

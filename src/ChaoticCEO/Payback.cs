using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ChaoticCEO
{
    public sealed class BuildingAsk
    {
        public BuildingAsk(string street, int number, double price)
        {
            Street = street ?? "";
            Number = number;
            Price = price;
        }

        public string Street { get; }

        public int Number { get; }

        public double Price { get; }
    }

    public sealed class RentPayback
    {
        public RentPayback(string street, int number, double days, bool bought)
        {
            Street = street ?? "";
            Number = number;
            Days = days;
            Bought = bought;
            Explain = bought
                ? "Open " + Street
                    + ". Buy " + Street + " " + number.ToString(CultureInfo.InvariantCulture)
                    + ". Payback " + Format(days)
                    + "."
                : "";
        }

        public string Street { get; }

        public int Number { get; }

        public double Days { get; }

        public bool Bought { get; }

        public string Explain { get; }

        public static RentPayback None()
        {
            return new RentPayback("", 0, 0d, false);
        }

        private static string Format(double days)
        {
            var rounded = Math.Round(days, 4, MidpointRounding.ToEven);
            if (rounded == Math.Truncate(rounded))
            {
                return ((long)rounded).ToString(CultureInfo.InvariantCulture);
            }

            return rounded.ToString("0.####", CultureInfo.InvariantCulture);
        }
    }

    public static class Payback
    {
        public static RentPayback Decide(
            ApiNote? api,
            LogisticsReview? review,
            IReadOnlyList<FinancialDay>? days,
            IReadOnlyList<BuildingAsk>? asks)
        {
            if (api == null || api.Build.Length == 0 || review == null)
            {
                return RentPayback.None();
            }

            return Choose(days, asks);
        }

        public static RentPayback FromLedger(
            LogisticsReview? review,
            ApiNote? api,
            string? statementsCsv,
            IReadOnlyList<BuildingAsk>? asks)
        {
            return Decide(api, review, ReadDays(statementsCsv), asks);
        }

        private static RentPayback Choose(IReadOnlyList<FinancialDay>? days, IReadOnlyList<BuildingAsk>? asks)
        {
            if (asks == null || asks.Count == 0)
            {
                return RentPayback.None();
            }

            var rents = LatestRents(days);
            BuildingAsk? best = null;
            var bestDays = 0d;
            var bestIndex = -1;
            for (var i = 0; i < asks.Count; i++)
            {
                var ask = asks[i];
                if (ask == null || ask.Street.Length == 0 || ask.Number <= 0 || ask.Price <= 0d)
                {
                    continue;
                }

                if (!rents.TryGetValue((ask.Street, ask.Number), out var rent) || rent <= 0d)
                {
                    continue;
                }

                var payback = ask.Price / rent;
                if (best == null || payback < bestDays || (payback == bestDays && i > bestIndex))
                {
                    best = ask;
                    bestDays = payback;
                    bestIndex = i;
                }
            }

            if (best == null)
            {
                return RentPayback.None();
            }

            return new RentPayback(best.Street, best.Number, bestDays, true);
        }

        private static Dictionary<(string Street, int Number), double> LatestRents(IReadOnlyList<FinancialDay>? days)
        {
            var best = new Dictionary<(string Street, int Number), (int Day, int Sequence, double Rent)>();
            var sequence = 0;
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
                        sequence++;
                        if (row == null || row.Street.Length == 0)
                        {
                            continue;
                        }

                        var key = (row.Street, row.Number);
                        if (!best.TryGetValue(key, out var current)
                            || day.Day > current.Day
                            || (day.Day == current.Day && sequence > current.Sequence))
                        {
                            best[key] = (day.Day, sequence, row.Rent);
                        }
                    }
                }
            }

            var rents = new Dictionary<(string Street, int Number), double>();
            foreach (var pair in best)
            {
                rents[pair.Key] = pair.Value.Rent;
            }

            return rents;
        }

        private static List<FinancialDay> ReadDays(string? path)
        {
            var rowsByDay = new Dictionary<int, List<StatementRow>>();
            var order = new List<int>();
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                foreach (var line in File.ReadAllLines(path))
                {
                    if (line.Length == 0 || line.StartsWith("day,", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var cells = line.Split(',');
                    if (cells.Length < 7)
                    {
                        continue;
                    }

                    if (!int.TryParse(cells[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var day)
                        || !int.TryParse(cells[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
                        || !float.TryParse(cells[6], NumberStyles.Float, CultureInfo.InvariantCulture, out var rent))
                    {
                        continue;
                    }

                    if (!rowsByDay.TryGetValue(day, out var rows))
                    {
                        rows = new List<StatementRow>();
                        rowsByDay[day] = rows;
                        order.Add(day);
                    }

                    rows.Add(new StatementRow(cells[1], number, 0f, 0f, 0f, rent, 0f, 0f, 0f));
                }
            }

            var days = new List<FinancialDay>();
            foreach (var day in order)
            {
                days.Add(new FinancialDay(day, rowsByDay[day]));
            }

            return days;
        }
    }
}

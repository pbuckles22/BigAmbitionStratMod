using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace ChaoticCEO
{
    public sealed class StoreCurve
    {
        public StoreCurve(
            string street,
            int number,
            decimal dailySales,
            decimal goodsPerSale,
            decimal dailyWages,
            decimal dailyRent,
            decimal dailyMarketing,
            decimal dailyTheft,
            int sampleDays)
        {
            Street = street ?? "";
            Number = number;
            DailySales = dailySales;
            GoodsPerSale = goodsPerSale;
            DailyWages = dailyWages;
            DailyRent = dailyRent;
            DailyMarketing = dailyMarketing;
            DailyTheft = dailyTheft;
            SampleDays = sampleDays;
        }

        public string Street { get; }

        public int Number { get; }

        public decimal DailySales { get; }

        public decimal GoodsPerSale { get; }

        public decimal DailyWages { get; }

        public decimal DailyRent { get; }

        public decimal DailyMarketing { get; }

        public decimal DailyTheft { get; }

        public int SampleDays { get; }
    }

    public static class Curves
    {
        public static IReadOnlyList<StoreCurve> Fit(IReadOnlyList<FinancialDay>? days)
        {
            var buckets = new Dictionary<(string Street, int Number), Bucket>();
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
                        if (row == null || row.Street.Length == 0)
                        {
                            continue;
                        }

                        var key = (row.Street, row.Number);
                        if (!buckets.TryGetValue(key, out var bucket))
                        {
                            bucket = new Bucket();
                            buckets[key] = bucket;
                        }

                        bucket.Add(row);
                    }
                }
            }

            return buckets
                .OrderBy(pair => pair.Key.Street, StringComparer.Ordinal)
                .ThenBy(pair => pair.Key.Number)
                .Select(pair => pair.Value.ToCurve(pair.Key.Street, pair.Key.Number))
                .ToList();
        }

        public static IReadOnlyList<StoreCurve> FitFile(string? statementsCsv)
        {
            return Fit(ReadFile(statementsCsv));
        }

        public static IReadOnlyList<FinancialDay> ReadFile(string? statementsCsv)
        {
            return ReadDays(statementsCsv);
        }

        public static StatementRow Project(StoreCurve? curve)
        {
            if (curve == null || curve.Street.Length == 0)
            {
                return new StatementRow("", 0, 0f, 0f, 0f, 0f, 0f, 0f, 0f);
            }

            var sales = (float)curve.DailySales;
            var goods = (float)(curve.DailySales * curve.GoodsPerSale);
            var wages = (float)curve.DailyWages;
            var rent = (float)curve.DailyRent;
            var marketing = (float)curve.DailyMarketing;
            var theft = (float)curve.DailyTheft;
            var profit = sales - goods - wages - rent - marketing - theft;
            return new StatementRow(
                curve.Street,
                curve.Number,
                sales,
                goods,
                wages,
                rent,
                marketing,
                theft,
                profit);
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
                    if (cells.Length < 10)
                    {
                        continue;
                    }

                    if (!int.TryParse(cells[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var day)
                        || !int.TryParse(cells[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
                        || !float.TryParse(cells[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var sales)
                        || !float.TryParse(cells[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var goods)
                        || !float.TryParse(cells[5], NumberStyles.Float, CultureInfo.InvariantCulture, out var wages)
                        || !float.TryParse(cells[6], NumberStyles.Float, CultureInfo.InvariantCulture, out var rent)
                        || !float.TryParse(cells[7], NumberStyles.Float, CultureInfo.InvariantCulture, out var marketing)
                        || !float.TryParse(cells[8], NumberStyles.Float, CultureInfo.InvariantCulture, out var theft)
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

                    rows.Add(new StatementRow(cells[1], number, sales, goods, wages, rent, marketing, theft, profit));
                }
            }

            var days = new List<FinancialDay>();
            foreach (var day in order)
            {
                days.Add(new FinancialDay(day, rowsByDay[day]));
            }

            return days;
        }

        private sealed class Bucket
        {
            private decimal _sales;
            private decimal _goods;
            private decimal _wages;
            private decimal _rent;
            private decimal _marketing;
            private decimal _theft;
            private int _count;

            public void Add(StatementRow row)
            {
                _sales += (decimal)row.Sales;
                _goods += (decimal)row.Goods;
                _wages += (decimal)row.Wages;
                _rent += (decimal)row.Rent;
                _marketing += (decimal)row.Marketing;
                _theft += (decimal)row.Theft;
                _count++;
            }

            public StoreCurve ToCurve(string street, int number)
            {
                var dailySales = _count == 0 ? 0m : _sales / _count;
                var goodsPerSale = _sales == 0m ? 0m : _goods / _sales;
                return new StoreCurve(
                    street,
                    number,
                    dailySales,
                    goodsPerSale,
                    _count == 0 ? 0m : _wages / _count,
                    _count == 0 ? 0m : _rent / _count,
                    _count == 0 ? 0m : _marketing / _count,
                    _count == 0 ? 0m : _theft / _count,
                    _count);
            }
        }
    }
}

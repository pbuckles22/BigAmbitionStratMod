using System;
using System.Collections.Generic;
using System.Linq;

namespace ChaoticCEO
{
    public sealed class PortfolioRow
    {
        public PortfolioRow(string business, string family, string street, int number, double tco)
        {
            Business = business ?? "";
            Family = family ?? "";
            Street = street ?? "";
            Number = number;
            Tco = tco;
        }

        public string Business { get; }

        public string Family { get; }

        public string Street { get; }

        public int Number { get; }

        public double Tco { get; }
    }

    public static class DayStep
    {
        public static FinancialDay Day(int day, IReadOnlyList<StoreCurve>? curves)
        {
            var rows = new List<StatementRow>();
            if (curves != null)
            {
                foreach (var curve in curves
                    .Where(curve => curve != null && curve.Street.Length > 0)
                    .OrderBy(curve => curve.Street, StringComparer.Ordinal)
                    .ThenBy(curve => curve.Number))
                {
                    rows.Add(Curves.Project(curve));
                }
            }

            return new FinancialDay(day, rows);
        }

        public static IReadOnlyList<FinancialDay> Run(int startDay, int count, IReadOnlyList<StoreCurve>? curves)
        {
            if (count <= 0 || curves == null || curves.Count == 0)
            {
                return Array.Empty<FinancialDay>();
            }

            var days = new List<FinancialDay>(count);
            for (var offset = 0; offset < count; offset++)
            {
                days.Add(Day(startDay + offset, curves));
            }

            return days;
        }

        public static IReadOnlyList<PortfolioRow> Portfolio(
            IReadOnlyList<FinancialDay>? days,
            IReadOnlyList<PortfolioBusiness>? portfolio)
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

                        if (!buckets.TryGetValue(key, out var costs))
                        {
                            costs = new List<double>();
                            buckets[key] = costs;
                        }

                        costs.Add(row.Goods + row.Wages + row.Rent + row.Marketing);
                    }
                }
            }

            return buckets
                .OrderBy(pair => pair.Key.Street, StringComparer.Ordinal)
                .ThenBy(pair => pair.Key.Number)
                .Select(pair =>
                {
                    var business = index[pair.Key];
                    var costs = pair.Value;
                    return new PortfolioRow(
                        business.Business,
                        business.Family,
                        pair.Key.Street,
                        pair.Key.Number,
                        costs.Sum() / costs.Count);
                })
                .ToList();
        }

        public static IReadOnlyList<RankedBusiness> Rank(
            IReadOnlyList<FinancialDay>? days,
            IReadOnlyList<PortfolioBusiness>? portfolio)
        {
            return Advisor.Rank(
                days ?? Array.Empty<FinancialDay>(),
                portfolio ?? Array.Empty<PortfolioBusiness>());
        }
    }
}

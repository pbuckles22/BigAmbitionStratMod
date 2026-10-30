using System;
using System.Collections.Generic;
using System.Globalization;

namespace ChaoticCEO
{
    public sealed class CloseScoreLine
    {
        public CloseScoreLine(string street, int number, string explain)
        {
            Street = street ?? "";
            Number = number;
            Explain = explain ?? "";
        }

        public string Street { get; }

        public int Number { get; }

        public string Explain { get; }
    }

    public static class CloseScore
    {
        public static IReadOnlyList<CloseScoreLine> Score(ApiNote? api, IReadOnlyList<FinancialDay>? days)
        {
            var lines = new List<CloseScoreLine>();
            if (api == null || api.Build.Length == 0 || days == null)
            {
                return lines;
            }

            var order = new List<(string Street, int Number)>();
            var profits = new Dictionary<(string Street, int Number), Dictionary<int, double>>();
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
                    if (!profits.TryGetValue(key, out var byDay))
                    {
                        byDay = new Dictionary<int, double>();
                        profits[key] = byDay;
                        order.Add(key);
                    }

                    byDay[day.Day] = row.Profit;
                }
            }

            foreach (var key in order)
            {
                var byDay = profits[key];
                var dayNumbers = new List<int>(byDay.Keys);
                dayNumbers.Sort();
                var first = byDay[dayNumbers[0]];
                var last = first;
                var streak = 0;
                foreach (var dayNumber in dayNumbers)
                {
                    last = byDay[dayNumber];
                    if (last < 0d)
                    {
                        streak++;
                    }
                    else
                    {
                        streak = 0;
                    }
                }

                lines.Add(new CloseScoreLine(
                    key.Street,
                    key.Number,
                    OneLine(key.Street, key.Number, last, streak, last - first)));
            }

            return lines;
        }

        private static string OneLine(string street, int number, double profit, int streak, double trend)
        {
            return "Close " + street + " "
                + number.ToString(CultureInfo.InvariantCulture)
                + ". After rent " + FormatProfit(profit)
                + ". Streak " + streak.ToString(CultureInfo.InvariantCulture)
                + ". Trend " + FormatProfit(trend)
                + ".";
        }

        private static string FormatProfit(double value)
        {
            var rounded = Math.Round(value, 4, MidpointRounding.ToEven);
            if (rounded == Math.Truncate(rounded))
            {
                return ((long)rounded).ToString(CultureInfo.InvariantCulture);
            }

            return rounded.ToString("0.####", CultureInfo.InvariantCulture);
        }
    }
}

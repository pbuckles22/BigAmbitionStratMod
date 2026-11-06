using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ChaoticCEO
{
    public static class TopBar
    {
        public const decimal WinNetWorth = 100000000m;

        private static string _seen = "";

        public static string Line(
            int day,
            decimal cash,
            decimal netWorth,
            decimal profit,
            int stage,
            bool autopilot,
            int speed)
        {
            return "Day " + day.ToString(CultureInfo.InvariantCulture)
                + "  Cash " + Money(Dollar(cash))
                + "  Net worth " + Money(Dollar(netWorth)) + " / " + Money(WinNetWorth)
                + "  Profit " + Money(Dollar(profit))
                + "  Stage " + stage.ToString(CultureInfo.InvariantCulture)
                + "  Autopilot " + (autopilot ? "on" : "off")
                + "  Speed " + speed.ToString(CultureInfo.InvariantCulture);
        }

        public static int Day(string? rolled)
        {
            if (string.IsNullOrEmpty(rolled))
            {
                return 0;
            }

            const string token = "day=";
            var at = rolled.LastIndexOf(token, StringComparison.Ordinal);
            if (at < 0)
            {
                return 0;
            }

            var rest = rolled.Substring(at + token.Length);
            var end = 0;
            while (end < rest.Length && char.IsDigit(rest[end]))
            {
                end++;
            }

            if (end == 0)
            {
                return 0;
            }

            return int.Parse(rest.Substring(0, end), CultureInfo.InvariantCulture);
        }

        public static decimal Profit(string? csv)
        {
            if (string.IsNullOrEmpty(csv))
            {
                return 0m;
            }

            var sum = 0m;
            var lines = csv.Split('\n');
            for (var index = 1; index < lines.Length; index++)
            {
                var row = lines[index].Trim().TrimEnd('\r');
                if (row.Length == 0)
                {
                    continue;
                }

                var cells = row.Split(',');
                if (cells.Length < 10)
                {
                    continue;
                }

                if (decimal.TryParse(cells[9], NumberStyles.Number, CultureInfo.InvariantCulture, out var profit))
                {
                    sum += profit;
                }
            }

            return sum;
        }

        public static decimal ShownProfit(decimal dayTotal, decimal businessSubtotal)
        {
            _ = businessSubtotal;
            return Dollar(dayTotal);
        }

        public static decimal Dollar(decimal value)
        {
            return decimal.Round(value, 0, MidpointRounding.AwayFromZero);
        }

        public static decimal Yesterday(IReadOnlyList<FinancialDay>? days, int today)
        {
            if (days == null)
            {
                return 0m;
            }

            var sum = 0m;
            var target = today - 1;
            foreach (var day in days)
            {
                if (day == null || day.Day != target || day.Rows == null)
                {
                    continue;
                }

                foreach (var row in day.Rows)
                {
                    if (row != null)
                    {
                        sum += (decimal)row.Profit;
                    }
                }
            }

            return sum;
        }

        public static int Clock(bool paused, int? rate)
        {
            if (paused || rate == null || rate.Value <= 0)
            {
                return 0;
            }

            return rate.Value;
        }

        public static string From(
            string? rolled,
            string? csv,
            decimal cash,
            decimal netWorth,
            int stage,
            bool autopilot,
            int speed)
        {
            return Line(Day(rolled), cash, netWorth, Profit(csv), stage, autopilot, speed);
        }

        public static IReadOnlyList<string> Frame(string? bar, IReadOnlyList<string>? body)
        {
            var lines = new List<string>();
            if (!string.IsNullOrEmpty(bar))
            {
                lines.Add(bar);
            }

            if (body == null)
            {
                return lines;
            }

            foreach (var row in body)
            {
                if (!string.IsNullOrEmpty(row))
                {
                    lines.Add(row);
                }
            }

            return lines;
        }

        public static string Record(string localAppData, string line)
        {
            var shown = "bar.shown version=" + LoadLog.Version + " " + (line ?? "");
            var key = (localAppData ?? "") + "\n" + shown;
            if (key == _seen)
            {
                return shown;
            }

            _seen = key;
            var path = LoadLog.CeoLogPath(localAppData ?? "");
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(path, shown + Environment.NewLine);
            return shown;
        }

        private static string Money(decimal value)
        {
            var rounded = decimal.Round(value, 4, MidpointRounding.ToEven);
            if (rounded == decimal.Truncate(rounded))
            {
                return decimal.Truncate(rounded).ToString(CultureInfo.InvariantCulture);
            }

            return rounded.ToString("0.####", CultureInfo.InvariantCulture);
        }
    }
}

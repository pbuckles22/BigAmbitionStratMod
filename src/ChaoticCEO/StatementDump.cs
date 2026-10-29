using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ChaoticCEO
{
    public sealed class StatementRow
    {
        public StatementRow(
            string street,
            int number,
            float sales,
            float goods,
            float wages,
            float rent,
            float marketing,
            float theft,
            float profit)
        {
            Street = street ?? "";
            Number = number;
            Sales = sales;
            Goods = goods;
            Wages = wages;
            Rent = rent;
            Marketing = marketing;
            Theft = theft;
            Profit = profit;
        }

        public string Street { get; }

        public int Number { get; }

        public float Sales { get; }

        public float Goods { get; }

        public float Wages { get; }

        public float Rent { get; }

        public float Marketing { get; }

        public float Theft { get; }

        public float Profit { get; }
    }

    public sealed class FinancialDay
    {
        public FinancialDay(int day, IReadOnlyList<StatementRow> rows)
        {
            Day = day;
            Rows = rows ?? Array.Empty<StatementRow>();
        }

        public int Day { get; }

        public IReadOnlyList<StatementRow> Rows { get; }
    }

    public static class StatementDump
    {
        public const string Hotkey = "F8";

        public static string CsvPath(string localAppData)
        {
            return Path.Combine(localAppData, LoadLog.ModId, "statements.csv");
        }

        public static string Write(string localAppData, int currentDay, IReadOnlyList<FinancialDay> days)
        {
            var yesterday = currentDay - 1;
            IReadOnlyList<StatementRow> rows = Array.Empty<StatementRow>();
            if (days != null)
            {
                foreach (var day in days)
                {
                    if (day != null && day.Day == yesterday)
                    {
                        rows = day.Rows ?? Array.Empty<StatementRow>();
                        break;
                    }
                }
            }

            var csvPath = CsvPath(localAppData);
            var directory = Path.GetDirectoryName(csvPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(csvPath, Csv(yesterday, rows));
            var line = Line(yesterday, rows);
            File.AppendAllText(LoadLog.CeoLogPath(localAppData), line + Environment.NewLine);
            return line;
        }

        public static string FormatAmount(float value)
        {
            var rounded = Math.Round(value, 4, MidpointRounding.ToEven);
            if (rounded == Math.Truncate(rounded))
            {
                return ((long)rounded).ToString(CultureInfo.InvariantCulture);
            }

            return rounded.ToString("0.####", CultureInfo.InvariantCulture);
        }

        private static string Csv(int day, IReadOnlyList<StatementRow> rows)
        {
            var text = new StringBuilder();
            text.Append("day,street,number,sales,goods,wages,rent,marketing,theft,profit\n");
            foreach (var row in rows)
            {
                if (row == null)
                {
                    continue;
                }

                text.Append(day.ToString(CultureInfo.InvariantCulture));
                text.Append(',');
                text.Append(row.Street);
                text.Append(',');
                text.Append(row.Number.ToString(CultureInfo.InvariantCulture));
                text.Append(',');
                text.Append(FormatAmount(row.Sales));
                text.Append(',');
                text.Append(FormatAmount(row.Goods));
                text.Append(',');
                text.Append(FormatAmount(row.Wages));
                text.Append(',');
                text.Append(FormatAmount(row.Rent));
                text.Append(',');
                text.Append(FormatAmount(row.Marketing));
                text.Append(',');
                text.Append(FormatAmount(row.Theft));
                text.Append(',');
                text.Append(FormatAmount(row.Profit));
                text.Append('\n');
            }

            return text.ToString();
        }

        private static string Line(int day, IReadOnlyList<StatementRow> rows)
        {
            StatementRow? first = null;
            var count = 0;
            foreach (var row in rows)
            {
                if (row == null)
                {
                    continue;
                }

                if (first == null)
                {
                    first = row;
                }

                count++;
            }

            return "csv.written version=" + LoadLog.Version
                + " rows=" + count.ToString(CultureInfo.InvariantCulture)
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " street=" + (first == null ? "" : first.Street)
                + " number=" + (first == null ? "" : first.Number.ToString(CultureInfo.InvariantCulture))
                + " profit=" + (first == null ? "" : FormatAmount(first.Profit));
        }
    }
}

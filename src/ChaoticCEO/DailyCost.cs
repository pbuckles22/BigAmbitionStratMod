using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ChaoticCEO
{
    public sealed class CostParts
    {
        public CostParts(
            decimal wages,
            decimal rent,
            decimal marketing,
            decimal loan,
            decimal truck,
            decimal unassigned)
        {
            Wages = NonNegative(wages);
            Rent = NonNegative(rent);
            Marketing = NonNegative(marketing);
            Loan = NonNegative(loan);
            Truck = NonNegative(truck);
            Unassigned = NonNegative(unassigned);
            Daily = Wages + Rent + Marketing + Loan + Truck + Unassigned;
        }

        public decimal Wages { get; }

        public decimal Rent { get; }

        public decimal Marketing { get; }

        public decimal Loan { get; }

        public decimal Truck { get; }

        public decimal Unassigned { get; }

        public decimal Daily { get; }

        private static decimal NonNegative(decimal value)
        {
            return value > 0m ? value : 0m;
        }
    }

    public static class DailyCost
    {
        public static CostParts Read(
            IReadOnlyList<FinancialDay>? days,
            int today,
            decimal loanPayments,
            decimal truckCost,
            decimal unassignedWages = 0m)
        {
            decimal wages = 0m;
            decimal rent = 0m;
            decimal marketing = 0m;
            if (days != null)
            {
                var yesterday = today - 1;
                foreach (var day in days)
                {
                    if (day == null || day.Day != yesterday || day.Rows == null)
                    {
                        continue;
                    }

                    foreach (var row in day.Rows)
                    {
                        if (row == null)
                        {
                            continue;
                        }

                        wages += NonNegative(row.Wages);
                        rent += NonNegative(row.Rent);
                        marketing += NonNegative(row.Marketing);
                    }
                }
            }

            return new CostParts(wages, rent, marketing, loanPayments, truckCost, unassignedWages);
        }

        public static decimal From(
            IReadOnlyList<FinancialDay>? days,
            int today,
            decimal loanPayments,
            decimal truckCost,
            decimal unassignedWages = 0m)
        {
            return Read(days, today, loanPayments, truckCost, unassignedWages).Daily;
        }

        public static string Line(int day, decimal cash, CostParts parts)
        {
            var cost = parts ?? new CostParts(0m, 0m, 0m, 0m, 0m, 0m);
            return "cost.read version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " cash=" + Format(cash)
                + " wages=" + Format(cost.Wages)
                + " rent=" + Format(cost.Rent)
                + " marketing=" + Format(cost.Marketing)
                + " loan=" + Format(cost.Loan)
                + " truck=" + Format(cost.Truck)
                + " unassigned=" + Format(cost.Unassigned)
                + " daily=" + Format(cost.Daily)
                + " reserve=" + Format(Money.Reserve(cost.Daily));
        }

        public static string Record(string? localAppData, string line)
        {
            if (string.IsNullOrEmpty(localAppData) || string.IsNullOrEmpty(line))
            {
                return line ?? "";
            }

            var path = LoadLog.CeoLogPath(localAppData);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(path, line + Environment.NewLine);
            return line;
        }

        private static decimal NonNegative(float value)
        {
            if (value <= 0f)
            {
                return 0m;
            }

            return (decimal)value;
        }

        private static string Format(decimal value)
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

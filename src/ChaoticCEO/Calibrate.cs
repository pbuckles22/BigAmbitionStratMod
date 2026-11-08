using System;
using System.Collections.Generic;

namespace ChaoticCEO
{
    public sealed class CalibrateResult
    {
        public CalibrateResult(
            int startDay,
            int endDay,
            decimal projected,
            decimal actual,
            decimal error,
            bool within)
        {
            StartDay = startDay;
            EndDay = endDay;
            Projected = projected;
            Actual = actual;
            Error = error;
            Within = within;
        }

        public int StartDay { get; }

        public int EndDay { get; }

        public decimal Projected { get; }

        public decimal Actual { get; }

        public decimal Error { get; }

        public bool Within { get; }

        public static CalibrateResult Miss(int startDay, int endDay)
        {
            return new CalibrateResult(startDay, endDay, 0m, 0m, 1m, false);
        }
    }

    public static class Calibrate
    {
        public const decimal Tolerance = 0.20m;

        public const int StartDay = 1000;

        public const int EndDay = 1014;

        public static decimal Error(decimal projected, decimal actual)
        {
            var gap = Math.Abs(projected - actual);
            var denom = Math.Abs(actual);
            if (denom < 1m)
            {
                denom = 1m;
            }

            return gap / denom;
        }

        public static bool Within(decimal projected, decimal actual, decimal tolerance)
        {
            return Error(projected, actual) <= tolerance;
        }

        public static CalibrateResult Check(
            IReadOnlyList<FinancialDay>? ledger,
            int startDay = StartDay,
            int endDay = EndDay,
            decimal tolerance = Tolerance)
        {
            if (ledger == null || ledger.Count == 0 || endDay < startDay)
            {
                return CalibrateResult.Miss(startDay, endDay);
            }

            var fitDays = new List<FinancialDay>();
            FinancialDay? actualDay = null;
            foreach (var day in ledger)
            {
                if (day == null)
                {
                    continue;
                }

                if (day.Day <= startDay)
                {
                    fitDays.Add(day);
                }

                if (day.Day == endDay)
                {
                    actualDay = day;
                }
            }

            if (fitDays.Count == 0 || actualDay == null)
            {
                return CalibrateResult.Miss(startDay, endDay);
            }

            var curves = Curves.Fit(fitDays);
            if (curves.Count == 0)
            {
                return CalibrateResult.Miss(startDay, endDay);
            }

            var stepped = DayStep.Run(startDay, endDay - startDay + 1, curves);
            FinancialDay? projectedDay = null;
            foreach (var day in stepped)
            {
                if (day.Day == endDay)
                {
                    projectedDay = day;
                    break;
                }
            }

            if (projectedDay == null)
            {
                return CalibrateResult.Miss(startDay, endDay);
            }

            var keys = new HashSet<(string Street, int Number)>();
            decimal projected = 0m;
            foreach (var row in projectedDay.Rows)
            {
                if (row == null)
                {
                    continue;
                }

                keys.Add((row.Street, row.Number));
                projected += (decimal)row.Profit;
            }

            decimal actual = 0m;
            foreach (var row in actualDay.Rows)
            {
                if (row == null || !keys.Contains((row.Street, row.Number)))
                {
                    continue;
                }

                actual += (decimal)row.Profit;
            }

            var error = Error(projected, actual);
            return new CalibrateResult(
                startDay,
                endDay,
                projected,
                actual,
                error,
                Within(projected, actual, tolerance));
        }

        public static CalibrateResult CheckFile(string? statementsCsv)
        {
            return Check(Curves.ReadFile(statementsCsv));
        }
    }
}

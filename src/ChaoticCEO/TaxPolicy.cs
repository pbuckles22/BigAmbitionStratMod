using System;
using System.Globalization;

namespace ChaoticCEO
{
    public sealed class TaxPaymentPick
    {
        public TaxPaymentPick(string kind, decimal amount, bool chosen)
        {
            Kind = kind ?? "";
            Amount = amount;
            Chosen = chosen;
        }

        public string Kind { get; }

        public decimal Amount { get; }

        public bool Chosen { get; }

        public static TaxPaymentPick None()
        {
            return new TaxPaymentPick("", 0m, false);
        }
    }

    public static class TaxPolicy
    {
        public const decimal EstimatedTaxRate = 0.25m;

        public const int UrgentTaxDays = 2;

        public const int MinSurvivalDays = 3;

        public const decimal MinTaxPartialStep = 500m;

        public const int TaxShieldWindowDays = 5;

        public const decimal TaxShieldDaySpend = 0.60m;

        public static decimal UnbilledAccruedTax(
            decimal cycleNetProfit,
            decimal deductibleCapEx = 0m,
            decimal estimatedRate = EstimatedTaxRate)
        {
            var taxable = cycleNetProfit - (deductibleCapEx > 0m ? deductibleCapEx : 0m);
            if (taxable <= 0m || estimatedRate <= 0m)
            {
                return 0m;
            }

            return decimal.Round(taxable * estimatedRate, 2, MidpointRounding.AwayFromZero);
        }

        public static decimal TotalTaxReserve(
            decimal billedCurrentTax,
            decimal billedBackTax,
            decimal unbilledAccruedTax)
        {
            return NonNegative(billedCurrentTax)
                + NonNegative(billedBackTax)
                + NonNegative(unbilledAccruedTax);
        }

        public static decimal TotalReserve(decimal dailyCost, decimal taxReserve)
        {
            return Money.Reserve(dailyCost) + NonNegative(taxReserve);
        }

        public static decimal EffectiveSpendRoom(
            decimal cash,
            decimal dailyCost,
            decimal taxReserve,
            bool isDeductibleCapEx,
            int daysUntilTaxAssessment)
        {
            var operatingReserve = Money.Reserve(dailyCost);
            var effectiveTaxReserve = NonNegative(taxReserve);
            if (isDeductibleCapEx && effectiveTaxReserve > 0m)
            {
                effectiveTaxReserve = decimal.Round(
                    effectiveTaxReserve * (1m - EstimatedTaxRate),
                    2,
                    MidpointRounding.AwayFromZero);
            }

            var above = cash - operatingReserve - effectiveTaxReserve;
            if (above <= 0m)
            {
                return 0m;
            }

            var inShieldWindow = isDeductibleCapEx
                && daysUntilTaxAssessment >= 0
                && daysUntilTaxAssessment <= TaxShieldWindowDays;
            var rate = inShieldWindow ? TaxShieldDaySpend : Money.DaySpend;
            return decimal.Round(above * rate, 2, MidpointRounding.AwayFromZero);
        }

        public static TaxPaymentPick DecidePayment(
            decimal cash,
            decimal dailyCost,
            decimal billedCurrentTax,
            decimal billedBackTax,
            int daysUntilDue = 0)
        {
            var survivalFloor = NonNegative(dailyCost) * MinSurvivalDays;
            var reserveFloor = Money.Reserve(dailyCost);
            var survivalRoom = cash - survivalFloor;
            var reserveRoom = cash - reserveFloor;
            var back = NonNegative(billedBackTax);
            var current = NonNegative(billedCurrentTax);

            if (back > 0m)
            {
                if (survivalRoom >= back)
                {
                    return new TaxPaymentPick("back", back, true);
                }

                if (survivalRoom >= MinTaxPartialStep)
                {
                    return new TaxPaymentPick("back-partial", survivalRoom, true);
                }

                return TaxPaymentPick.None();
            }

            if (current > 0m)
            {
                if (reserveRoom >= current)
                {
                    return new TaxPaymentPick("current", current, true);
                }

                if (daysUntilDue <= UrgentTaxDays)
                {
                    if (survivalRoom >= current)
                    {
                        return new TaxPaymentPick("current", current, true);
                    }

                    if (survivalRoom >= MinTaxPartialStep)
                    {
                        return new TaxPaymentPick("current-partial", survivalRoom, true);
                    }
                }
                else if (reserveRoom >= MinTaxPartialStep)
                {
                    return new TaxPaymentPick("current-partial", reserveRoom, true);
                }
            }

            return TaxPaymentPick.None();
        }

        public static (decimal current, decimal back) Amounts(TaxPaymentPick? pick)
        {
            if (pick == null || !pick.Chosen || pick.Amount <= 0m)
            {
                return (0m, 0m);
            }

            if (pick.Kind.StartsWith("back", StringComparison.Ordinal))
            {
                return (0m, pick.Amount);
            }

            if (pick.Kind.StartsWith("current", StringComparison.Ordinal))
            {
                return (pick.Amount, 0m);
            }

            return (0m, 0m);
        }

        public static string HoldLine(int day)
        {
            return "tax.held version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " reason=survival";
        }

        private static decimal NonNegative(decimal value)
        {
            return value > 0m ? value : 0m;
        }
    }
}

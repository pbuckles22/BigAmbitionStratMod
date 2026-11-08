using System.Collections.Generic;

namespace ChaoticCEO
{
    public sealed class MoneyPick
    {
        public MoneyPick(string action, decimal amount, int dailyPayment, bool chosen)
        {
            Action = action ?? "";
            Amount = amount;
            DailyPayment = dailyPayment;
            Chosen = chosen;
        }

        public string Action { get; }

        public decimal Amount { get; }

        public int DailyPayment { get; }

        public bool Chosen { get; }

        public static MoneyPick None()
        {
            return new MoneyPick("", 0m, 0, false);
        }
    }

    public static class Money
    {
        public const int ReserveDays = 7;

        public const decimal DaySpend = 0.25m;

        public const decimal LoanCover = BankLoan.LoanCover;

        public static decimal Reserve(decimal dailyCost)
        {
            if (dailyCost <= 0m)
            {
                return 0m;
            }

            return dailyCost * ReserveDays;
        }

        public static decimal SpendRoom(decimal cash, decimal dailyCost)
        {
            var above = cash - Reserve(dailyCost);
            if (above <= 0m)
            {
                return 0m;
            }

            return above * DaySpend;
        }

        public static MoneyPick Decide(
            int stage,
            decimal cash,
            decimal dailyCost,
            decimal projectedDailyProfit,
            IReadOnlyList<LoanOffer>? offers,
            LoanBalance? loan,
            decimal bestPaybackRate)
        {
            if (loan != null
                && loan.Remaining > 0m
                && loan.InterestRate > bestPaybackRate)
            {
                return new MoneyPick("repay", loan.Remaining, 0, true);
            }

            if (stage != 1 || offers == null || projectedDailyProfit <= 0m)
            {
                return MoneyPick.None();
            }

            var cover = projectedDailyProfit * LoanCover;
            LoanOffer? winner = null;
            foreach (var offer in offers)
            {
                if (offer == null
                    || offer.Amount < BankLoan.MinimumAmount
                    || offer.DailyPayment <= 0
                    || offer.DailyPayment > cover)
                {
                    continue;
                }

                if (winner == null || offer.Amount >= winner.Amount)
                {
                    winner = offer;
                }
            }

            if (winner == null)
            {
                return MoneyPick.None();
            }

            return new MoneyPick("loan", winner.Amount, winner.DailyPayment, true);
        }
    }
}

using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ChaoticCEO
{
    public sealed class LoanOffer
    {
        public LoanOffer(decimal amount, int dailyPayment)
        {
            Amount = amount;
            DailyPayment = dailyPayment;
        }

        public decimal Amount { get; }

        public int DailyPayment { get; }
    }

    public sealed class LoanBalance
    {
        public LoanBalance(decimal remaining, int dailyInterest, decimal interestRate)
        {
            Remaining = remaining;
            DailyInterest = dailyInterest;
            InterestRate = interestRate;
        }

        public decimal Remaining { get; }

        public int DailyInterest { get; }

        public decimal InterestRate { get; }
    }

    public sealed class LoanTakePick
    {
        public LoanTakePick(bool taken, decimal amount, int dailyPayment, string call)
        {
            Taken = taken;
            Amount = amount;
            DailyPayment = dailyPayment;
            Call = call ?? "";
        }

        public bool Taken { get; }

        public decimal Amount { get; }

        public int DailyPayment { get; }

        public string Call { get; }
    }

    public sealed class LoanRepayPick
    {
        public LoanRepayPick(bool repaid, decimal amount, string call)
        {
            Repaid = repaid;
            Amount = amount;
            Call = call ?? "";
        }

        public bool Repaid { get; }

        public decimal Amount { get; }

        public string Call { get; }
    }

    public static class BankLoan
    {
        public const decimal LoanCover = 0.5m;

        public const decimal MinimumAmount = 500m;

        public static LoanTakePick Take(
            decimal projectedDailyProfit,
            IReadOnlyList<LoanOffer>? offers,
            bool alreadyBorrowed,
            ApiNote? api)
        {
            var call = api == null ? "" : api.Call("loan");
            if (alreadyBorrowed || offers == null || call.Length == 0 || projectedDailyProfit <= 0m)
            {
                return NoneTake();
            }

            var cover = projectedDailyProfit * LoanCover;
            LoanOffer? winner = null;
            foreach (var offer in offers)
            {
                if (offer == null
                    || offer.Amount < MinimumAmount
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
                return NoneTake();
            }

            return new LoanTakePick(true, winner.Amount, winner.DailyPayment, call);
        }

        public static LoanRepayPick Repay(LoanBalance? loan, decimal bestPaybackRate, ApiNote? api)
        {
            var call = api == null ? "" : api.Call("loan repay");
            if (loan == null || loan.Remaining <= 0m || call.Length == 0)
            {
                return NoneRepay();
            }

            if (loan.InterestRate <= bestPaybackRate)
            {
                return NoneRepay();
            }

            return new LoanRepayPick(true, loan.Remaining, call);
        }

        public static string RecordTake(string localAppData, LoanTakePick? pick, int day)
        {
            if (pick == null || !pick.Taken)
            {
                return "";
            }

            var line = "loan.taken version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " amount=" + pick.Amount.ToString(CultureInfo.InvariantCulture)
                + " dailyPayment=" + pick.DailyPayment.ToString(CultureInfo.InvariantCulture)
                + " call=" + pick.Call;
            return AppendOnce(localAppData, line);
        }

        public static string RecordRepay(string localAppData, LoanRepayPick? pick, int day)
        {
            if (pick == null || !pick.Repaid)
            {
                return "";
            }

            var line = "loan.repaid version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " amount=" + pick.Amount.ToString(CultureInfo.InvariantCulture)
                + " call=" + pick.Call;
            return AppendOnce(localAppData, line);
        }

        private static string AppendOnce(string localAppData, string line)
        {
            var path = LoadLog.CeoLogPath(localAppData ?? "");
            if (File.Exists(path))
            {
                foreach (var existing in File.ReadAllLines(path))
                {
                    if (existing == line)
                    {
                        return line;
                    }
                }
            }

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(path, line + System.Environment.NewLine);
            return line;
        }

        private static LoanTakePick NoneTake()
        {
            return new LoanTakePick(false, 0m, 0, "");
        }

        private static LoanRepayPick NoneRepay()
        {
            return new LoanRepayPick(false, 0m, "");
        }
    }

    public sealed class BankLoanWatch
    {
        private object? _seen;

        public bool Due(object? save)
        {
            return save != null && !ReferenceEquals(_seen, save);
        }

        public void Mark(object? save)
        {
            _seen = save;
        }
    }
}

using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class BankLoanTests
{
    [Fact]
    public void BankLoan_takes_the_largest_cover_fit_and_names_the_take_call()
    {
        var api = GameApi.Live();
        var taken = BankLoan.Take(
            projectedDailyProfit: 200m,
            offers: new[]
            {
                new LoanOffer(400m, 10),
                new LoanOffer(500m, 20),
                new LoanOffer(900m, 90),
                new LoanOffer(2000m, 100),
                new LoanOffer(3000m, 101),
            },
            alreadyBorrowed: false,
            api);
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "ceo-loan-" + Guid.NewGuid().ToString("N")));
        try
        {
            var written = BankLoan.RecordTake(dir.FullName, taken, 1);
            var again = BankLoan.RecordTake(dir.FullName, taken, 1);

            Assert.True(taken.Taken);
            Assert.Equal(2000m, taken.Amount);
            Assert.Equal(100, taken.DailyPayment);
            Assert.Equal(BankLoan.LoanCover, 0.5m);
            Assert.Equal("Dialogs.BankDialog.LoanRequest", taken.Call);
            Assert.Equal(taken.Call, api.Call("loan"));
            Assert.Equal(
                "loan.taken version=" + LoadLog.Version
                    + " day=1 amount=2000 dailyPayment=100 call=" + taken.Call,
                written);
            Assert.Equal(written, again);
            Assert.Equal(1, File.ReadAllLines(LoadLog.CeoLogPath(dir.FullName)).Count(line => line.StartsWith("loan.taken ")));
        }
        finally
        {
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void BankLoan_a_later_equal_amount_wins()
    {
        var api = GameApi.Live();
        var taken = BankLoan.Take(
            projectedDailyProfit: 200m,
            offers: new[]
            {
                new LoanOffer(2000m, 100),
                new LoanOffer(2000m, 80),
            },
            alreadyBorrowed: false,
            api);

        Assert.True(taken.Taken);
        Assert.Equal(2000m, taken.Amount);
        Assert.Equal(80, taken.DailyPayment);
    }

    [Fact]
    public void BankLoan_skips_a_second_take_and_a_cover_miss_and_repays_when_interest_beats_payback()
    {
        var api = GameApi.Live();
        var again = BankLoan.Take(
            projectedDailyProfit: 200m,
            offers: new[] { new LoanOffer(2000m, 100) },
            alreadyBorrowed: true,
            api);
        var miss = BankLoan.Take(
            projectedDailyProfit: 100m,
            offers: new[] { new LoanOffer(2000m, 100), new LoanOffer(400m, 10) },
            alreadyBorrowed: false,
            api);
        var tiny = BankLoan.Take(
            projectedDailyProfit: 200m,
            offers: new[] { new LoanOffer(400m, 10) },
            alreadyBorrowed: false,
            api);
        var noCall = BankLoan.Take(
            projectedDailyProfit: 200m,
            offers: new[] { new LoanOffer(2000m, 100) },
            alreadyBorrowed: false,
            api: null);
        var repaid = BankLoan.Repay(
            new LoanBalance(1000m, dailyInterest: 40, interestRate: 0.20m),
            bestPaybackRate: 0.10m,
            api);
        var kept = BankLoan.Repay(
            new LoanBalance(1000m, dailyInterest: 5, interestRate: 0.05m),
            bestPaybackRate: 0.10m,
            api);
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "ceo-loan-" + Guid.NewGuid().ToString("N")));
        try
        {
            var written = BankLoan.RecordRepay(dir.FullName, repaid, 1);

            Assert.False(again.Taken);
            Assert.False(miss.Taken);
            Assert.False(tiny.Taken);
            Assert.False(noCall.Taken);
            Assert.True(repaid.Repaid);
            Assert.Equal(1000m, repaid.Amount);
            Assert.Equal("LoanEntryUi.PayOffLoan", repaid.Call);
            Assert.Equal(repaid.Call, api.Call("loan repay"));
            Assert.False(kept.Repaid);
            Assert.Equal("", kept.Call);
            Assert.Equal(
                "loan.repaid version=" + LoadLog.Version
                    + " day=1 amount=1000 call=" + repaid.Call,
                written);
            Assert.Equal(1, File.ReadAllLines(LoadLog.CeoLogPath(dir.FullName)).Count(line => line.StartsWith("loan.repaid ")));
        }
        finally
        {
            Directory.Delete(dir.FullName, true);
        }
    }
}

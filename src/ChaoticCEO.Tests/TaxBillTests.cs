using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class TaxBillTests
{
    [Fact]
    public void TaxBill_pays_current_and_back_taxes_and_names_both_calls()
    {
        var api = GameApi.Live();
        var paid = TaxBill.Pay(currentDue: 120.5m, backDue: 40m, api);
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "ceo-tax-" + Guid.NewGuid().ToString("N")));
        try
        {
            var written = TaxBill.Record(dir.FullName, paid, 1);
            var again = TaxBill.Record(dir.FullName, paid, 1);

            Assert.True(paid.Paid);
            Assert.Equal(120.5m, paid.Current);
            Assert.Equal(40m, paid.Back);
            Assert.Equal("Helpers.TaxHelper.PayCurrentTaxes", paid.CurrentCall);
            Assert.Equal("Helpers.TaxHelper.PayBackTaxes", paid.BackCall);
            Assert.Equal(paid.CurrentCall, api.Call("taxes pay"));
            Assert.Equal(paid.BackCall, api.Call("taxes back"));
            Assert.Equal(
                "tax.paid version=" + LoadLog.Version
                    + " day=1 current=120.5 back=40 currentCall=" + paid.CurrentCall
                    + " backCall=" + paid.BackCall,
                written);
            Assert.Equal(written, again);
            Assert.Equal(1, File.ReadAllLines(LoadLog.CeoLogPath(dir.FullName)).Count(line => line.StartsWith("tax.paid ")));
        }
        finally
        {
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void TaxBill_skips_a_zero_bill_and_pays_only_the_side_that_is_due()
    {
        var api = GameApi.Live();
        var none = TaxBill.Pay(currentDue: 0m, backDue: 0m, api);
        var currentOnly = TaxBill.Pay(currentDue: 10m, backDue: 0m, api);
        var backOnly = TaxBill.Pay(currentDue: 0m, backDue: 8m, api);
        var noCall = TaxBill.Pay(currentDue: 10m, backDue: 8m, api: null);

        Assert.False(none.Paid);
        Assert.Equal("", none.CurrentCall);
        Assert.Equal("", none.BackCall);
        Assert.True(currentOnly.Paid);
        Assert.Equal(10m, currentOnly.Current);
        Assert.Equal(0m, currentOnly.Back);
        Assert.Equal("Helpers.TaxHelper.PayCurrentTaxes", currentOnly.CurrentCall);
        Assert.Equal("", currentOnly.BackCall);
        Assert.True(backOnly.Paid);
        Assert.Equal(0m, backOnly.Current);
        Assert.Equal(8m, backOnly.Back);
        Assert.Equal("", backOnly.CurrentCall);
        Assert.Equal("Helpers.TaxHelper.PayBackTaxes", backOnly.BackCall);
        Assert.False(noCall.Paid);
    }
}

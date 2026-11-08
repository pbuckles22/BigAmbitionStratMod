using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class RivalTakeoverTests
{
    [Fact]
    public void RivalTakeover_declines_when_the_offer_misses_payback_and_names_the_decline_call()
    {
        var api = GameApi.Live();
        var declined = RivalTakeover.Decline(
            offerPrice: 1000m,
            dailyProfit: 10m,
            street: "ba:street_seventhavenue",
            number: 26,
            alreadyDeclined: false,
            api);
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "ceo-takeover-" + Guid.NewGuid().ToString("N")));
        try
        {
            var written = RivalTakeover.Record(dir.FullName, declined, 1);
            var again = RivalTakeover.Record(dir.FullName, declined, 1);

            Assert.True(declined.Declined);
            Assert.Equal(RivalTakeover.PaybackMax, 365);
            Assert.Equal(1000m, declined.OfferPrice);
            Assert.Equal(10m, declined.DailyProfit);
            Assert.Equal("ba:street_seventhavenue", declined.Street);
            Assert.Equal(26, declined.Number);
            Assert.Equal("UI.Smartphone.Apps.BizMan.RealEstateSettings.CancelForSale", declined.Call);
            Assert.Equal(declined.Call, api.Call("rival takeover decline"));
            Assert.Equal(
                "takeover.declined version=" + LoadLog.Version
                    + " day=1 street=ba:street_seventhavenue number=26 offer=1000 dailyProfit=10 call=" + declined.Call,
                written);
            Assert.Equal(written, again);
            Assert.Equal(1, File.ReadAllLines(LoadLog.CeoLogPath(dir.FullName)).Count(line => line.StartsWith("takeover.declined ")));
        }
        finally
        {
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void RivalTakeover_keeps_an_offer_that_beats_payback_and_skips_a_second_decline()
    {
        var api = GameApi.Live();
        var kept = RivalTakeover.Decline(
            offerPrice: 3650m,
            dailyProfit: 10m,
            street: "ba:street_a",
            number: 1,
            alreadyDeclined: false,
            api);
        var exact = RivalTakeover.Decline(
            offerPrice: 3650m,
            dailyProfit: 10m,
            street: "ba:street_a",
            number: 1,
            alreadyDeclined: false,
            api);
        var justUnder = RivalTakeover.Decline(
            offerPrice: 3649.99m,
            dailyProfit: 10m,
            street: "ba:street_a",
            number: 1,
            alreadyDeclined: false,
            api);
        var again = RivalTakeover.Decline(
            offerPrice: 100m,
            dailyProfit: 10m,
            street: "ba:street_a",
            number: 1,
            alreadyDeclined: true,
            api);
        var noCall = RivalTakeover.Decline(
            offerPrice: 100m,
            dailyProfit: 10m,
            street: "ba:street_a",
            number: 1,
            alreadyDeclined: false,
            api: null);
        var noProfit = RivalTakeover.Decline(
            offerPrice: 100m,
            dailyProfit: 0m,
            street: "ba:street_a",
            number: 1,
            alreadyDeclined: false,
            api);

        Assert.False(kept.Declined);
        Assert.Equal("", kept.Call);
        Assert.False(exact.Declined);
        Assert.True(justUnder.Declined);
        Assert.False(again.Declined);
        Assert.False(noCall.Declined);
        Assert.False(noProfit.Declined);
    }
}

using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class MarketingTests
{
    [Fact]
    public void Marketing_starts_when_daily_cost_is_under_cover_and_names_the_start_call()
    {
        var api = GameApi.Live();
        var started = Marketing.Start(
            dailyCost: 100m,
            gain: 300m,
            alreadyActive: false,
            street: "ba:street_seventhavenue",
            number: 26,
            type: Marketing.StarterType,
            api);
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "ceo-mkt-" + Guid.NewGuid().ToString("N")));
        try
        {
            var written = Marketing.RecordStart(dir.FullName, started, 1);
            var again = Marketing.RecordStart(dir.FullName, started, 1);

            Assert.True(started.Started);
            Assert.Equal(Marketing.MarketingCover, 0.5m);
            Assert.Equal(100m, started.DailyCost);
            Assert.Equal(Marketing.StarterType, started.Type);
            Assert.Equal("ba:street_seventhavenue", started.Street);
            Assert.Equal(26, started.Number);
            Assert.Equal("Dialogs.MarketingAgencyDialog.OnMarketingSettingsSet", started.Call);
            Assert.Equal(started.Call, api.Call("marketing start"));
            Assert.Equal(
                "marketing.started version=" + LoadLog.Version
                    + " day=1 street=ba:street_seventhavenue number=26 type=" + Marketing.StarterType
                    + " dailyCost=100 call=" + started.Call,
                written);
            Assert.Equal(written, again);
            Assert.Equal(1, File.ReadAllLines(LoadLog.CeoLogPath(dir.FullName)).Count(line => line.StartsWith("marketing.started ")));
        }
        finally
        {
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void Marketing_stops_an_active_campaign_and_names_the_stop_call()
    {
        var api = GameApi.Live();
        var stopped = Marketing.Stop(
            alreadyActive: true,
            street: "ba:street_seventhavenue",
            number: 26,
            api);
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "ceo-mkt-" + Guid.NewGuid().ToString("N")));
        try
        {
            var written = Marketing.RecordStop(dir.FullName, stopped, 2);

            Assert.True(stopped.Stopped);
            Assert.Equal("ba:street_seventhavenue", stopped.Street);
            Assert.Equal(26, stopped.Number);
            Assert.Equal("UI.Smartphone.Apps.BizMan.BizManMarketing.CancelAllCampaignsFromAgency", stopped.Call);
            Assert.Equal(stopped.Call, api.Call("marketing stop"));
            Assert.Equal(
                "marketing.stopped version=" + LoadLog.Version
                    + " day=2 street=ba:street_seventhavenue number=26 call=" + stopped.Call,
                written);
            Assert.Equal(1, File.ReadAllLines(LoadLog.CeoLogPath(dir.FullName)).Count(line => line.StartsWith("marketing.stopped ")));
        }
        finally
        {
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void Marketing_skips_a_cover_miss_a_second_start_and_a_blank_stop()
    {
        var api = GameApi.Live();
        var miss = Marketing.Start(
            dailyCost: 200m,
            gain: 300m,
            alreadyActive: false,
            street: "ba:street_a",
            number: 1,
            type: Marketing.StarterType,
            api);
        var atCover = Marketing.Start(
            dailyCost: 150m,
            gain: 300m,
            alreadyActive: false,
            street: "ba:street_a",
            number: 1,
            type: Marketing.StarterType,
            api);
        var again = Marketing.Start(
            dailyCost: 100m,
            gain: 300m,
            alreadyActive: true,
            street: "ba:street_a",
            number: 1,
            type: Marketing.StarterType,
            api);
        var noCall = Marketing.Start(
            dailyCost: 100m,
            gain: 300m,
            alreadyActive: false,
            street: "ba:street_a",
            number: 1,
            type: Marketing.StarterType,
            api: null);
        var blankStop = Marketing.Stop(alreadyActive: false, street: "ba:street_a", number: 1, api);
        var noStopCall = Marketing.Stop(alreadyActive: true, street: "ba:street_a", number: 1, api: null);

        Assert.False(miss.Started);
        Assert.False(atCover.Started);
        Assert.False(again.Started);
        Assert.False(noCall.Started);
        Assert.False(blankStop.Stopped);
        Assert.Equal("", blankStop.Call);
        Assert.False(noStopCall.Stopped);
    }

    [Fact]
    public void Marketing_a_cost_just_under_cover_starts_and_zero_gain_does_not()
    {
        var api = GameApi.Live();
        var under = Marketing.Start(
            dailyCost: 149.99m,
            gain: 300m,
            alreadyActive: false,
            street: "ba:street_b",
            number: 2,
            type: Marketing.StarterType,
            api);
        var zeroGain = Marketing.Start(
            dailyCost: 1m,
            gain: 0m,
            alreadyActive: false,
            street: "ba:street_b",
            number: 2,
            type: Marketing.StarterType,
            api);
        var blankStreet = Marketing.Start(
            dailyCost: 100m,
            gain: 300m,
            alreadyActive: false,
            street: "",
            number: 2,
            type: Marketing.StarterType,
            api);

        Assert.True(under.Started);
        Assert.Equal(149.99m, under.DailyCost);
        Assert.False(zeroGain.Started);
        Assert.False(blankStreet.Started);
    }
}

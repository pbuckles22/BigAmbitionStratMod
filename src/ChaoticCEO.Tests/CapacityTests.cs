using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class CapacityTests
{
    [Fact]
    public void Capacity_matches_the_layout_and_names_the_update_call()
    {
        var api = GameApi.Live();
        var rent = new BuildingRent("ba:street_seventhavenue", 26, true, api.Call("rent"));
        var layout = Starter.Choose(Starter.StageOne(), alreadyOpen: false, api);
        var matched = Capacity.Match(rent, layout, alreadyMatched: false, api);
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "ceo-capacity-" + Guid.NewGuid().ToString("N")));
        try
        {
            var written = Capacity.Record(dir.FullName, matched, 1);
            var again = Capacity.Record(dir.FullName, matched, 1);

            Assert.True(layout.Loaded);
            Assert.True(matched.Matched);
            Assert.Equal("ba:street_seventhavenue", matched.Street);
            Assert.Equal(26, matched.Number);
            Assert.Equal("Helpers.BusinessHelper.UpdateCustomerCapacity", matched.Call);
            Assert.Equal(matched.Call, api.Call("capacity fixtures"));
            Assert.Equal(
                "capacity.matched version=" + LoadLog.Version
                    + " day=1 street=ba:street_seventhavenue number=26 call=" + matched.Call,
                written);
            Assert.Equal(written, again);
            Assert.Equal(1, File.ReadAllLines(LoadLog.CeoLogPath(dir.FullName)).Count(line => line.StartsWith("capacity.matched ")));
        }
        finally
        {
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void Capacity_skips_a_second_match_a_closed_layout_and_a_blank_call()
    {
        var api = GameApi.Live();
        var rent = new BuildingRent("ba:street_seventhavenue", 26, true, api.Call("rent"));
        var layout = Starter.Choose(Starter.StageOne(), alreadyOpen: false, api);
        var again = Capacity.Match(rent, layout, alreadyMatched: true, api);
        var closed = Capacity.Match(
            rent,
            Starter.Choose(Starter.StageOne(), alreadyOpen: true, api),
            alreadyMatched: false,
            api);
        var unrented = Capacity.Match(
            new BuildingRent("ba:street_seventhavenue", 26, false, api.Call("rent")),
            layout,
            alreadyMatched: false,
            api);
        var blankStreet = Capacity.Match(
            new BuildingRent("", 26, true, api.Call("rent")),
            layout,
            alreadyMatched: false,
            api);
        var blankNumber = Capacity.Match(
            new BuildingRent("ba:street_seventhavenue", 0, true, api.Call("rent")),
            layout,
            alreadyMatched: false,
            api);
        var noCall = Capacity.Match(rent, layout, alreadyMatched: false, api: null);

        Assert.False(again.Matched);
        Assert.Equal("", again.Call);
        Assert.False(closed.Matched);
        Assert.False(unrented.Matched);
        Assert.False(blankStreet.Matched);
        Assert.False(blankNumber.Matched);
        Assert.False(noCall.Matched);
        Assert.Equal("", noCall.Call);
    }

    [Fact]
    public void Capacity_miss_skips_an_unmatched_pick()
    {
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "ceo-capacity-miss-skip-" + Guid.NewGuid().ToString("N")));
        try
        {
            var written = Capacity.Miss(dir.FullName, new CapacityPick(false, "ba:street_seventhavenue", 22, "Helpers.BusinessHelper.UpdateCustomerCapacity"), 1575);
            var blank = Capacity.Miss(dir.FullName, null, 1575);

            Assert.Equal("", written);
            Assert.Equal("", blank);
            Assert.False(File.Exists(LoadLog.CeoLogPath(dir.FullName)));
        }
        finally
        {
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void Capacity_a_failed_call_logs_one_miss_and_does_not_spam()
    {
        var api = GameApi.Live();
        var rent = new BuildingRent("ba:street_seventhavenue", 26, true, api.Call("rent"));
        var layout = Starter.Choose(Starter.StageOne(), alreadyOpen: false, api);
        var matched = Capacity.Match(rent, layout, alreadyMatched: false, api);
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "ceo-capacity-miss-" + Guid.NewGuid().ToString("N")));
        try
        {
            var written = Capacity.Miss(dir.FullName, matched, 1016);
            var again = Capacity.Miss(dir.FullName, matched, 1016);
            var watch = new CapacityWatch();
            var save = new object();

            Assert.True(matched.Matched);
            Assert.Equal(
                "capacity.missed version=" + LoadLog.Version
                    + " day=1016 street=ba:street_seventhavenue number=26 call=" + matched.Call,
                written);
            Assert.Equal(written, again);
            Assert.Equal(1, File.ReadAllLines(LoadLog.CeoLogPath(dir.FullName)).Count(line => line.StartsWith("capacity.missed ")));
            Assert.True(watch.Due(save));
            watch.Mark(save);
            Assert.False(watch.Due(save));
        }
        finally
        {
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void Chain_layout_capacity_match_logs_a_miss_when_the_call_fails()
    {
        // Chain: layout → capacity match → miss
        var api = GameApi.Live();
        var rent = new BuildingRent("ba:street_seventhavenue", 22, true, api.Call("rent"));
        var layout = Starter.Choose(Starter.StageOne(), alreadyOpen: false, api);
        var matched = Capacity.Match(rent, layout, alreadyMatched: false, api);
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "ceo-capacity-chain-" + Guid.NewGuid().ToString("N")));
        try
        {
            Assert.True(layout.Loaded);
            Assert.True(matched.Matched);
            Assert.Equal(api.Call("capacity fixtures"), matched.Call);

            var missed = Capacity.Miss(dir.FullName, matched, 1575);
            var watch = new CapacityWatch();
            var save = new object();
            watch.Mark(save);
            var rematch = Capacity.Match(rent, layout, alreadyMatched: CapacityBridge.AlreadyMatched(dir.FullName, rent.Street, rent.Number), api);

            Assert.StartsWith("capacity.missed ", missed);
            Assert.Contains("day=1575", missed);
            Assert.Contains("number=22", missed);
            Assert.Equal(matched.Call, missed.Split(new[] { " call=" }, StringSplitOptions.None)[^1]);
            Assert.False(watch.Due(save));
            Assert.True(rematch.Matched);
            Assert.Equal(1, File.ReadAllLines(LoadLog.CeoLogPath(dir.FullName)).Count(line => line.StartsWith("capacity.missed ")));
            Assert.Equal(0, File.ReadAllLines(LoadLog.CeoLogPath(dir.FullName)).Count(line => line.StartsWith("capacity.matched ")));
        }
        finally
        {
            Directory.Delete(dir.FullName, true);
        }
    }
}

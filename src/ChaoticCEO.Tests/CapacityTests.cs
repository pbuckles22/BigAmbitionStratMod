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
}

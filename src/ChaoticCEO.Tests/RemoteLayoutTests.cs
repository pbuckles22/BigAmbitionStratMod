using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class RemoteLayoutTests
{
    [Fact]
    public void Open_rent_the_player_does_not_enter_still_gets_a_business_type()
    {
        var api = GameApi.Live();
        var rent = new BuildingRent("ba:street_seventhavenue", 15, true, api.Call("rent"));
        var pick = Starter.Choose(Starter.StageOne(), alreadyOpen: false, api);
        var laid = RemoteLayout.Decide(entered: false, rent, "ba:businesstype_empty", pick);

        Assert.True(pick.Loaded);
        Assert.Equal("ba:businesstype_giftshop", laid.BusinessType);
        Assert.True(laid.Applied);
        Assert.False(laid.SilentNoop);
    }

    [Fact]
    public void Apply_wins_when_the_player_is_outside()
    {
        var api = GameApi.Live();
        var rent = new BuildingRent("ba:street_seventhavenue", 15, true, api.Call("rent"));
        var pick = Starter.Choose(Starter.StageOne(), alreadyOpen: false, api);
        var laid = RemoteLayout.Decide(entered: false, rent, "", pick);

        Assert.Equal("ba:businesstype_giftshop", laid.BusinessType);
        Assert.True(laid.Applied);
        Assert.False(laid.SilentNoop);
    }

    [Fact]
    public void Walking_in_still_gets_the_same_business_type()
    {
        var api = GameApi.Live();
        var rent = new BuildingRent("ba:street_seventhavenue", 15, true, api.Call("rent"));
        var pick = Starter.Choose(Starter.StageOne(), alreadyOpen: false, api);
        var outside = RemoteLayout.Decide(entered: false, rent, "", pick);
        var inside = RemoteLayout.Decide(entered: true, rent, "", pick);

        Assert.Equal(outside.BusinessType, inside.BusinessType);
        Assert.Equal("ba:businesstype_giftshop", inside.BusinessType);
        Assert.True(inside.Applied);
    }

    [Fact]
    public void A_loaded_pick_that_leaves_the_type_blank_is_a_silent_noop()
    {
        var dir = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "ceo-remote-layout-noop-" + Guid.NewGuid().ToString("N")));
        try
        {
            var api = GameApi.Live();
            var rent = new BuildingRent("ba:street_seventhavenue", 15, true, api.Call("rent"));
            var blank = new StarterPick("", "GiftShopWholesalers", "Starter Gift", true, api.Call("clone"), 8, 16);
            var laid = RemoteLayout.Decide(entered: false, rent, "", blank);
            var line = RemoteLayout.Open(dir.FullName, rent, entered: false, "", blank, 1);
            var text = File.ReadAllText(LoadLog.CeoLogPath(dir.FullName));

            Assert.False(laid.Applied);
            Assert.True(laid.SilentNoop);
            Assert.Equal("", laid.BusinessType);
            Assert.Contains("SILENT_NOOP", line);
            Assert.Contains("where=layout", line);
            Assert.Contains("street=ba:street_seventhavenue", line);
            Assert.Contains("number=15", line);
            Assert.DoesNotContain("not-inside", line);
            Assert.DoesNotContain("not-inside", text);
            Assert.DoesNotContain("layout.loaded", text);
        }
        finally
        {
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void A_lot_that_is_not_open_does_not_get_a_type()
    {
        var api = GameApi.Live();
        var closed = Rent.Choose(
            new[] { new RentLot("ba:street_seventhavenue", 15, 40m) },
            cash: 15000m,
            anyRented: true,
            api);
        var pick = Starter.Choose(Starter.StageOne(), alreadyOpen: false, api);
        var laid = RemoteLayout.Decide(entered: false, closed, "", pick);

        Assert.False(closed.Rented);
        Assert.False(laid.Applied);
        Assert.False(laid.SilentNoop);
        Assert.Equal("", laid.BusinessType);
    }

    [Fact]
    public void An_unloaded_pick_does_not_pretend_the_type_landed()
    {
        var api = GameApi.Live();
        var rent = new BuildingRent("ba:street_seventhavenue", 15, true, api.Call("rent"));
        var hold = Starter.Choose(Starter.StageOne(), alreadyOpen: true, api);
        var laid = RemoteLayout.Decide(entered: false, rent, "", hold);

        Assert.False(hold.Loaded);
        Assert.False(laid.Applied);
        Assert.False(laid.SilentNoop);
        Assert.Equal("", laid.BusinessType);
    }

    [Fact]
    public void Chain_open_rent_remote_layout()
    {
        // Chain: open rent → remote layout
        var dir = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "ceo-remote-layout-chain-" + Guid.NewGuid().ToString("N")));
        try
        {
            var api = GameApi.Live();
            var rented = Rent.Choose(
                new[]
                {
                    new RentLot("ba:street_high", 2, 80m),
                    new RentLot("ba:street_seventhavenue", 15, 40m),
                },
                cash: 15000m,
                anyRented: false,
                api);
            var rentLine = Rent.Record(dir.FullName, rented, 1);
            var pick = Starter.Choose(Starter.StageOne(), alreadyOpen: false, api);
            var laid = RemoteLayout.Decide(entered: false, rented, "", pick);
            var written = RemoteLayout.Open(dir.FullName, rented, entered: false, "", pick, 1);
            var text = File.ReadAllText(LoadLog.CeoLogPath(dir.FullName));

            Assert.Contains("building.rented ", rentLine);
            Assert.Equal("ba:street_seventhavenue", rented.Street);
            Assert.Equal(15, rented.Number);
            Assert.Equal("ba:businesstype_giftshop", pick.BusinessType);
            Assert.Equal("BusinessLayoutSets.BusinessLayoutSetHelper.LoadBusinessLayoutSet", pick.Call);
            Assert.Equal(pick.BusinessType, laid.BusinessType);
            Assert.True(laid.Applied);
            Assert.Contains("layout.loaded ", written);
            Assert.Contains("street=" + rented.Street, written);
            Assert.Contains("number=15", written);
            Assert.Contains("type=ba:businesstype_giftshop", written);
            Assert.Contains("call=" + pick.Call, written);
            Assert.DoesNotContain("not-inside", text);
            Assert.DoesNotContain("SILENT_NOOP", text);
            Assert.Equal(1, Count(text, written));
        }
        finally
        {
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void Chain_open_rent_remote_layout_when_the_player_is_inside()
    {
        // Chain: open rent → remote layout
        var dir = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "ceo-remote-layout-inside-" + Guid.NewGuid().ToString("N")));
        try
        {
            var api = GameApi.Live();
            var rented = Rent.Choose(
                new[] { new RentLot("ba:street_seventhavenue", 15, 40m) },
                cash: 15000m,
                anyRented: false,
                api);
            var rentLine = Rent.Record(dir.FullName, rented, 1);
            var pick = Starter.Choose(Starter.StageOne(), alreadyOpen: false, api);
            var written = RemoteLayout.Open(dir.FullName, rented, entered: true, "", pick, 1);
            var text = File.ReadAllText(LoadLog.CeoLogPath(dir.FullName));

            Assert.Contains("street=" + rented.Street, rentLine);
            Assert.Contains("type=ba:businesstype_giftshop", written);
            Assert.Contains("call=" + pick.Call, written);
            Assert.Contains(written, text);
            Assert.DoesNotContain("not-inside", text);
            Assert.DoesNotContain("SILENT_NOOP", text);
        }
        finally
        {
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void Chain_open_rent_remote_layout_names_the_noop_when_the_type_stays_blank()
    {
        // Chain: open rent → remote layout
        var dir = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "ceo-remote-layout-chain-noop-" + Guid.NewGuid().ToString("N")));
        try
        {
            var api = GameApi.Live();
            var rented = Rent.Choose(
                new[] { new RentLot("ba:street_seventhavenue", 15, 40m) },
                cash: 15000m,
                anyRented: false,
                api);
            var rentLine = Rent.Record(dir.FullName, rented, 1);
            var pick = Starter.Choose(Starter.StageOne(), alreadyOpen: false, api);
            var written = RemoteLayout.Open(dir.FullName, rented, entered: false, "", pick, 1, "");
            var text = File.ReadAllText(LoadLog.CeoLogPath(dir.FullName));

            Assert.Contains("street=" + rented.Street, rentLine);
            Assert.Contains("SILENT_NOOP", written);
            Assert.Contains("expected=ba:businesstype_giftshop", written);
            Assert.Contains("where=layout", written);
            Assert.Contains("street=" + rented.Street, written);
            Assert.DoesNotContain("not-inside", text);
            Assert.DoesNotContain("layout.loaded", text);
        }
        finally
        {
            Directory.Delete(dir.FullName, true);
        }
    }

    private static int Count(string text, string line)
    {
        var hits = 0;
        foreach (var row in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (row == line)
            {
                hits++;
            }
        }

        return hits;
    }
}

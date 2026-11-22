using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class StoreNameTests
{
    [Fact]
    public void A_second_gift_shop_ends_with_the_neighborhood_initials()
    {
        var hell = Starter.BusinessName(
            "ba:businesstype_giftshop",
            "ba:neighborhood_hellskitchen",
            "ba:street_ninthavenue",
            15);
        var garment = Starter.BusinessName(
            "ba:businesstype_giftshop",
            "ba:neighborhood_garmentdistrict",
            "ba:street_seventhavenue",
            15);

        Assert.Equal("Starter Gift 9AV15 HK", hell);
        Assert.EndsWith(" HK", hell);
        Assert.Equal("Starter Gift 7AV15 GD", garment);
        Assert.EndsWith(" GD", garment);
        Assert.Equal("HK", Starter.Initials("ba:neighborhood_hellskitchen"));
        Assert.Equal("GD", Starter.Initials("ba:neighborhood_garmentdistrict"));
        Assert.Equal("LM", Starter.Initials("Lower Manhattan"));
        Assert.Equal("MH", Starter.Initials("neighborhood_murrayhill"));
        Assert.Equal("M", Starter.Initials("Midtown"));
        Assert.Equal("TH", Starter.Initials("ba:neighborhood_thehamptons"));
        Assert.NotEqual(hell, garment);
    }

    [Fact]
    public void Two_stores_in_one_neighborhood_keep_different_names()
    {
        var seventh = Starter.BusinessName(
            "ba:businesstype_giftshop",
            "ba:neighborhood_garmentdistrict",
            "ba:street_seventhavenue",
            15);
        var ninth = Starter.BusinessName(
            "ba:businesstype_giftshop",
            "ba:neighborhood_garmentdistrict",
            "ba:street_ninthavenue",
            15);

        Assert.Equal("Starter Gift 7AV15 GD", seventh);
        Assert.Equal("Starter Gift 9AV15 GD", ninth);
        Assert.EndsWith(" GD", seventh);
        Assert.EndsWith(" GD", ninth);
        Assert.NotEqual(seventh, ninth);
    }

    [Fact]
    public void A_shared_starter_name_is_replaced_and_a_unique_name_stays()
    {
        var renamed = Starter.Distinct(new[]
        {
            new NamedStore("ba:street_seventhavenue", 15, "ba:neighborhood_garmentdistrict", "ba:businesstype_giftshop", "Starter Gift"),
            new NamedStore("ba:street_ninthavenue", 15, "ba:neighborhood_hellskitchen", "ba:businesstype_giftshop", "Starter Gift"),
            new NamedStore("ba:street_thirdstreet", 4, "Midtown", "ba:businesstype_florist", "Starter Florist 3ST4 M"),
        });

        Assert.Equal("Starter Gift 7AV15 GD", renamed[0].Name);
        Assert.Equal("Starter Gift 9AV15 HK", renamed[1].Name);
        Assert.Equal("Starter Florist 3ST4 M", renamed[2].Name);
        Assert.NotEqual(renamed[0].Name, renamed[1].Name);
    }

    [Fact]
    public void A_blank_neighborhood_still_uses_the_address()
    {
        var name = Starter.BusinessName(
            "ba:businesstype_giftshop",
            "",
            "ba:street_ninthavenue",
            15);

        Assert.Equal("Starter Gift 9AV15", name);
        Assert.NotEqual("Starter Gift", name);
    }

    [Fact]
    public void Chain_open_rent_remote_layout_distinct_name()
    {
        // Chain: open rent → remote layout → distinct name
        var dir = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "ceo-store-name-" + Guid.NewGuid().ToString("N")));
        try
        {
            var api = GameApi.Live();
            var rented = Rent.Choose(
                new[]
                {
                    new RentLot("ba:street_ninthavenue", 15, 40m),
                    new RentLot("ba:street_seventhavenue", 15, 80m),
                },
                cash: 15000m,
                anyRented: false,
                api);
            var pick = Starter.Choose(Starter.StageOne(), alreadyOpen: false, api);
            var laid = RemoteLayout.Decide(entered: false, rented, "", pick);
            var name = Starter.BusinessName(
                laid.BusinessType,
                "ba:neighborhood_hellskitchen",
                rented.Street,
                rented.Number);
            var line = Starter.RecordName(dir.FullName, 16, rented.Street, rented.Number, name);

            Assert.Equal("ba:street_ninthavenue", rented.Street);
            Assert.Equal(15, rented.Number);
            Assert.Equal("ba:businesstype_giftshop", laid.BusinessType);
            Assert.Equal("Starter Gift 9AV15 HK", name);
            Assert.EndsWith(" HK", name);
            Assert.Contains("name=Starter_Gift_9AV15_HK", line);
            Assert.Contains("street=" + rented.Street, line);
            Assert.Contains(line, File.ReadAllText(LoadLog.CeoLogPath(dir.FullName)));
        }
        finally
        {
            Directory.Delete(dir.FullName, true);
        }
    }
}

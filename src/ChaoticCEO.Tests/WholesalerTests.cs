using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class WholesalerTests
{
    private const string GiftWholesale = "ba:street_firststreet 37";

    [Fact]
    public void Wholesaler_names_the_contract()
    {
        var api = GameApi.Live();
        var rent = new BuildingRent("ba:street_seventhavenue", 26, true, api.Call("rent"));
        var layout = Starter.Choose(Starter.StageOne(), alreadyOpen: false, api);
        var signed = Wholesaler.Sign(rent, layout, Wholesaler.StageOne(), alreadySigned: false, api);
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "ceo-wholesale-" + Guid.NewGuid().ToString("N")));
        try
        {
            var written = Wholesaler.Record(dir.FullName, signed, 1);
            var again = Wholesaler.Record(dir.FullName, signed, 1);

            Assert.True(layout.Loaded);
            Assert.Equal("ba:businesstype_giftshop", layout.BusinessType);
            Assert.True(signed.Signed);
            Assert.Equal(GiftWholesale, signed.Wholesale);
            Assert.Equal("ba:street_seventhavenue", signed.Street);
            Assert.Equal(26, signed.Number);
            Assert.Equal("Dialogs.WholesaleStoreManagerDialog.StartContract", signed.Call);
            Assert.Equal(signed.Call, api.Call("wholesaler contract"));
            Assert.Equal(
                "contract.signed version=" + LoadLog.Version
                    + " day=1 street=ba:street_seventhavenue number=26 wholesale=" + GiftWholesale
                    + " call=" + signed.Call,
                written);
            Assert.Equal(written, again);
            Assert.Equal(1, File.ReadAllLines(LoadLog.CeoLogPath(dir.FullName)).Count(line => line.StartsWith("contract.signed ")));
        }
        finally
        {
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void Wholesaler_gift_shop_catalog_covers_GiftShopWholesalers_layout_stock()
    {
        // Inspected from StreamingAssets/BusinessLayouts/GiftShop/*/GiftShopWholesalers.json
        // (cheapgift, cheapflower, sodacan, paperbag, umbrella). Do not guess umbrella.
        var needed = Wholesaler.GiftShopWholesalersStock();
        var catalog = Wholesaler.Catalog("ba:businesstype_giftshop");

        Assert.Equal(
            new[]
            {
                "ba:itemname_cheapgift",
                "ba:itemname_cheapflower",
                "ba:itemname_sodacan",
                "ba:itemname_paperbag",
                "ba:itemname_umbrella",
            },
            needed);
        Assert.True(Wholesaler.Covers(needed, catalog));
        Assert.Contains("ba:itemname_umbrella", catalog);
        Assert.Contains("ba:itemname_paperbag", catalog);
        Assert.Contains("ba:itemname_sodacan", catalog);
        Assert.Contains("ba:itemname_cheapflower", catalog);
        Assert.Contains("ba:itemname_cheapgift", catalog);
    }

    [Fact]
    public void Wholesaler_covers_fails_when_catalog_misses_a_needed_item()
    {
        // Harvest: day-8 reds for umbrella/paperbag/flowers/soda while catalog was gifts-only.
        Assert.False(
            Wholesaler.Covers(
                Wholesaler.GiftShopWholesalersStock(),
                new[] { "ba:itemname_cheapgift", "ba:itemname_expensivegift" }));
        Assert.False(Wholesaler.Covers(Wholesaler.GiftShopWholesalersStock(), null));
        Assert.False(Wholesaler.Covers(null, Wholesaler.Catalog("ba:businesstype_giftshop")));
        Assert.Empty(Wholesaler.Catalog(""));
        Assert.Empty(Wholesaler.Catalog(null));
    }

    [Fact]
    public void Wholesaler_gift_shop_layout_umbrella_is_specialty_cargo()
    {
        Assert.Equal("specialty", Advisor.CargoFamily("ba:itemname_umbrella"));
        Assert.Equal("specialty", Advisor.CargoFamily("ba:itemname_cheapgift"));
        Assert.Equal("food", Advisor.CargoFamily("ba:itemname_sodacan"));
    }

    [Fact]
    public void Wholesaler_a_signed_store_stays_put_and_skips_what_cannot_be_contracted()
    {
        var api = GameApi.Live();
        var rent = new BuildingRent("ba:street_seventhavenue", 26, true, api.Call("rent"));
        var layout = Starter.Choose(Starter.StageOne(), alreadyOpen: false, api);
        var again = Wholesaler.Sign(rent, layout, Wholesaler.StageOne(), alreadySigned: true, api);
        var closed = Wholesaler.Sign(
            rent,
            Starter.Choose(Starter.StageOne(), alreadyOpen: true, api),
            Wholesaler.StageOne(),
            alreadySigned: false,
            api);
        var unrented = Wholesaler.Sign(
            new BuildingRent("ba:street_seventhavenue", 26, false, api.Call("rent")),
            layout,
            Wholesaler.StageOne(),
            alreadySigned: false,
            api);
        var blank = Wholesaler.Sign(
            rent,
            layout,
            new[]
            {
                new WholesalerOffer("ba:businesstype_giftshop", ""),
                new WholesalerOffer("", GiftWholesale),
                new WholesalerOffer("ba:businesstype_lawfirm", GiftWholesale),
                new WholesalerOffer("ba:businesstype_giftshop", GiftWholesale),
            },
            alreadySigned: false,
            api);
        var noCall = Wholesaler.Sign(rent, layout, Wholesaler.StageOne(), alreadySigned: false, api: null);
        var catalog = Assert.Single(Wholesaler.StageOne(), offer => offer.BusinessType == "ba:businesstype_giftshop");

        Assert.False(again.Signed);
        Assert.Equal("", again.Wholesale);
        Assert.Equal("", again.Call);
        Assert.False(closed.Signed);
        Assert.False(unrented.Signed);
        Assert.True(blank.Signed);
        Assert.Equal(GiftWholesale, blank.Wholesale);
        Assert.Equal(api.Call("wholesaler contract"), blank.Call);
        Assert.False(noCall.Signed);
        Assert.Equal(GiftWholesale, catalog.Wholesale);
    }
}

using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class StarterTests
{
    [Fact]
    public void Starter_names_the_business_type_and_the_load_call()
    {
        var api = GameApi.Live();
        var chosen = Starter.Choose(
            new[]
            {
                new StarterOffer("ba:businesstype_florist", "FloristWholesalers", 25m),
                new StarterOffer("ba:businesstype_giftshop", "GiftShopWholesalers", 40m),
                new StarterOffer("ba:businesstype_fastfoodrestaurant", "FastFoodKabob", 30m),
            },
            alreadyOpen: false,
            api);

        Assert.True(chosen.Loaded);
        Assert.Equal("ba:businesstype_giftshop", chosen.BusinessType);
        Assert.Equal("GiftShopWholesalers", chosen.Layout);
        Assert.Equal("Starter Gift", chosen.Name);
        Assert.Equal(8, chosen.OpenFrom);
        Assert.Equal(16, chosen.OpenTo);
        Assert.Equal("BusinessLayoutSets.BusinessLayoutSetHelper.LoadBusinessLayoutSet", chosen.Call);
        Assert.Equal(chosen.Call, api.Call("clone"));
        var catalog = Starter.Choose(Starter.StageOne(), alreadyOpen: false, api);
        Assert.Equal("ba:businesstype_giftshop", catalog.BusinessType);
        Assert.Equal("GiftShopWholesalers", catalog.Layout);
        Assert.Equal(chosen.Call, catalog.Call);
    }

    [Fact]
    public void Starter_a_tie_keeps_the_later_type_and_skips_a_store_that_is_already_open()
    {
        var api = GameApi.Live();
        var tie = Starter.Choose(
            new[]
            {
                new StarterOffer("ba:businesstype_giftshop", "GiftShopWholesalers", 40m),
                new StarterOffer("ba:businesstype_fastfoodrestaurant", "FastFoodKabob", 40m),
            },
            alreadyOpen: false,
            api);
        var open = Starter.Choose(
            new[] { new StarterOffer("ba:businesstype_giftshop", "GiftShopWholesalers", 40m) },
            alreadyOpen: true,
            api);
        var blank = Starter.Choose(
            new[]
            {
                new StarterOffer("", "GiftShopWholesalers", 90m),
                new StarterOffer("ba:businesstype_giftshop", "", 90m),
                new StarterOffer("ba:businesstype_lawfirm", "LawOffice", 100m),
                new StarterOffer("ba:businesstype_florist", "FloristWholesalers", -1m),
                new StarterOffer("ba:businesstype_florist", "FloristWholesalers", 25m),
            },
            alreadyOpen: false,
            api);
        var noCall = Starter.Choose(
            new[] { new StarterOffer("ba:businesstype_giftshop", "GiftShopWholesalers", 40m) },
            alreadyOpen: false,
            api: null);

        Assert.Equal("ba:businesstype_fastfoodrestaurant", tie.BusinessType);
        Assert.Equal("FastFoodKabob", tie.Layout);
        Assert.Equal("Starter Kabob", tie.Name);
        Assert.Equal(8, tie.OpenFrom);
        Assert.Equal(16, tie.OpenTo);
        Assert.Equal(api.Call("clone"), tie.Call);
        Assert.False(open.Loaded);
        Assert.Equal("", open.BusinessType);
        Assert.Equal("", open.Layout);
        Assert.Equal("", open.Call);
        Assert.Equal(0, open.OpenFrom);
        Assert.Equal(0, open.OpenTo);
        Assert.Equal("ba:businesstype_florist", blank.BusinessType);
        Assert.Equal("FloristWholesalers", blank.Layout);
        Assert.False(noCall.Loaded);
        Assert.Equal("", noCall.Call);
    }
}

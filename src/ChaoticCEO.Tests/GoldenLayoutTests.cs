using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class GoldenLayoutTests
{
    [Fact]
    public void Layout_decide_stage_one_uses_the_starter_layout()
    {
        var pick = Layout.Decide(
            1,
            "ba:businesstype_giftshop",
            "small",
            new[]
            {
                Owned("ba:businesstype_giftshop", "small", "GiftShopEmpire", 90),
                Owned("ba:businesstype_florist", "small", "FloristWholesalers", 50),
            },
            Starter.StageOne());

        Assert.True(pick.Chosen);
        Assert.Equal("GiftShopWholesalers", pick.Blueprint);
    }

    [Fact]
    public void Layout_decide_stage_two_picks_the_larger_interior_score_for_that_type_and_size()
    {
        var pick = Layout.Decide(
            2,
            "ba:businesstype_giftshop",
            "small",
            new[]
            {
                Owned("ba:businesstype_giftshop", "small", "GiftShopA", 40),
                Owned("ba:businesstype_giftshop", "small", "GiftShopB", 70),
                Owned("ba:businesstype_giftshop", "large", "GiftShopLarge", 99),
                Owned("ba:businesstype_florist", "small", "FloristWholesalers", 80),
            },
            Starter.StageOne());

        Assert.True(pick.Chosen);
        Assert.Equal("GiftShopB", pick.Blueprint);
    }

    [Fact]
    public void Layout_decide_a_tie_keeps_the_later_layout()
    {
        var pick = Layout.Decide(
            3,
            "ba:businesstype_florist",
            "medium",
            new[]
            {
                Owned("ba:businesstype_florist", "medium", "FloristA", 55),
                Owned("ba:businesstype_florist", "medium", "FloristB", 55),
            },
            Starter.StageOne());

        Assert.True(pick.Chosen);
        Assert.Equal("FloristB", pick.Blueprint);
    }

    [Fact]
    public void Layout_decide_a_size_with_no_golden_uses_the_starter_never_another_size()
    {
        var pick = Layout.Decide(
            2,
            "ba:businesstype_giftshop",
            "large",
            new[]
            {
                Owned("ba:businesstype_giftshop", "small", "GiftShopSmall", 90),
                Owned("ba:businesstype_florist", "large", "FloristLarge", 80),
            },
            Starter.StageOne());

        Assert.True(pick.Chosen);
        Assert.Equal("GiftShopWholesalers", pick.Blueprint);
    }

    [Fact]
    public void Layout_decide_score_zero_is_not_a_golden_layout()
    {
        var pick = Layout.Decide(
            2,
            "ba:businesstype_fastfoodrestaurant",
            "small",
            new[]
            {
                Owned("ba:businesstype_fastfoodrestaurant", "small", "KabobZero", 0),
                Owned("ba:businesstype_fastfoodrestaurant", "small", "KabobLive", 12),
            },
            Starter.StageOne());

        Assert.True(pick.Chosen);
        Assert.Equal("KabobLive", pick.Blueprint);
    }

    [Fact]
    public void Layout_decide_skips_blank_type_size_and_empty_lists()
    {
        var blankType = Layout.Decide(1, "", "small", Array.Empty<OwnedLayout>(), Starter.StageOne());
        var blankSize = Layout.Decide(
            2,
            "ba:businesstype_giftshop",
            "",
            new[] { Owned("ba:businesstype_giftshop", "small", "GiftShopA", 40) },
            Starter.StageOne());
        var missingOwned = Layout.Decide(
            2,
            "ba:businesstype_giftshop",
            "small",
            null,
            Starter.StageOne());
        var noStarter = Layout.Decide(
            1,
            "ba:businesstype_clothingstore",
            "small",
            Array.Empty<OwnedLayout>(),
            Starter.StageOne());

        Assert.False(blankType.Chosen);
        Assert.Equal("", blankType.Blueprint);
        Assert.False(blankSize.Chosen);
        Assert.True(missingOwned.Chosen);
        Assert.Equal("GiftShopWholesalers", missingOwned.Blueprint);
        Assert.False(noStarter.Chosen);
    }

    [Fact]
    public void Layout_decide_stage_four_still_uses_score_for_that_type_and_size()
    {
        var pick = Layout.Decide(
            4,
            "ba:businesstype_giftshop",
            "small",
            new[]
            {
                Owned("ba:businesstype_giftshop", "small", "GiftShopLate", 33),
            },
            Starter.StageOne());

        Assert.True(pick.Chosen);
        Assert.Equal("GiftShopLate", pick.Blueprint);
    }

    [Fact]
    public void Layout_decide_skips_a_blank_blueprint()
    {
        var pick = Layout.Decide(
            2,
            "ba:businesstype_giftshop",
            "small",
            new[]
            {
                Owned("ba:businesstype_giftshop", "small", "", 90),
                Owned("ba:businesstype_giftshop", "small", "GiftShopOk", 20),
            },
            Starter.StageOne());

        Assert.True(pick.Chosen);
        Assert.Equal("GiftShopOk", pick.Blueprint);
    }

    private static OwnedLayout Owned(string type, string size, string blueprint, int score)
    {
        return new OwnedLayout(type, size, blueprint, score);
    }
}

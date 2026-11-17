using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class LayoutSizeTests
{
    private const string Gift = "ba:businesstype_giftshop";

    private const string Clothing = "ba:businesstype_clothingstore";

    private const string Electronics = "ba:businesstype_electronicsstore";

    [Fact]
    public void Layout_decide_an_a1_layout_is_never_loaded_onto_a_b1()
    {
        var pick = Layout.Decide(
            2,
            Gift,
            "B1",
            new[] { new OwnedLayout(Gift, "A1", "GiftShopA1", 90) },
            Starter.StageOne());

        Assert.False(pick.Chosen);
        Assert.Equal("", pick.Blueprint);
        Assert.NotEqual("GiftShopA1", pick.Blueprint);
        Assert.NotEqual("GiftShopWholesalers", pick.Blueprint);
    }

    [Fact]
    public void Chain_a_size_code_chooses_the_layout_for_that_code()
    {
        // Chain: size code → layout
        var size = "B1";
        var layout = Layout.Decide(
            2,
            Gift,
            size,
            new[] { new OwnedLayout(Gift, "A1", "GiftShopA1", 90) },
            Starter.StageOne());
        var step = Playbook.Decide(null, null, layout, null, null, null, null, null, null, null, null, null);

        Assert.False(layout.Chosen);
        Assert.Equal("", layout.Blueprint);
        Assert.False(step.Chosen);
        Assert.NotEqual("GiftShopWholesalers", step.Detail);
        Assert.NotEqual("GiftShopA1", step.Detail);
    }

    [Fact]
    public void Layout_decide_a_missing_size_uses_that_sizes_starter()
    {
        var pick = Layout.Decide(
            2,
            Gift,
            "C2",
            new[] { new OwnedLayout(Gift, "A1", "GiftShopA1", 99) },
            Starter.StageOne());

        Assert.True(pick.Chosen);
        Assert.Equal("GiftShopImporters", pick.Blueprint);
        Assert.NotEqual("GiftShopWholesalers", pick.Blueprint);
        Assert.NotEqual("GiftShopA1", pick.Blueprint);
    }

    [Fact]
    public void Chain_sku_scope_keeps_an_electronics_store_closed()
    {
        // Chain: sku scope → open
        var allowed = SkuScope.IsBusinessAllowed(Electronics, factoryPipeline: false);
        var electronics = new OpenCandidate(Electronics, "goods", 200m, true, "durables", 0, true);
        var clothing = new OpenCandidate(Clothing, "goods", 40m, true, "durables", 0, true);
        var open = Open.Decide(2, new[] { electronics, clothing }, 0m);
        var step = Playbook.Decide(open, null, null, null, null, null, null, null, null, null, null, null);

        Assert.False(allowed);
        Assert.Equal(Clothing, open.Type);
        Assert.True(open.Chosen);
        Assert.NotEqual(Electronics, open.Type);
        Assert.True(step.Chosen);
        Assert.Equal("open", step.Kind);
        Assert.Equal(Clothing, step.Detail);
    }

    [Fact]
    public void Sku_scope_factory_locked_phones_earbuds_headphones_and_smartwatch_are_not_ordered()
    {
        var ordered = SkuScope.Order(
            new[]
            {
                "ba:itemname_smartphone1",
                "ba:itemname_earbuds01",
                "ba:itemname_headphones",
                "ba:itemname_smartwatch",
                "ba:itemname_cheapjewelry",
            },
            factoryPipeline: false);

        Assert.Equal(new[] { "ba:itemname_cheapjewelry" }, ordered);
        Assert.False(SkuScope.IsBusinessAllowed(Electronics, factoryPipeline: false));
        Assert.True(SkuScope.IsBusinessAllowed(Clothing, factoryPipeline: false));
    }

    [Fact]
    public void Open_decide_a_lone_electronics_store_stays_closed_while_the_factory_is_off()
    {
        var open = Open.Decide(
            2,
            new[] { new OpenCandidate(Electronics, "goods", 200m, true, "durables", 0, true) },
            0m);

        Assert.False(open.Chosen);
        Assert.Equal("", open.Type);
    }

    [Fact]
    public void Sku_scope_an_open_factory_orders_the_phone_and_opens_the_store()
    {
        var ordered = SkuScope.Order(
            new[] { "ba:itemname_smartphone1", "ba:itemname_cheapjewelry" },
            factoryPipeline: true);
        var open = Open.Decide(
            2,
            new[]
            {
                new OpenCandidate(Electronics, "goods", 200m, true, "durables", 0, true),
                new OpenCandidate(Clothing, "goods", 40m, true, "durables", 0, true),
            },
            0m,
            factoryPipeline: true);

        Assert.Equal(new[] { "ba:itemname_smartphone1", "ba:itemname_cheapjewelry" }, ordered);
        Assert.True(SkuScope.IsBusinessAllowed(Electronics, factoryPipeline: true));
        Assert.Equal(Electronics, open.Type);
    }

    [Fact]
    public void Sku_scope_the_game_ids_for_phones_headphones_and_watches_stay_unorderable()
    {
        var ordered = SkuScope.Order(
            new[]
            {
                "ba:itemname_smartphone2",
                "ba:itemname_headphones01",
                "ba:itemname_smartwatch1",
                "ba:itemname_smartwatch2",
                "ba:itemname_cheapjewelry",
            },
            factoryPipeline: false);

        Assert.Equal(new[] { "ba:itemname_cheapjewelry" }, ordered);
    }

    [Fact]
    public void Chain_the_open_type_uses_the_size_code_starter()
    {
        // Chain: open → size code → layout
        var open = Open.Decide(1, Open.StageOneCandidates(Starter.StageOne(), null, ""), 0m);
        var layout = Layout.Decide(
            2,
            open.Type,
            "C2",
            new[] { new OwnedLayout(open.Type, "A1", "GiftShopA1", 99) },
            Starter.StageOne());
        var step = Playbook.Decide(null, null, layout, null, null, null, null, null, null, null, null, null);

        Assert.Equal(Gift, open.Type);
        Assert.Equal("GiftShopImporters", layout.Blueprint);
        Assert.True(step.Chosen);
        Assert.Equal("layout", step.Kind);
        Assert.Equal("GiftShopImporters", step.Detail);
    }
}

using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class CloseListTests
{
    private const string Warehouse = "ba:businesstype_warehouse";

    private const string Headquarters = "ba:businesstype_headquarters";

    private const string Gift = "ba:businesstype_giftshop";

    private const string Food = "ba:businesstype_fastfoodrestaurant";

    private const string Clothing = "ba:businesstype_clothingstore";

    private const string Office = "ba:businesstype_lawfirm";

    private const string Hair = "ba:businesstype_hairdresser";

    [Fact]
    public void Close_filter_a_warehouse_does_not_close()
    {
        var pick = Close.Filter(
            Warehouse,
            suppliedDays: Close.FixDays,
            staffedDays: Close.FixDays,
            redProfitDays: Close.RedDays,
            delivered: true,
            staffed: true);

        Assert.False(pick.Chosen);
        Assert.Equal("", pick.Action);
    }

    [Fact]
    public void Chain_a_goods_type_that_meets_the_gates_is_the_playbook_close()
    {
        // Chain: type → close filter → playbook close
        var pick = Close.Filter(
            Gift,
            suppliedDays: Close.FixDays,
            staffedDays: Close.FixDays,
            redProfitDays: Close.RedDays,
            delivered: true,
            staffed: true);
        var decided = Close.Decide(
            "goods",
            suppliedDays: Close.FixDays,
            staffedDays: Close.FixDays,
            redProfitDays: Close.RedDays,
            supplied: true,
            staffed: true);
        var step = Playbook.Decide(null, null, null, null, null, null, null, null, null, null, pick, null);

        Assert.Equal(decided.Action, pick.Action);
        Assert.Equal(decided.Chosen, pick.Chosen);
        Assert.True(step.Chosen);
        Assert.Equal("close", step.Kind);
        Assert.Equal("close", step.Detail);
        Assert.Equal("Next close close", Playbook.Line(step));
    }

    [Fact]
    public void Close_filter_a_goods_store_with_no_delivery_does_not_close()
    {
        var pick = Close.Filter(
            Food,
            suppliedDays: Close.FixDays,
            staffedDays: Close.FixDays,
            redProfitDays: Close.RedDays,
            delivered: false,
            staffed: true);

        Assert.True(pick.Chosen);
        Assert.Equal("connect", pick.Action);
        Assert.NotEqual("close", pick.Action);
    }

    [Fact]
    public void Close_filter_a_headquarters_and_an_office_do_not_close()
    {
        var headquarters = Close.Filter(
            Headquarters,
            suppliedDays: Close.FixDays,
            staffedDays: Close.FixDays,
            redProfitDays: Close.RedDays,
            delivered: true,
            staffed: true);
        var office = Close.Filter(
            Office,
            suppliedDays: Close.FixDays,
            staffedDays: Close.FixDays,
            redProfitDays: Close.RedDays,
            delivered: true,
            staffed: true);

        Assert.False(headquarters.Chosen);
        Assert.Equal("", headquarters.Action);
        Assert.False(office.Chosen);
        Assert.Equal("", office.Action);
    }

    [Fact]
    public void Close_filter_waits_out_a_short_red_streak()
    {
        var pick = Close.Filter(
            Gift,
            suppliedDays: Close.FixDays,
            staffedDays: Close.FixDays,
            redProfitDays: Close.RedDays - 1,
            delivered: true,
            staffed: true);

        Assert.False(pick.Chosen);
        Assert.Equal("", pick.Action);
    }

    [Fact]
    public void Chain_an_unstaffed_goods_store_is_a_hire_and_not_a_close()
    {
        // Chain: type → close filter → playbook close
        var pick = Close.Filter(
            Clothing,
            suppliedDays: Close.FixDays,
            staffedDays: 0,
            redProfitDays: Close.RedDays,
            delivered: true,
            staffed: false);
        var decided = Close.Decide(
            "goods",
            suppliedDays: Close.FixDays,
            staffedDays: 0,
            redProfitDays: Close.RedDays,
            supplied: true,
            staffed: false);
        var step = Playbook.Decide(null, null, null, null, null, null, null, null, null, null, pick, null);

        Assert.Equal("hire", decided.Action);
        Assert.Equal(decided.Action, pick.Action);
        Assert.Equal("close", step.Kind);
        Assert.Equal("hire", step.Detail);
        Assert.NotEqual("close", step.Detail);
    }

    [Fact]
    public void Close_filter_a_hairdresser_does_not_close()
    {
        var pick = Close.Filter(
            Hair,
            suppliedDays: Close.FixDays,
            staffedDays: Close.FixDays,
            redProfitDays: Close.RedDays,
            delivered: true,
            staffed: true);
        var both = Close.Filter(
            Food,
            suppliedDays: 0,
            staffedDays: 0,
            redProfitDays: Close.RedDays,
            delivered: false,
            staffed: false);

        Assert.False(pick.Chosen);
        Assert.Equal("", pick.Action);
        Assert.Equal("connect", both.Action);
    }

    [Fact]
    public void Chain_the_close_pick_beats_an_open()
    {
        // Chain: type → close filter → playbook close
        var pick = Close.Filter(
            Gift,
            suppliedDays: Close.FixDays,
            staffedDays: Close.FixDays,
            redProfitDays: Close.RedDays,
            delivered: true,
            staffed: true);
        var step = Playbook.Decide(
            new OpenPick(Gift, true),
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            pick,
            new RunPick("cleaner", true));

        Assert.Equal("close", pick.Action);
        Assert.Equal("close", step.Kind);
        Assert.Equal("close", step.Detail);
    }
}

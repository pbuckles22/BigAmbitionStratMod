using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class CloseDecideTests
{
    [Fact]
    public void Close_names_fix_and_red_days()
    {
        Assert.Equal(7, Close.FixDays);
        Assert.Equal(7, Close.RedDays);
    }

    [Fact]
    public void Close_decide_never_closes_a_warehouse_headquarters_or_office()
    {
        var warehouse = Close.Decide(
            "warehouse",
            suppliedDays: Close.FixDays,
            staffedDays: Close.FixDays,
            redProfitDays: Close.RedDays,
            supplied: true,
            staffed: true);
        var headquarters = Close.Decide(
            "headquarters",
            suppliedDays: Close.FixDays,
            staffedDays: Close.FixDays,
            redProfitDays: Close.RedDays,
            supplied: true,
            staffed: true);
        var office = Close.Decide(
            "office",
            suppliedDays: Close.FixDays,
            staffedDays: Close.FixDays,
            redProfitDays: Close.RedDays,
            supplied: true,
            staffed: true);

        Assert.False(warehouse.Chosen);
        Assert.Equal("", warehouse.Action);
        Assert.False(headquarters.Chosen);
        Assert.Equal("", headquarters.Action);
        Assert.False(office.Chosen);
        Assert.Equal("", office.Action);
    }

    [Fact]
    public void Close_decide_closes_a_goods_store_after_fix_and_red_days()
    {
        var pick = Close.Decide(
            "goods",
            suppliedDays: Close.FixDays,
            staffedDays: Close.FixDays,
            redProfitDays: Close.RedDays,
            supplied: true,
            staffed: true);

        Assert.True(pick.Chosen);
        Assert.Equal("close", pick.Action);
    }

    [Fact]
    public void Close_decide_waits_until_fix_days_of_supply_and_staff()
    {
        var shortSupply = Close.Decide(
            "goods",
            suppliedDays: Close.FixDays - 1,
            staffedDays: Close.FixDays,
            redProfitDays: Close.RedDays,
            supplied: true,
            staffed: true);
        var shortStaff = Close.Decide(
            "goods",
            suppliedDays: Close.FixDays,
            staffedDays: Close.FixDays - 1,
            redProfitDays: Close.RedDays,
            supplied: true,
            staffed: true);
        var shortRed = Close.Decide(
            "goods",
            suppliedDays: Close.FixDays,
            staffedDays: Close.FixDays,
            redProfitDays: Close.RedDays - 1,
            supplied: true,
            staffed: true);

        Assert.False(shortSupply.Chosen);
        Assert.Equal("", shortSupply.Action);
        Assert.False(shortStaff.Chosen);
        Assert.Equal("", shortStaff.Action);
        Assert.False(shortRed.Chosen);
        Assert.Equal("", shortRed.Action);
    }

    [Fact]
    public void Close_decide_an_unsupplied_store_gets_connected()
    {
        var pick = Close.Decide(
            "goods",
            suppliedDays: 0,
            staffedDays: Close.FixDays,
            redProfitDays: Close.RedDays,
            supplied: false,
            staffed: true);

        Assert.True(pick.Chosen);
        Assert.Equal("connect", pick.Action);
    }

    [Fact]
    public void Close_decide_an_unstaffed_store_gets_hired()
    {
        var pick = Close.Decide(
            "goods",
            suppliedDays: Close.FixDays,
            staffedDays: 0,
            redProfitDays: Close.RedDays,
            supplied: true,
            staffed: false);

        Assert.True(pick.Chosen);
        Assert.Equal("hire", pick.Action);
    }

    [Fact]
    public void Close_decide_connect_beats_hire_when_both_are_missing()
    {
        var pick = Close.Decide(
            "goods",
            suppliedDays: 0,
            staffedDays: 0,
            redProfitDays: Close.RedDays,
            supplied: false,
            staffed: false);

        Assert.True(pick.Chosen);
        Assert.Equal("connect", pick.Action);
    }

    [Fact]
    public void Close_decide_heal_beats_close_while_the_store_is_broken()
    {
        var pick = Close.Decide(
            "goods",
            suppliedDays: Close.FixDays,
            staffedDays: Close.FixDays,
            redProfitDays: Close.RedDays,
            supplied: false,
            staffed: true);

        Assert.True(pick.Chosen);
        Assert.Equal("connect", pick.Action);
        Assert.NotEqual("close", pick.Action);
    }

    [Fact]
    public void Close_decide_skips_blank_kind()
    {
        var blank = Close.Decide(
            "",
            suppliedDays: Close.FixDays,
            staffedDays: Close.FixDays,
            redProfitDays: Close.RedDays,
            supplied: true,
            staffed: true);
        var missing = Close.Decide(
            null,
            suppliedDays: Close.FixDays,
            staffedDays: Close.FixDays,
            redProfitDays: Close.RedDays,
            supplied: true,
            staffed: true);

        Assert.False(blank.Chosen);
        Assert.False(missing.Chosen);
    }

    [Fact]
    public void Close_decide_a_service_store_is_not_a_goods_close_step()
    {
        var pick = Close.Decide(
            "service",
            suppliedDays: Close.FixDays,
            staffedDays: Close.FixDays,
            redProfitDays: Close.RedDays,
            supplied: true,
            staffed: true);

        Assert.False(pick.Chosen);
        Assert.Equal("", pick.Action);
    }
}

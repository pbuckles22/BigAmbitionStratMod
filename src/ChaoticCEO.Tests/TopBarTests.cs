using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class TopBarTests
{
    [Fact]
    public void TopBar_names_day_cash_net_worth_profit_stage_autopilot_and_speed()
    {
        var line = TopBar.Line(1016, 1000m, 2500000m, 7m, 1, true, 3);

        Assert.Equal(
            "Day 1016  Cash 1000  Net worth 2500000 / 100000000  Profit 7  Stage 1  Autopilot on  Speed 3",
            line);
        Assert.Equal(100000000m, TopBar.WinNetWorth);
    }

    [Fact]
    public void TopBar_says_off_when_autopilot_is_off()
    {
        var line = TopBar.Line(1016, 1000m, TopBar.WinNetWorth, 7m, 4, false, 1);

        Assert.Contains("Autopilot off", line);
        Assert.DoesNotContain("Autopilot on", line);
        Assert.Contains("Net worth 100000000 / 100000000", line);
    }

    [Fact]
    public void TopBar_clock_is_zero_when_paused()
    {
        Assert.Equal(0, TopBar.Clock(paused: true, rate: 3));
        Assert.Equal(3, TopBar.Clock(paused: false, rate: 3));
        Assert.Equal(0, TopBar.Clock(paused: false, rate: null));
    }

    [Fact]
    public void TopBar_profit_sums_every_statement_row()
    {
        var csv = "day,street,number,sales,goods,wages,rent,marketing,theft,profit\n"
            + "1015,a,1,0,0,0,0,0,0,3\n"
            + "1015,b,2,0,0,0,0,0,0,4\n";
        var line = TopBar.From("day.rolled version=1.17.11.11 day=1016", csv, 10m, 20m, 1, true, 1);

        Assert.Equal(7m, TopBar.Profit(csv));
        Assert.Equal(
            "Day 1016  Cash 10  Net worth 20 / 100000000  Profit 7  Stage 1  Autopilot on  Speed 1",
            line);
    }

    [Fact]
    public void Stage_read_counts_the_store_on_the_plan()
    {
        var needs = new[] { new GoodsNeed("ba:street_fourthavenue 28", "ba:itemname_haircareproduct") };
        var open = Stage.Read(
            1016,
            2000000m,
            needs,
            Array.Empty<DeliveryStop>(),
            Array.Empty<WarehouseHold>(),
            Array.Empty<GarageSlot>());
        var stops = new[]
        {
            new DeliveryStop("W11", "ba:street_fourthavenue 28", "ba:itemname_haircareproduct", null, ""),
        };
        var supplied = Stage.Read(
            1016,
            2000000m,
            needs,
            stops,
            Array.Empty<WarehouseHold>(),
            Array.Empty<GarageSlot>());

        Assert.Equal(1, open);
        Assert.Equal(4, supplied);
    }

    [Fact]
    public void TopBar_yesterday_is_the_prior_day()
    {
        var days = new[]
        {
            new FinancialDay(1015, new[] { new StatementRow("a", 1, 0f, 0f, 0f, 0f, 0f, 0f, 7f) }),
            new FinancialDay(1016, new[] { new StatementRow("a", 1, 0f, 0f, 0f, 0f, 0f, 0f, 99f) }),
        };

        Assert.Equal(7m, TopBar.Yesterday(days, 1016));
        Assert.Equal(0m, TopBar.Yesterday(days, 1015));
    }

    [Fact]
    public void EconoView_yesterday_wins_over_the_business_line()
    {
        Assert.Equal(1547592m, TopBar.ShownProfit(1547592m, 1360666.1249m));
        Assert.Equal(15879949m, TopBar.Dollar(15879949.4m));
    }
}

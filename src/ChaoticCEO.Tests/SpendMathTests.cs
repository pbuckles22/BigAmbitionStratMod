using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class SpendMathTests
{
    [Fact]
    public void Day64_names_the_gift_shop_the_warehouse_and_the_wallet_gap()
    {
        var lines = SpendMath.Lines(
            64,
            new[] { GiftShop(), Warehouse() },
            new[]
            {
                new SpendItem("7th Avenue", 15, "retail", "goods", "cheapgift", 704.0386m),
                new SpendItem("7th Avenue", 15, "retail", "sales", "cheapgift", 2790m),
            },
            null,
            null,
            new SpendEmpire(1935.9614m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 1935.9614m),
            null,
            12300.51m,
            3783.681m);

        var version = LoadLog.Version;
        Assert.Equal(
            new[]
            {
                "spend.store version=" + version + " day=64 street=7th_Avenue number=15 kind=retail sales=2790 goods=704.0386 wages=0 rent=0 marketing=100 theft=0 license=0 ongoing=100 profit=1985.9614 check=1985.9614 gap=0",
                "spend.store version=" + version + " day=64 street=Warehouse number=2 kind=cost sales=0 goods=0 wages=50 rent=0 marketing=0 theft=0 license=0 ongoing=50 profit=-50 check=-50 gap=0",
                "spend.item version=" + version + " day=64 street=7th_Avenue number=15 kind=retail side=sales name=cheapgift amount=2790",
                "spend.item version=" + version + " day=64 street=7th_Avenue number=15 kind=retail side=goods name=cheapgift amount=704.0386",
                "spend.roll version=" + version + " day=64 retail=1985.9614 service=0 cost=-50 other=0 stores=1935.9614",
                "spend.empire version=" + version + " day=64 business=1935.9614 realEstate=0 residential=0 loan=0 parking=0 unassigned=0 salary=0 insurance=0 headhunter=0 interest=0 total=1935.9614 check=1935.9614 gap=0 businessGap=0",
                "spend.cash version=" + version + " day=64 open=12300.51 close=3783.681 delta=-8516.829 moves=0 gap=-8516.829",
            },
            lines);
    }

    [Fact]
    public void Interest_is_named_and_stays_out_of_the_total()
    {
        var lines = SpendMath.Lines(
            4,
            null,
            null,
            null,
            null,
            new SpendEmpire(10m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 3m, 10m),
            null,
            0m,
            0m);

        Assert.Contains(
            "spend.empire version=" + LoadLog.Version + " day=4 business=10 realEstate=0 residential=0 loan=0 parking=0 unassigned=0 salary=0 insurance=0 headhunter=0 interest=3 total=10 check=10 gap=0 businessGap=10",
            lines);
    }

    [Fact]
    public void License_and_theft_are_in_the_ongoing_subtraction()
    {
        var lines = SpendMath.Lines(
            4,
            new[] { new SpendStore("Main", 1, "retail", 20m, 0m, 0m, 0m, 0m, 1m, 4m, 15m) },
            null,
            null,
            null,
            null,
            null,
            0m,
            0m);

        Assert.Contains(
            "spend.store version=" + LoadLog.Version + " day=4 street=Main number=1 kind=retail sales=20 goods=0 wages=0 rent=0 marketing=0 theft=1 license=4 ongoing=5 profit=15 check=15 gap=0",
            lines);
    }

    [Fact]
    public void Profit_that_is_not_sales_minus_costs_names_the_gap()
    {
        var lines = SpendMath.Lines(
            4,
            new[] { new SpendStore("Main", 1, "retail", 10m, 3m, 2m, 0m, 0m, 0m, 0m, 1m) },
            null,
            null,
            null,
            null,
            null,
            0m,
            0m);

        Assert.Contains(
            "spend.store version=" + LoadLog.Version + " day=4 street=Main number=1 kind=retail sales=10 goods=3 wages=2 rent=0 marketing=0 theft=0 license=0 ongoing=2 profit=1 check=5 gap=-4",
            lines);
    }

    [Fact]
    public void Moves_that_sum_to_the_wallet_leave_no_gap()
    {
        var lines = SpendMath.Lines(
            64,
            null,
            null,
            null,
            null,
            null,
            new[]
            {
                new SpendMove("ba:transaction_purchase", "ba:transactioncategory_marketing", "", 0, "player", -10m),
                new SpendMove("ba:transaction_purchase", "", "7th Avenue", 15, "retail", -50m),
            },
            100m,
            40m);

        var version = LoadLog.Version;
        Assert.Equal(
            new[]
            {
                "spend.roll version=" + version + " day=64 retail=0 service=0 cost=0 other=0 stores=0",
                "spend.empire version=" + version + " day=64 business=0 realEstate=0 residential=0 loan=0 parking=0 unassigned=0 salary=0 insurance=0 headhunter=0 interest=0 total=0 check=0 gap=0 businessGap=0",
                "spend.move version=" + version + " day=64 type=ba:transaction_purchase cats=ba:transactioncategory_marketing street= number=0 kind=player amount=-10",
                "spend.move version=" + version + " day=64 type=ba:transaction_purchase cats= street=7th_Avenue number=15 kind=retail amount=-50",
                "spend.cash version=" + version + " day=64 open=100 close=40 delta=-60 moves=-60 gap=0",
            },
            lines);
    }

    [Fact]
    public void Missing_open_does_not_invent_a_delta()
    {
        var lines = SpendMath.Lines(
            63,
            null,
            null,
            null,
            null,
            null,
            new[] { new SpendMove("rent", "", "", 0, "player", -10m) },
            null,
            40m);

        Assert.Contains(
            "spend.cash version=" + LoadLog.Version + " day=63 open=missing close=40 delta=missing moves=-10 gap=missing",
            lines);
    }

    [Fact]
    public void Kind_splits_retail_service_and_cost()
    {
        Assert.Equal("specialty", StoreFamily.For("ba:businesstype_giftshop"));
        Assert.Equal("retail", SpendKind.Of("ba:businesstype_giftshop"));
        Assert.Equal("retail", SpendKind.Of("specialty"));
        Assert.Equal("service", SpendKind.Of("ba:businesstype_hairdresser"));
        Assert.Equal("cost", SpendKind.Of("ba:businesstype_wholesalestore"));
        Assert.Equal("cost", SpendKind.Of("ba:businesstype_lawfirm"));
        Assert.Equal("cost", SpendKind.Of("ba:businesstype_headquarters"));
        Assert.Equal("cost", SpendKind.Of("hq"));
        Assert.Equal("other", SpendKind.Of(""));
        Assert.Equal("other", SpendKind.Of("ba:businesstype_unknown"));
    }

    [Fact]
    public void Remembered_close_is_the_next_open()
    {
        SpendCash.Reset();
        Assert.Null(SpendCash.Take(12300.51m));
        Assert.Equal(12300.51m, SpendCash.Take(3783.681m));
        SpendCash.Reset();
        Assert.Null(SpendCash.Take(1m));
    }

    [Fact]
    public void Missing_summary_names_the_block()
    {
        Assert.Equal(
            "spend.blocked version=" + LoadLog.Version + " day=63 reason=no_summary",
            SpendMath.Blocked(63, "no_summary"));
    }

    [Fact]
    public void Chain_statements_name_the_store_and_the_wallet_gap()
    {
        // Chain: statements → daily cost → spend kind → named spend → cash gap
        var days = new[]
        {
            new FinancialDay(
                64,
                new[] { new StatementRow("7th Avenue", 15, 2790f, 700f, 0f, 0f, 100f, 0f, 1990f) }),
        };
        var cost = DailyCost.Read(days, today: 65, loanPayments: 0m, truckCost: 0m);
        var kind = SpendKind.Of(StoreFamily.For("ba:businesstype_giftshop"));
        var row = days[0].Rows[0];
        var lines = SpendMath.Lines(
            64,
            new[]
            {
                new SpendStore(
                    row.Street,
                    row.Number,
                    kind,
                    (decimal)row.Sales,
                    (decimal)row.Goods,
                    cost.Wages,
                    cost.Rent,
                    cost.Marketing,
                    (decimal)row.Theft,
                    0m,
                    (decimal)row.Profit),
            },
            null,
            null,
            null,
            new SpendEmpire((decimal)row.Profit, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, (decimal)row.Profit),
            null,
            12300.51m,
            3783.681m);

        Assert.Equal("retail", kind);
        Assert.Equal(100m, cost.Marketing);
        Assert.Contains(
            "spend.store version=" + LoadLog.Version + " day=64 street=7th_Avenue number=15 kind=retail sales=2790 goods=700 wages=0 rent=0 marketing=100 theft=0 license=0 ongoing=100 profit=1990 check=1990 gap=0",
            lines);
        Assert.Contains(
            "spend.cash version=" + LoadLog.Version + " day=64 open=12300.51 close=3783.681 delta=-8516.829 moves=0 gap=-8516.829",
            lines);
    }

    [Fact]
    public void Chain_family_splits_retail_from_the_cost_center()
    {
        // Chain: store family → spend kind → retail and cost → empire check
        var retail = SpendKind.Of(StoreFamily.For("ba:businesstype_giftshop"));
        var cost = SpendKind.Of(StoreFamily.For("ba:businesstype_wholesalestore"));
        var service = SpendKind.Of(StoreFamily.For("ba:businesstype_hairdresser"));
        var lines = SpendMath.Lines(
            8,
            new[]
            {
                new SpendStore("Dock", 4, cost, 0m, 0m, 50m, 0m, 0m, 0m, 0m, -50m),
                new SpendStore("7th Avenue", 15, retail, 2790m, 700m, 0m, 0m, 100m, 0m, 0m, 1990m),
                new SpendStore("Salon", 3, service, 80m, 0m, 30m, 0m, 0m, 0m, 0m, 50m),
            },
            null,
            new[] { new SpendPlace("Home Street", 9, "home", 200m) },
            new[] { new SpendPlace("Dock", 4, cost, 25m) },
            new SpendEmpire(1990m, 25m, 200m, -15m, -5m, -40m, 10m, -8m, -2m, 3m, 1755m),
            null,
            0m,
            0m);

        var version = LoadLog.Version;
        Assert.Equal("retail", retail);
        Assert.Equal("cost", cost);
        Assert.Equal("service", service);
        Assert.Contains(
            "spend.roll version=" + version + " day=8 retail=1990 service=50 cost=-50 other=0 stores=1990",
            lines);
        Assert.Contains(
            "spend.home version=" + version + " day=8 street=Home_Street number=9 amount=200",
            lines);
        Assert.Contains(
            "spend.property version=" + version + " day=8 street=Dock number=4 kind=cost amount=25",
            lines);
        Assert.Contains(
            "spend.empire version=" + version + " day=8 business=1990 realEstate=25 residential=200 loan=-15 parking=-5 unassigned=-40 salary=10 insurance=-8 headhunter=-2 interest=3 total=1755 check=1755 gap=0 businessGap=0",
            lines);
    }

    private static SpendStore GiftShop()
    {
        return new SpendStore("7th Avenue", 15, "retail", 2790m, 704.0386m, 0m, 0m, 100m, 0m, 0m, 1985.9614m);
    }

    private static SpendStore Warehouse()
    {
        return new SpendStore("Warehouse", 2, "cost", 0m, 0m, 50m, 0m, 0m, 0m, 0m, -50m);
    }
}

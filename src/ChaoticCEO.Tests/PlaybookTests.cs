using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class PlaybookTests
{
    [Fact]
    public void Playbook_decide_picks_open_on_a_new_game_stage_one_board()
    {
        var step = Playbook.Decide(
            open: new OpenPick("ba:businesstype_giftshop", true),
            where: SitePick.None(),
            layout: LayoutPick.None(),
            prices: PricePick.None(),
            supply: SupplyPick.None(),
            trucks: TruckPick.None(),
            warehouses: WarehousePick.None(),
            managers: ManagerPick.None(),
            money: MoneyPick.None(),
            realEstate: EstatePick.None(),
            close: ClosePick.None(),
            run: RunPick.None());

        Assert.True(step.Chosen);
        Assert.Equal("open", step.Kind);
        Assert.Equal("ba:businesstype_giftshop", step.Detail);
        Assert.Equal("Next open ba:businesstype_giftshop", Playbook.Line(step));
    }

    [Fact]
    public void Playbook_decide_need_floor_sleep_beats_open()
    {
        var step = Playbook.Decide(
            open: new OpenPick("ba:businesstype_giftshop", true),
            where: SitePick.None(),
            layout: LayoutPick.None(),
            prices: PricePick.None(),
            supply: SupplyPick.None(),
            trucks: TruckPick.None(),
            warehouses: WarehousePick.None(),
            managers: ManagerPick.None(),
            money: MoneyPick.None(),
            realEstate: EstatePick.None(),
            close: ClosePick.None(),
            run: new RunPick("sleep", true));

        Assert.True(step.Chosen);
        Assert.Equal("run", step.Kind);
        Assert.Equal("sleep", step.Detail);
    }

    [Fact]
    public void Playbook_decide_open_beats_where_layout_and_money()
    {
        var step = Playbook.Decide(
            open: new OpenPick("ba:businesstype_florist", true),
            where: new SitePick("ba:street_seventhstreet", 26, true),
            layout: new LayoutPick("FloristWholesalers", true),
            prices: PricePick.None(),
            supply: SupplyPick.None(),
            trucks: TruckPick.None(),
            warehouses: WarehousePick.None(),
            managers: ManagerPick.None(),
            money: new MoneyPick("loan", 2_000m, 100, true),
            realEstate: EstatePick.None(),
            close: ClosePick.None(),
            run: RunPick.None());

        Assert.Equal("open", step.Kind);
        Assert.Equal("ba:businesstype_florist", step.Detail);
    }

    [Fact]
    public void Playbook_decide_managers_beat_money_when_both_are_chosen()
    {
        var step = Playbook.Decide(
            open: OpenPick.None(),
            where: SitePick.None(),
            layout: LayoutPick.None(),
            prices: PricePick.None(),
            supply: SupplyPick.None(),
            trucks: TruckPick.None(),
            warehouses: WarehousePick.None(),
            managers: new ManagerPick("pricing", "plan", "", true),
            money: new MoneyPick("loan", 2_000m, 100, true),
            realEstate: EstatePick.None(),
            close: ClosePick.None(),
            run: RunPick.None());

        Assert.Equal("managers", step.Kind);
        Assert.Equal("pricing plan", step.Detail);
    }

    [Fact]
    public void Playbook_decide_close_beats_run_staff_and_real_estate()
    {
        var step = Playbook.Decide(
            open: OpenPick.None(),
            where: SitePick.None(),
            layout: LayoutPick.None(),
            prices: PricePick.None(),
            supply: SupplyPick.None(),
            trucks: TruckPick.None(),
            warehouses: WarehousePick.None(),
            managers: ManagerPick.None(),
            money: MoneyPick.None(),
            realEstate: new EstatePick("ba:street_hq", 1, "headquarters", 100m, true),
            close: new ClosePick("close", true),
            run: new RunPick("cleaner", true));

        Assert.Equal("close", step.Kind);
        Assert.Equal("close", step.Detail);
    }

    [Fact]
    public void Playbook_decide_trucks_beat_warehouses_when_both_are_chosen()
    {
        var step = Playbook.Decide(
            open: OpenPick.None(),
            where: SitePick.None(),
            layout: LayoutPick.None(),
            prices: PricePick.None(),
            supply: SupplyPick.None(),
            trucks: new TruckPick("W12", 1, true),
            warehouses: new WarehousePick("specialty", "W3", "home", "enter", true),
            managers: ManagerPick.None(),
            money: MoneyPick.None(),
            realEstate: EstatePick.None(),
            close: ClosePick.None(),
            run: RunPick.None());

        Assert.Equal("trucks", step.Kind);
        Assert.Equal("W12 1", step.Detail);
    }

    [Fact]
    public void Playbook_decide_empty_board_is_none()
    {
        var step = Playbook.Decide(
            open: null,
            where: null,
            layout: null,
            prices: null,
            supply: null,
            trucks: null,
            warehouses: null,
            managers: null,
            money: null,
            realEstate: null,
            close: null,
            run: null);

        Assert.False(step.Chosen);
        Assert.Equal("", step.Kind);
        Assert.Equal("", step.Detail);
        Assert.Equal("Next none", Playbook.Line(step));
        Assert.Equal("Next none", Playbook.Line(null));
    }

    [Fact]
    public void Playbook_line_names_each_kind_detail()
    {
        Assert.Equal(
            "Next where ba:street_seventhstreet 26",
            Playbook.Line(new PlaybookStep("where", "ba:street_seventhstreet 26", true)));
        Assert.Equal(
            "Next layout GiftShopWholesalers",
            Playbook.Line(new PlaybookStep("layout", "GiftShopWholesalers", true)));
        Assert.Equal(
            "Next prices ba:itemname_cheapgift 21",
            Playbook.Line(new PlaybookStep("prices", "ba:itemname_cheapgift 21", true)));
        Assert.Equal(
            "Next supply warehouse W12",
            Playbook.Line(new PlaybookStep("supply", "warehouse W12", true)));
        Assert.Equal(
            "Next trucks W12 1",
            Playbook.Line(new PlaybookStep("trucks", "W12 1", true)));
        Assert.Equal(
            "Next warehouses home specialty W3",
            Playbook.Line(new PlaybookStep("warehouses", "home specialty W3", true)));
        Assert.Equal(
            "Next money loan 2000",
            Playbook.Line(new PlaybookStep("money", "loan 2000", true)));
        Assert.Equal(
            "Next realestate headquarters ba:street_hq 1",
            Playbook.Line(new PlaybookStep("realestate", "headquarters ba:street_hq 1", true)));
    }

    [Fact]
    public void Playbook_record_writes_playbook_shown_once_per_step()
    {
        using var dir = new TempDir();
        var step = new PlaybookStep("open", "ba:businesstype_giftshop", true);

        var shown = Playbook.Record(dir.Path, step);
        var again = Playbook.Record(dir.Path, step);

        Assert.Equal(
            "playbook.shown version=" + LoadLog.Version + " kind=open detail=ba:businesstype_giftshop",
            shown);
        Assert.Equal(shown, again);
        var log = File.ReadAllLines(LoadLog.CeoLogPath(dir.Path));
        Assert.Single(log, line => line.StartsWith("playbook.shown ", StringComparison.Ordinal));
    }

    private sealed class TempDir : IDisposable
    {
        public TempDir()
        {
            Path = Directory.CreateTempSubdirectory("ceo-playbook").FullName;
        }

        public string Path { get; }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}

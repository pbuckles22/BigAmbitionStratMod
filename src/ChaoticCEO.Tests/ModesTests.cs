using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class ModesTests
{
    private const string Gift = "ba:businesstype_giftshop";
    private const string Hair = "ba:businesstype_hairdresser";
    private const string Law = "ba:businesstype_lawfirm";
    private const string Web = "ba:businesstype_webdevelopmentagency";
    private const string Graphics = "ba:businesstype_graphicsdesignagency";
    private const string Gym = "ba:businesstype_gym";
    private const string Nightclub = "ba:businesstype_nightclub";
    private const string HairCare = "ba:itemname_haircareproduct";

    [Fact]
    public void Hairdresser_g2_is_product_and_a_styling_station()
    {
        var ready = StoreHealth.Assess(Facts(Hair, hairCare: 8, stations: 1));
        var productOnly = StoreHealth.Assess(Facts(Hair, hairCare: 8, stations: 0));
        var stationOnly = StoreHealth.Assess(Facts(Hair, hairCare: 0, stations: 1));
        var inner = Modes.Stocked(Modes.Of(Hair), shelvesStocked: false, hairCareOnHand: 8, stylingStations: 1, completeWorkstations: 0, Hair);

        Assert.Equal(BusinessMode.Service, Modes.Of(Hair));
        Assert.True(inner);
        Assert.True(ready.Stocked);
        Assert.False(productOnly.Stocked);
        Assert.False(stationOnly.Stocked);
        Assert.True(Modes.SignsContract(Hair));
        Assert.False(Modes.Stocked(Modes.Of(Hair), false, 0, 1, 4, Hair));
        Assert.False(Modes.Stocked(BusinessMode.Workstation, true, 0, 0, 4, Hair));
        Assert.True(Modes.Stocked(BusinessMode.Retail, false, 8, 1, 0, Hair));
    }

    [Fact]
    public void Retail_g2_stays_on_the_shelves()
    {
        var stocked = StoreHealth.Assess(Facts(Gift, shelves: GiftShelves()));
        var bareBag = StoreHealth.Assess(Facts(Gift, shelves: GiftShelves(bag: 0)));
        var desksDoNotPass = StoreHealth.Assess(Facts(Gift, workstations: 4));

        Assert.Equal(BusinessMode.Retail, Modes.Of(Gift));
        Assert.Equal(BusinessMode.Retail, Modes.Of(null));
        Assert.True(stocked.Stocked);
        Assert.False(bareBag.Stocked);
        Assert.False(desksDoNotPass.Stocked);
        Assert.True(Modes.SignsContract(Gift));
        Assert.Equal("GiftShopWholesalers", Modes.FitOut(Gift));
    }

    [Fact]
    public void An_office_or_a_gym_passes_g2_on_workstations_and_signs_no_contract()
    {
        var law = StoreHealth.Assess(Facts(Law, workstations: 4));
        var threeDesks = StoreHealth.Assess(Facts(Law, workstations: 3, hairCare: 40, stations: 2));
        var gym = StoreHealth.Assess(Facts(Gym, workstations: 1));
        var emptyGym = StoreHealth.Assess(Facts(Gym, workstations: 0, hairCare: 40, stations: 2));

        Assert.Equal(BusinessMode.Workstation, Modes.Of(Law));
        Assert.Equal(BusinessMode.Workstation, Modes.Of(Gym));
        Assert.Equal(4, Modes.DeskFloor(Law));
        Assert.Equal(1, Modes.DeskFloor(Gym));
        Assert.True(law.Stocked);
        Assert.False(threeDesks.Stocked);
        Assert.True(gym.Stocked);
        Assert.False(emptyGym.Stocked);
        Assert.False(Modes.SignsContract(Law));
        Assert.False(Modes.SignsContract(Gym));
        Assert.Equal("", Wholesaler.ContractType(Law, Gift));
        Assert.Equal("", Wholesaler.ContractType(Gym, Gift));
        Assert.Equal(Gift, Wholesaler.ContractType(Hair, Gift));
        Assert.False(Modes.SignsContract("ba:businesstype_travelagency"));
        Assert.Equal(1, Modes.DeskFloor("ba:businesstype_travelagency"));
        Assert.False(Modes.SignsContract("ba:businesstype_recruitmentagency"));
        Assert.Equal(4, Modes.DeskFloor("ba:businesstype_recruitmentagency"));
    }

    [Fact]
    public void Cargo_on_three_desks_does_not_pass_a_law_firm()
    {
        var cargo = StoreHealth.Assess(Facts(Law, shelves: GiftShelves(), workstations: 3));

        Assert.False(cargo.Stocked);
        Assert.False(Modes.Stocked(Modes.Of(Law), shelvesStocked: true, hairCareOnHand: 20, stylingStations: 1, completeWorkstations: 3, Law));
        Assert.True(Modes.Stocked(Modes.Of(Law), shelvesStocked: false, hairCareOnHand: 0, stylingStations: 0, completeWorkstations: 4, Law));
    }

    [Fact]
    public void A_nightclub_does_not_use_the_hairdresser_gate()
    {
        var hairOnly = StoreHealth.Assess(Facts(
            Nightclub,
            hairCare: 10,
            stations: 2,
            needed: new[] { "ba:itemname_drink" }));
        var drink = StoreHealth.Assess(Facts(
            Nightclub,
            shelves: new Dictionary<string, int> { ["ba:itemname_drink"] = 4 },
            needed: new[] { "ba:itemname_drink" }));

        Assert.Equal(BusinessMode.Service, Modes.Of(Nightclub));
        Assert.False(hairOnly.Stocked);
        Assert.True(drink.Stocked);
        Assert.True(Modes.SignsContract(Nightclub));
        Assert.Equal("", Modes.FitOut(Nightclub));
    }

    [Fact]
    public void Fit_out_uses_that_modes_starter()
    {
        Assert.Equal("HairdresserWholesalers", Modes.FitOut(Hair));
        Assert.Equal("LawFirm", Modes.FitOut(Law));
        Assert.Equal("WebDevelopmentAgency", Modes.FitOut(Web));
        Assert.Equal("GraphicDesigner", Modes.FitOut(Graphics));
        Assert.Equal("GymWholesalers", Modes.FitOut(Gym));
        Assert.Equal("FloristWholesalers", Modes.FitOut("ba:businesstype_florist"));
        Assert.Equal("FastFoodKabob", Modes.FitOut("ba:businesstype_fastfoodrestaurant"));
        Assert.Equal("", Modes.FitOut("ba:businesstype_clothingstore"));
        Assert.NotEqual(Modes.FitOut(Web), Modes.FitOut(Graphics));
    }

    [Fact]
    public void Placed_chairs_and_desks_are_the_station_counts()
    {
        var hair = Modes.Read(
            Hair,
            new[] { Modes.StylingChair, Modes.StylingChair, "ba:itemname_cleaningstation" });
        var desks = new List<string>();
        for (var seat = 0; seat < 3; seat++)
        {
            desks.Add("ba:itemname_officedesk1");
            desks.Add("ba:itemname_officechair2");
            desks.Add(Modes.Computer);
        }

        desks.Add("ba:itemname_officedesk2left");
        desks.Add("ba:itemname_armchair1");
        desks.Add(Modes.Computer);
        var law = Modes.Read(Law, desks);
        var shortOffice = Modes.Read(
            Law,
            new[]
            {
                "ba:itemname_officedesk1",
                "ba:itemname_officechair2",
                Modes.Computer,
            });
        var gym = Modes.Read(Gym, new[] { "ba:itemname_treadmill", "ba:itemname_cleaningstation" });
        var bareGym = Modes.Read(Gym, new[] { "ba:itemname_cleaningstation" });

        Assert.Equal(2, hair.Styling);
        Assert.Equal(0, hair.Workstations);
        Assert.Equal(4, law.Workstations);
        Assert.Equal(0, law.Styling);
        Assert.Equal(1, shortOffice.Workstations);
        Assert.False(StoreHealth.Assess(Facts(Law, workstations: shortOffice.Workstations)).Stocked);
        Assert.True(StoreHealth.Assess(Facts(Law, workstations: law.Workstations)).Stocked);
        Assert.Equal(1, gym.Workstations);
        Assert.Equal(0, bareGym.Workstations);
        Assert.True(StoreHealth.Assess(Facts(Gym, workstations: gym.Workstations)).Stocked);
    }

    [Fact]
    public void The_bill_is_labor_and_overhead_over_one_minus_margin()
    {
        var hairLabor = Modes.Labor(22m, 2m, 2.50m);
        var lawLabor = Modes.Labor(60m, 1m, 0m);
        var gymLabor = Modes.Labor(18m, 10m, 0m);

        Assert.Equal(13.50m, hairLabor);
        Assert.Equal(60m, lawLabor);
        Assert.Equal(1.8m, gymLabor);
        Assert.Equal(34.00m, Modes.Bill(BusinessMode.Service, hairLabor, 3.50m, 0.50m));
        Assert.Equal(137.00m, Modes.Bill(BusinessMode.Workstation, lawLabor, 8.50m, 0.50m));
        Assert.Equal(8.00m, Modes.Bill(BusinessMode.Workstation, gymLabor, 2.20m, 0.50m));
        Assert.Equal(0m, Modes.Bill(BusinessMode.Retail, hairLabor, 3.50m, 0.50m));
        Assert.Equal(0m, Modes.Bill(BusinessMode.Service, hairLabor, 3.50m, 1m));
        Assert.Equal(0m, Modes.Bill(BusinessMode.Service, hairLabor, 3.50m, 0m));
        Assert.Equal(0m, Modes.Labor(0m, 2m, 2.50m));
        Assert.Equal(10.01m, Modes.Bill(BusinessMode.Service, 5.0025m, 0m, 0.50m));
    }

    [Fact]
    public void The_bill_is_written_onto_the_fee_and_not_onto_a_retail_price()
    {
        var hairLabor = Modes.Labor(22m, 2m, 2.50m);
        var lawLabor = Modes.Labor(60m, 1m, 0m);
        var webLabor = Modes.Labor(45m, 1m, 0m);
        var graphicsLabor = Modes.Labor(36m, 1m, 0m);
        var gymLabor = Modes.Labor(18m, 10m, 0m);
        var hair = Modes.Quote(Hair, hairLabor, 3.50m, 0.50m);
        var law = Modes.Quote(Law, lawLabor, 8.50m, 0.50m);
        var web = Modes.Quote(Web, webLabor, 7.50m, 0.50m);
        var graphics = Modes.Quote(Graphics, graphicsLabor, 6.50m, 0.50m);
        var gym = Modes.Quote(Gym, gymLabor, 2.20m, 0.50m);
        var gift = Modes.Quote(Gift, hairLabor, 3.50m, 0.50m);
        var product = Prices.Decide(HairCare, 10m, 10m, 2.50m, 4, 4, 1m, null);

        Assert.Equal(34.00m, hair.Price);
        Assert.Equal("ba:itemname_haircuttingfee", hair.Item);
        Assert.True(hair.Chosen);
        Assert.Equal(137.00m, law.Price);
        Assert.Equal("ba:itemname_hourlylawyerfee", law.Item);
        Assert.Equal(105.00m, web.Price);
        Assert.Equal("ba:itemname_hourlyprogrammerfee", web.Item);
        Assert.Equal(85.00m, graphics.Price);
        Assert.Equal("ba:itemname_hourlygraphicdesignerfee", graphics.Item);
        Assert.Equal(8.00m, gym.Price);
        Assert.Equal("ba:itemname_gymcovercharge", gym.Item);
        Assert.False(gift.Chosen);
        Assert.Equal(0m, gift.Price);
        Assert.NotEqual(HairCare, hair.Item);
        Assert.NotEqual(hair.Price, product.Price);
        Assert.False(Modes.Quote(Nightclub, hairLabor, 3.50m, 0.50m).Chosen);
        Assert.False(Modes.Quote("ba:businesstype_recruitmentagency", lawLabor, 8.50m, 0.50m).Chosen);
        Assert.False(Modes.Quote(Hair, hairLabor, 3.50m, 0m).Chosen);
    }

    [Fact]
    public void The_standard_bill_is_the_fee_the_layout_quotes()
    {
        using var dir = new TempLog();
        var hair = Modes.Standard(Hair);
        var law = Modes.Standard(Law);
        var web = Modes.Standard(Web);
        var graphics = Modes.Standard(Graphics);
        var gym = Modes.Standard(Gym);
        var gift = Modes.Standard(Gift);
        var line = Modes.Record(dir.Path, hair, day: 1);
        var again = Modes.Record(dir.Path, hair, day: 1);
        var blank = Modes.Record(dir.Path, gift, day: 1);
        var file = File.ReadAllText(LoadLog.CeoLogPath(dir.Path)).Trim();

        Assert.True(hair.Chosen);
        Assert.Equal(34.00m, hair.Price);
        Assert.Equal("ba:itemname_haircuttingfee", hair.Item);
        Assert.Equal(137.00m, law.Price);
        Assert.Equal("ba:itemname_hourlylawyerfee", law.Item);
        Assert.Equal(105.00m, web.Price);
        Assert.Equal("ba:itemname_hourlyprogrammerfee", web.Item);
        Assert.Equal(85.00m, graphics.Price);
        Assert.Equal("ba:itemname_hourlygraphicdesignerfee", graphics.Item);
        Assert.Equal(8.00m, gym.Price);
        Assert.Equal("ba:itemname_gymcovercharge", gym.Item);
        Assert.False(Modes.Standard(Nightclub).Chosen);
        Assert.False(Modes.Standard("ba:businesstype_recruitmentagency").Chosen);
        Assert.False(gift.Chosen);
        Assert.Equal(
            "bill.quoted version=" + LoadLog.Version + " day=1 item=ba:itemname_haircuttingfee price=34.00",
            line);
        Assert.Equal(line, again);
        Assert.Equal("", blank);
        Assert.Equal(line, file);
    }

    [Fact]
    public void Chain_open_sets_the_mode_the_gate_the_starter_and_the_bill()
    {
        // Chain: open → mode → G2 → fit-out → bill
        var opened = Open.Decide(
            3,
            new[]
            {
                Goods(Gift, "specialty", 200m),
                Service(Hair, "service", 80m),
                Service(Law, "office", 40m),
            },
            Open.TruckBusy + 0.01m);
        var mode = Modes.Of(opened.Type);
        var health = StoreHealth.Assess(Facts(opened.Type, hairCare: 8, stations: 1));
        var starter = Modes.FitOut(opened.Type);
        var layout = Layout.Decide(
            1,
            opened.Type,
            "A1",
            owned: null,
            starters: new[] { new StarterOffer(opened.Type, starter, 1m) });
        var bill = Modes.Bill(mode, Modes.Labor(22m, 2m, 2.50m), 3.50m, 0.50m);

        Assert.True(opened.Chosen);
        Assert.Equal(Hair, opened.Type);
        Assert.Equal(BusinessMode.Service, mode);
        Assert.True(health.Stocked);
        Assert.Equal("HairdresserWholesalers", starter);
        Assert.True(layout.Chosen);
        Assert.Equal(starter, layout.Blueprint);
        Assert.Equal(34.00m, bill);
        Assert.True(Modes.SignsContract(opened.Type));
    }

    [Fact]
    public void Chain_a_blocked_goods_store_opens_a_gym_with_no_contract()
    {
        // Chain: open → mode → G2 → fit-out → bill
        var opened = Open.Decide(
            4,
            new[]
            {
                Goods(Gift, "specialty", 90m, owned: 2, warehouse: false),
                Service(Gym, "service", 20m),
            },
            truckLoadFraction: 0m);
        var mode = Modes.Of(opened.Type);
        var health = StoreHealth.Assess(Facts(opened.Type, workstations: Modes.DeskFloor(opened.Type)));
        var starter = Modes.FitOut(opened.Type);
        var layout = Layout.Decide(
            1,
            opened.Type,
            "A1",
            owned: null,
            starters: new[] { new StarterOffer(opened.Type, starter, 1m) });
        var bill = Modes.Bill(mode, Modes.Labor(18m, 10m, 0m), 2.20m, 0.50m);
        var contract = Wholesaler.ContractType(opened.Type, Gift);

        Assert.True(opened.Chosen);
        Assert.Equal(Gym, opened.Type);
        Assert.Equal(BusinessMode.Workstation, mode);
        Assert.Equal(1, Modes.DeskFloor(opened.Type));
        Assert.True(health.Stocked);
        Assert.Equal("GymWholesalers", starter);
        Assert.Equal(starter, layout.Blueprint);
        Assert.Equal(8.00m, bill);
        Assert.Equal("", contract);
        Assert.False(Modes.SignsContract(opened.Type));
    }

    [Fact]
    public void Chain_a_profit_tie_keeps_the_later_office()
    {
        // Chain: open → mode → G2 → fit-out → bill
        var opened = Open.Decide(
            3,
            new[]
            {
                Service(Hair, "service", 50m),
                Service(Law, "office", 50m),
            },
            Open.TruckBusy + 0.01m);
        var mode = Modes.Of(opened.Type);
        var hairGate = StoreHealth.Assess(Facts(opened.Type, hairCare: 8, stations: 1));
        var desks = StoreHealth.Assess(Facts(opened.Type, workstations: Modes.DeskFloor(opened.Type)));
        var starter = Modes.FitOut(opened.Type);
        var bill = Modes.Bill(mode, Modes.Labor(60m, 1m, 0m), 8.50m, 0.50m);

        Assert.Equal(Law, opened.Type);
        Assert.Equal(BusinessMode.Workstation, mode);
        Assert.False(hairGate.Stocked);
        Assert.True(desks.Stocked);
        Assert.Equal("LawFirm", starter);
        Assert.Equal(137.00m, bill);
        Assert.Equal("", Wholesaler.ContractType(opened.Type, Gift));
    }

    [Fact]
    public void Stage_one_open_cannot_be_a_hairdresser_an_office_or_a_gym()
    {
        var busy = Open.Decide(
            1,
            new[]
            {
                Goods(Gift, "specialty", 40m),
                Service(Hair, "service", 200m),
                Service(Law, "office", 180m),
                Service(Web, "office", 170m),
                Service(Gym, "service", 150m),
            },
            Open.TruckBusy + 0.01m);
        var onlyModes = Open.Decide(
            1,
            new[]
            {
                Service(Hair, "service", 200m),
                Service(Law, "office", 180m),
                Service(Gym, "service", 150m),
            },
            Open.TruckBusy + 0.01m);
        var loaded = Starter.Choose(
            new[]
            {
                new StarterOffer(Hair, "HairdresserWholesalers", 200m),
                new StarterOffer(Law, "LawFirm", 180m),
                new StarterOffer(Gym, "GymWholesalers", 150m),
                new StarterOffer(Gift, "WrongLayout", 40m),
            },
            alreadyOpen: false,
            GameApi.Live());

        Assert.True(busy.Chosen);
        Assert.Equal(Gift, busy.Type);
        Assert.False(onlyModes.Chosen);
        Assert.Equal("", onlyModes.Type);
        Assert.Equal(Gift, loaded.BusinessType);
        Assert.Equal("GiftShopWholesalers", loaded.Layout);
    }

    [Fact]
    public void The_game_loads_that_modes_starter_and_not_the_bill()
    {
        var api = GameApi.Live();
        var hair = Starter.Choose(Starter.StageOne(), alreadyOpen: false, api, Hair);
        var law = Starter.Choose(null, alreadyOpen: false, api, Law);
        var web = Starter.Choose(Starter.StageOne(), alreadyOpen: false, api, Web);
        var graphics = Starter.Choose(Starter.StageOne(), alreadyOpen: false, api, Graphics);
        var gym = Starter.Choose(Starter.StageOne(), alreadyOpen: false, api, Gym);
        var gift = Starter.Choose(
            new[] { new StarterOffer(Gift, "WrongLayout", 40m) },
            alreadyOpen: false,
            api,
            Gift);
        var blueprint = Layout.Decide(3, Hair, "A1", Array.Empty<OwnedLayout>(), Starter.StageOne());
        var bill = Modes.Bill(BusinessMode.Service, Modes.Labor(22m, 2m, 2.50m), 3.50m, 0.50m);
        var price = Prices.Decide(HairCare, 10m, 10m, 2.50m, 4, 4, 1m, null);

        Assert.True(hair.Loaded);
        Assert.Equal(Hair, hair.BusinessType);
        Assert.Equal("HairdresserWholesalers", hair.Layout);
        Assert.Equal("LawFirm", law.Layout);
        Assert.Equal("WebDevelopmentAgency", web.Layout);
        Assert.Equal("GraphicDesigner", graphics.Layout);
        Assert.Equal("GymWholesalers", gym.Layout);
        Assert.Equal(Gift, gift.BusinessType);
        Assert.Equal("GiftShopWholesalers", gift.Layout);
        Assert.Equal("HairdresserWholesalers", blueprint.Blueprint);
        Assert.Equal(34.00m, bill);
        Assert.True(price.Chosen);
        Assert.NotEqual(bill, price.Price);
    }

    [Fact]
    public void Chain_stage_one_loads_the_gift_shop_and_leaves_the_bill_off_the_price()
    {
        // Chain: open → mode → G2 → fit-out → bill
        var opened = Open.Decide(
            1,
            new[]
            {
                Goods(Gift, "specialty", 40m),
                Service(Hair, "service", 200m),
                Service(Law, "office", 180m),
                Service(Gym, "service", 150m),
            },
            Open.TruckBusy + 0.01m);
        var mode = Modes.Of(opened.Type);
        var health = StoreHealth.Assess(Facts(opened.Type, shelves: GiftShelves()));
        var loaded = Starter.Choose(
            new[] { new StarterOffer(opened.Type, "WrongLayout", 40m) },
            alreadyOpen: false,
            GameApi.Live());
        var bill = Modes.Bill(mode, Modes.Labor(22m, 2m, 2.50m), 3.50m, 0.50m);
        var price = Prices.Decide("ba:itemname_cheapgift", 10m, 10m, 4m, 8, 8, 1m, null);

        Assert.Equal(Gift, opened.Type);
        Assert.Equal(BusinessMode.Retail, mode);
        Assert.True(health.Stocked);
        Assert.Equal("GiftShopWholesalers", loaded.Layout);
        Assert.Equal(0m, bill);
        Assert.False(Modes.Standard(opened.Type).Chosen);
        Assert.True(price.Chosen);
        Assert.NotEqual(bill, price.Price);
    }

    [Fact]
    public void Chain_a_blank_layout_waits_then_the_gift_shop_loads()
    {
        // Chain: open → mode → G2 → fit-out → bill
        var opened = Open.Decide(
            1,
            new[]
            {
                Goods(Gift, "specialty", 40m),
                Service(Hair, "service", 200m),
            },
            Open.TruckBusy + 0.01m);
        var waiting = Starter.Choose(new StarterOffer[0], alreadyOpen: false, GameApi.Live());
        var loaded = Starter.Choose(Starter.StageOne(), alreadyOpen: false, GameApi.Live(), opened.Type);
        var quote = Modes.Standard(opened.Type);

        Assert.Equal(Gift, opened.Type);
        Assert.Equal(BusinessMode.Retail, Modes.Of(opened.Type));
        Assert.True(Starter.Hold(waiting));
        Assert.Equal("GiftShopWholesalers", loaded.Layout);
        Assert.True(loaded.Loaded);
        Assert.False(quote.Chosen);
    }

    [Fact]
    public void Chain_an_empty_lot_loads_the_gift_shop_the_open_chose()
    {
        // Chain: open → mode → G2 → fit-out → bill
        var opened = Open.Decide(
            1,
            new[]
            {
                Goods(Gift, "specialty", 40m),
                Service(Hair, "service", 200m),
            },
            Open.TruckBusy + 0.01m);
        var loaded = Starter.Choose(
            Starter.StageOne(),
            alreadyOpen: false,
            GameApi.Live(),
            "ba:businesstype_empty");
        var quote = Modes.Standard(loaded.BusinessType);

        Assert.Equal(Gift, opened.Type);
        Assert.Equal(opened.Type, loaded.BusinessType);
        Assert.Equal(BusinessMode.Retail, Modes.Of(loaded.BusinessType));
        Assert.Equal("GiftShopWholesalers", loaded.Layout);
        Assert.True(loaded.Loaded);
        Assert.False(quote.Chosen);
    }

    [Fact]
    public void Chain_a_loaded_hairdresser_quotes_the_standard_cutting_fee()
    {
        // Chain: open → mode → G2 → fit-out → bill
        using var dir = new TempLog();
        var opened = Open.Decide(
            3,
            new[]
            {
                Goods(Gift, "specialty", 40m),
                Service(Hair, "service", 80m),
            },
            Open.TruckBusy + 0.01m);
        var mode = Modes.Of(opened.Type);
        var health = StoreHealth.Assess(Facts(opened.Type, hairCare: 8, stations: 1));
        var loaded = Starter.Choose(Starter.StageOne(), alreadyOpen: false, GameApi.Live(), opened.Type);
        var quote = Modes.Standard(opened.Type);
        var line = Modes.Record(dir.Path, quote, day: 1);

        Assert.Equal(Hair, opened.Type);
        Assert.Equal(BusinessMode.Service, mode);
        Assert.True(health.Stocked);
        Assert.Equal("HairdresserWholesalers", loaded.Layout);
        Assert.True(quote.Chosen);
        Assert.Equal("ba:itemname_haircuttingfee", quote.Item);
        Assert.Equal(34.00m, quote.Price);
        Assert.Equal(
            "bill.quoted version=" + LoadLog.Version + " day=1 item=ba:itemname_haircuttingfee price=34.00",
            line);
    }

    [Fact]
    public void Chain_a_later_open_loads_that_modes_starter_and_not_the_bill()
    {
        // Chain: open → mode → G2 → fit-out → bill
        var opened = Open.Decide(
            3,
            new[]
            {
                Goods(Gift, "specialty", 40m),
                Service(Hair, "service", 80m),
            },
            Open.TruckBusy + 0.01m);
        var mode = Modes.Of(opened.Type);
        var health = StoreHealth.Assess(Facts(opened.Type, hairCare: 8, stations: 1));
        var loaded = Starter.Choose(Starter.StageOne(), alreadyOpen: false, GameApi.Live(), opened.Type);
        var blueprint = Layout.Decide(3, opened.Type, "A1", Array.Empty<OwnedLayout>(), Starter.StageOne());
        var bill = Modes.Bill(mode, Modes.Labor(22m, 2m, 2.50m), 3.50m, 0.50m);
        var price = Prices.Decide(HairCare, 10m, 10m, 2.50m, 4, 4, 1m, null);

        Assert.Equal(Hair, opened.Type);
        Assert.Equal(BusinessMode.Service, mode);
        Assert.True(health.Stocked);
        Assert.True(Modes.SignsContract(opened.Type));
        Assert.Equal("HairdresserWholesalers", loaded.Layout);
        Assert.Equal(loaded.Layout, blueprint.Blueprint);
        Assert.Equal(34.00m, bill);
        Assert.NotEqual(bill, price.Price);
    }

    [Fact]
    public void A_type_with_no_starter_does_not_load()
    {
        var api = GameApi.Live();
        var nightclub = Starter.Choose(Starter.StageOne(), alreadyOpen: false, api, Nightclub);
        var recruitment = Starter.Choose(
            Starter.StageOne(),
            alreadyOpen: false,
            api,
            "ba:businesstype_recruitmentagency");
        var closed = Starter.Choose(Starter.StageOne(), alreadyOpen: true, api, Hair);
        var noCall = Starter.Choose(Starter.StageOne(), alreadyOpen: false, api: null, Law);
        var clothing = Layout.Decide(2, "ba:businesstype_clothingstore", "A1", Array.Empty<OwnedLayout>(), null);

        Assert.False(nightclub.Loaded);
        Assert.Equal("", nightclub.Layout);
        Assert.Equal(Nightclub, nightclub.BusinessType);
        Assert.Equal("", Modes.FitOut("ba:businesstype_recruitmentagency"));
        Assert.False(recruitment.Loaded);
        Assert.Equal("", recruitment.Layout);
        Assert.False(closed.Loaded);
        Assert.Equal("", closed.Layout);
        Assert.False(noCall.Loaded);
        Assert.False(clothing.Chosen);
        Assert.Equal("", clothing.Blueprint);
    }

    [Fact]
    public void Chain_a_busy_stage_loads_the_gym_starter_and_leaves_the_bill_off_the_price()
    {
        // Chain: open → mode → G2 → fit-out → bill
        var opened = Open.Decide(
            3,
            new[]
            {
                Goods(Gift, "specialty", 90m),
                Service(Web, "office", 40m),
                Service(Gym, "service", 40m),
            },
            Open.TruckBusy + 0.01m);
        var mode = Modes.Of(opened.Type);
        var health = StoreHealth.Assess(Facts(opened.Type, workstations: Modes.DeskFloor(opened.Type)));
        var loaded = Starter.Choose(Starter.StageOne(), alreadyOpen: false, GameApi.Live(), opened.Type);
        var blueprint = Layout.Decide(3, opened.Type, "A1", Array.Empty<OwnedLayout>(), Starter.StageOne());
        var bill = Modes.Bill(mode, Modes.Labor(18m, 10m, 0m), 2.20m, 0.50m);
        var price = Prices.Decide("ba:itemname_cheapgift", 10m, 10m, 4m, 8, 8, 1m, null);

        Assert.Equal(Gym, opened.Type);
        Assert.Equal(BusinessMode.Workstation, mode);
        Assert.True(health.Stocked);
        Assert.False(Modes.SignsContract(opened.Type));
        Assert.Equal("", Wholesaler.ContractType(opened.Type, Gift));
        Assert.Equal("GymWholesalers", loaded.Layout);
        Assert.Equal(loaded.Layout, blueprint.Blueprint);
        Assert.NotEqual(Modes.FitOut(Web), loaded.Layout);
        Assert.Equal(8.00m, bill);
        Assert.NotEqual(bill, price.Price);
    }

    [Fact]
    public void Chain_a_hairdresser_open_writes_the_bill_onto_the_cutting_fee()
    {
        // Chain: open → mode → G2 → fit-out → bill
        var opened = Open.Decide(
            3,
            new[]
            {
                Goods(Gift, "specialty", 40m),
                Service(Hair, "service", 80m),
            },
            Open.TruckBusy + 0.01m);
        var mode = Modes.Of(opened.Type);
        var health = StoreHealth.Assess(Facts(opened.Type, hairCare: 8, stations: 1));
        var loaded = Starter.Choose(Starter.StageOne(), alreadyOpen: false, GameApi.Live(), opened.Type);
        var labor = Modes.Labor(22m, 2m, 2.50m);
        var bill = Modes.Bill(mode, labor, 3.50m, 0.50m);
        var quote = Modes.Quote(opened.Type, labor, 3.50m, 0.50m);
        var product = Prices.Decide(HairCare, 10m, 10m, 2.50m, 4, 4, 1m, null);

        Assert.Equal(Hair, opened.Type);
        Assert.Equal(BusinessMode.Service, mode);
        Assert.True(health.Stocked);
        Assert.Equal("HairdresserWholesalers", loaded.Layout);
        Assert.Equal(34.00m, bill);
        Assert.Equal(bill, quote.Price);
        Assert.Equal("ba:itemname_haircuttingfee", quote.Item);
        Assert.NotEqual(quote.Price, product.Price);
    }

    [Fact]
    public void Chain_a_gym_open_writes_the_bill_onto_the_cover_charge()
    {
        // Chain: open → mode → G2 → fit-out → bill
        var opened = Open.Decide(
            3,
            new[]
            {
                Goods(Gift, "specialty", 90m),
                Service(Gym, "service", 40m),
            },
            Open.TruckBusy + 0.01m);
        var mode = Modes.Of(opened.Type);
        var health = StoreHealth.Assess(Facts(opened.Type, workstations: Modes.DeskFloor(opened.Type)));
        var loaded = Starter.Choose(Starter.StageOne(), alreadyOpen: false, GameApi.Live(), opened.Type);
        var labor = Modes.Labor(18m, 10m, 0m);
        var bill = Modes.Bill(mode, labor, 2.20m, 0.50m);
        var quote = Modes.Quote(opened.Type, labor, 2.20m, 0.50m);
        var gift = Modes.Quote(Gift, labor, 2.20m, 0.50m);
        var retail = Prices.Decide("ba:itemname_cheapgift", 10m, 10m, 4m, 8, 8, 1m, null);

        Assert.Equal(Gym, opened.Type);
        Assert.Equal(BusinessMode.Workstation, mode);
        Assert.True(health.Stocked);
        Assert.Equal("GymWholesalers", loaded.Layout);
        Assert.False(Modes.SignsContract(opened.Type));
        Assert.Equal(8.00m, bill);
        Assert.Equal(bill, quote.Price);
        Assert.Equal("ba:itemname_gymcovercharge", quote.Item);
        Assert.False(gift.Chosen);
        Assert.NotEqual(quote.Price, retail.Price);
    }

    [Fact]
    public void Chain_the_loaded_layout_is_the_mode_starter()
    {
        // Chain: open → mode → G2 → fit-out → bill
        var chosen = Starter.Choose(
            new[] { new StarterOffer(Gift, "WrongLayout", 40m) },
            alreadyOpen: false,
            GameApi.Live());
        var mode = Modes.Of(chosen.BusinessType);
        var health = StoreHealth.Assess(Facts(chosen.BusinessType, shelves: GiftShelves()));
        var starter = Modes.FitOut(chosen.BusinessType);
        var bill = Modes.Bill(mode, Modes.Labor(22m, 2m, 2.50m), 3.50m, 0.50m);

        Assert.True(chosen.Loaded);
        Assert.Equal(Gift, chosen.BusinessType);
        Assert.Equal(BusinessMode.Retail, mode);
        Assert.True(health.Stocked);
        Assert.Equal("GiftShopWholesalers", starter);
        Assert.Equal(starter, chosen.Layout);
        Assert.Equal(0m, bill);
    }

    private static StoreFacts Facts(
        string type,
        int hairCare = 0,
        int stations = 0,
        int workstations = 0,
        IReadOnlyDictionary<string, int>? shelves = null,
        IReadOnlyList<string>? needed = null)
    {
        var cargo = shelves ?? new Dictionary<string, int>();
        if (hairCare > 0)
        {
            var copy = new Dictionary<string, int>(cargo);
            copy[HairCare] = hairCare;
            cargo = copy;
        }

        return new StoreFacts(
            "ba:street_seventhavenue 26",
            temporarilyClosed: false,
            neededItems: needed ?? (type == Gift ? Wholesaler.GiftShopWholesalersStock() : Array.Empty<string>()),
            cargo,
            registerStaffed: true,
            cleanerStaffed: true,
            staffSatisfaction: 80f,
            scheduledOpen: true,
            dailyCustomers: 4,
            dailyRevenue: 20m,
            recentOpenDayProfits: new[] { 5m, 6m, 7m },
            businessType: type,
            stylingStations: stations,
            completeWorkstations: workstations);
    }

    private static Dictionary<string, int> GiftShelves(int bag = 5)
    {
        return new Dictionary<string, int>
        {
            ["ba:itemname_cheapgift"] = 5,
            ["ba:itemname_cheapflower"] = 5,
            ["ba:itemname_sodacan"] = 5,
            ["ba:itemname_umbrella"] = 5,
            ["ba:itemname_paperbag"] = bag,
        };
    }

    private static OpenCandidate Goods(
        string type,
        string family,
        decimal profit,
        int owned = 0,
        bool warehouse = true)
    {
        return new OpenCandidate(type, "goods", profit, wholesalerSells: true, family, owned, warehouse);
    }

    private static OpenCandidate Service(string type, string kind, decimal profit)
    {
        return new OpenCandidate(type, kind, profit, wholesalerSells: false, kind, 0, false);
    }

    private sealed class TempLog : IDisposable
    {
        public TempLog()
        {
            Path = Directory.CreateTempSubdirectory("chaotic-bill").FullName;
        }

        public string Path { get; }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}

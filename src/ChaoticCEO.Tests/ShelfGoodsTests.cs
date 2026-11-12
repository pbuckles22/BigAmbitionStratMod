using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class ShelfGoodsTests
{
    private const string Gift = "ba:businesstype_giftshop";
    private const string Florist = "ba:businesstype_florist";
    private const string FastFood = "ba:businesstype_fastfoodrestaurant";
    private const string Kabob = "ba:itemname_kabob";
    private const string PaperBag = "ba:itemname_paperbag";
    private const string Umbrella = "ba:itemname_umbrella";
    private const string CheapGift = "ba:itemname_cheapgift";

    [Fact]
    public void Catalog_fast_food_lists_the_kabob_layout_not_the_gift_shop()
    {
        // Inspected from BusinessLayouts/FastFoodRestaurant/A1/FastFoodKabob.json,
        // including the register's paper bag.
        var catalog = Wholesaler.Catalog(FastFood);

        Assert.Equal(
            new[]
            {
                PaperBag,
                "ba:itemname_sodacan",
                "ba:itemname_frenchfries",
                "ba:itemname_banana",
                "ba:itemname_apple",
                Kabob,
            },
            catalog);
        Assert.DoesNotContain(CheapGift, catalog);
        Assert.DoesNotContain(Umbrella, catalog);
        Assert.NotEqual(Wholesaler.GiftShopWholesalersStock(), catalog);
    }

    [Fact]
    public void Catalog_florist_lists_the_florist_layout_not_the_gift_shop()
    {
        // Inspected from BusinessLayouts/Florist/A1/FloristWholesalers.json.
        var catalog = Wholesaler.Catalog(Florist);

        Assert.Equal(
            new[]
            {
                "ba:itemname_expensiveflower",
                Umbrella,
                PaperBag,
                "ba:itemname_sodacan",
                "ba:itemname_cheapflower",
            },
            catalog);
        Assert.DoesNotContain(CheapGift, catalog);
        Assert.NotEqual(Wholesaler.GiftShopWholesalersStock(), catalog);
    }

    [Fact]
    public void Fixture_capacity_is_the_sum_of_the_fixtures_that_sell_that_item()
    {
        var fixtures = new[]
        {
            new ShelfFixture("ba:itemname_productpanel", Umbrella, 40),
            new ShelfFixture("ba:itemname_productpanel", Umbrella, 60),
            new ShelfFixture("ba:itemname_cashregister", PaperBag, 200),
            new ShelfFixture("ba:itemname_drinksfridge", "ba:itemname_sodacan", 24),
            null,
        };

        Assert.Equal(100, Wholesaler.FixtureCapacity(Umbrella, fixtures));
        Assert.Equal(200, Wholesaler.FixtureCapacity(PaperBag, fixtures));
        Assert.Equal(24, Wholesaler.FixtureCapacity("ba:itemname_sodacan", fixtures));
        Assert.Equal(0, Wholesaler.FixtureCapacity(CheapGift, fixtures));
        Assert.Equal(0, Wholesaler.FixtureCapacity(Umbrella, null));
        Assert.Equal(0, Wholesaler.FixtureCapacity("", fixtures));
        Assert.Equal(0, Wholesaler.FixtureCapacity(null, fixtures));
    }

    [Fact]
    public void Cover_22_7th_day_42_paper_bags_cut_to_the_register()
    {
        // Harvest: day 42 on hand 177 against a 200 register. The line is the 23 that still fit.
        var fixtures = new[]
        {
            new ShelfFixture("ba:itemname_cashregister", PaperBag, 0),
            new ShelfFixture("ba:itemname_productpanel", Umbrella, 0),
            new ShelfFixture("ba:itemname_productpanel", Umbrella, 0),
            new ShelfFixture("ba:itemname_roundedshelf", CheapGift, 0),
        };
        var bags = Wholesaler.FixtureCapacity(PaperBag, fixtures);
        var plan = Wholesaler.Cover(
            PaperBag,
            currentDay: 42,
            nextDeliveryDay: 43,
            new[]
            {
                new CoverSale(40, 82, open: true),
                new CoverSale(41, 89, open: true),
            },
            onHand: 177,
            fixtureCap: 200,
            stands: Wholesaler.RetailStands(PaperBag));

        Assert.Equal(0, bags);
        Assert.Equal(0, Wholesaler.FixtureCapacity(Umbrella, fixtures));
        Assert.Equal(0, Wholesaler.FixtureCapacity(CheapGift, fixtures));
        Assert.Equal(372, plan.Target);
        Assert.Equal(23, plan.Need);
        Assert.Equal(23, plan.FixtureCap);
    }

    [Fact]
    public void Cover_22_7th_day_56_cheap_gifts_over_the_shelf_order_nothing()
    {
        // Harvest: day 56 on hand 184 already past a 100 shelf. The line is 0.
        var plan = Wholesaler.Cover(
            CheapGift,
            currentDay: 56,
            nextDeliveryDay: 57,
            new[]
            {
                new CoverSale(54, 70, open: true),
                new CoverSale(55, 74, open: true),
            },
            onHand: 184,
            fixtureCap: 100,
            stands: Wholesaler.RetailStands(CheapGift));

        Assert.Equal(344, plan.Target);
        Assert.Equal(0, plan.Need);
        Assert.Equal(0, plan.FixtureCap);
    }

    [Fact]
    public void A_short_wholesaler_line_is_a_supply_gap()
    {
        var shortLine = Wholesaler.ShortLine("ba:street_seventhavenue 22", Kabob, contractAmount: 10, lineAmount: 80);
        var exact = Wholesaler.ShortLine("ba:street_seventhavenue 22", Kabob, contractAmount: 80, lineAmount: 80);
        var over = Wholesaler.ShortLine("ba:street_seventhavenue 22", Kabob, contractAmount: 90, lineAmount: 80);
        var blank = Wholesaler.ShortLine("", Kabob, contractAmount: 0, lineAmount: 80);

        Assert.NotNull(shortLine);
        Assert.Equal("ba:street_seventhavenue 22", shortLine.Store);
        Assert.Equal("", shortLine.Warehouse);
        Assert.Equal(Kabob, shortLine.Item);
        Assert.Equal("short", shortLine.Reason);
        Assert.Equal(80, shortLine.Target);
        Assert.Null(exact);
        Assert.Null(over);
        Assert.Null(blank);
    }

    [Fact]
    public void Chain_open_type_catalog_line_cuts_to_the_shelf()
    {
        // Chain: open type → catalog → line
        var open = Open.Decide(
            1,
            Open.StageOneCandidates(
                new[]
                {
                    new StarterOffer(Gift, "GiftShopWholesalers", 40m),
                    new StarterOffer(FastFood, "FastFoodKabob", 90m),
                },
                ownedByFamily: null,
                pendingOpen: null),
            truckLoadFraction: 0m);
        var catalog = Wholesaler.Catalog(open.Type);
        var item = catalog[0];
        var cap = Wholesaler.FixtureCapacity(
            item,
            new[] { new ShelfFixture("ba:itemname_cashregister", item, 80) });
        var plan = Wholesaler.Cover(
            item,
            currentDay: 2,
            nextDeliveryDay: 2,
            days: null,
            onHand: 0,
            fixtureCap: cap,
            stands: Wholesaler.RetailStands(item));

        Assert.Equal(FastFood, open.Type);
        Assert.Equal(PaperBag, item);
        Assert.Equal(80, cap);
        Assert.Equal(80, plan.FixtureCap);
        Assert.Equal(80, plan.Need);
    }

    [Fact]
    public void Contract_type_uses_the_open_goods_list_and_leaves_hair_and_office_on_the_starter()
    {
        Assert.Equal(Florist, Wholesaler.ContractType(Florist, Gift));
        Assert.Equal(FastFood, Wholesaler.ContractType(FastFood, Gift));
        Assert.Equal(Gift, Wholesaler.ContractType(Gift, FastFood));
        Assert.Equal(Gift, Wholesaler.ContractType("ba:businesstype_hairdresser", Gift));
        Assert.Equal(Gift, Wholesaler.ContractType("ba:businesstype_lawfirm", Gift));
        Assert.Equal(Gift, Wholesaler.ContractType("", Gift));
        Assert.Equal(Gift, Wholesaler.ContractType(null, Gift));
        Assert.Empty(Wholesaler.Catalog("ba:businesstype_hairdresser"));
        Assert.Empty(Wholesaler.Catalog("ba:businesstype_lawfirm"));
    }

    [Fact]
    public void Chain_a_short_florist_line_is_the_supply_gap()
    {
        // Chain: open type → catalog → line → short gap
        var open = Open.Decide(
            1,
            Open.StageOneCandidates(
                new[]
                {
                    new StarterOffer(Gift, "GiftShopWholesalers", 40m),
                    new StarterOffer(Florist, "FloristWholesalers", 90m),
                },
                ownedByFamily: null,
                pendingOpen: null),
            truckLoadFraction: 0m);
        var catalog = Wholesaler.Catalog(open.Type);
        var item = catalog[0];
        var cap = Wholesaler.FixtureCapacity(
            item,
            new[] { new ShelfFixture("ba:itemname_productdisplaystandtiered", item, 40) });
        var plan = Wholesaler.Cover(
            item,
            currentDay: 2,
            nextDeliveryDay: 2,
            days: null,
            onHand: 0,
            fixtureCap: cap,
            stands: Wholesaler.RetailStands(item));
        var gap = Wholesaler.ShortLine("ba:street_seventhavenue 22", item, contractAmount: 12, plan.Need);
        var line = Wholesaler.ShortRecord(gap, 2, "ba:street_seventhavenue", 22, 12);

        Assert.Equal(Florist, open.Type);
        Assert.Equal("ba:itemname_expensiveflower", item);
        Assert.Equal(40, cap);
        Assert.Equal(40, plan.Need);
        Assert.NotNull(gap);
        Assert.Equal("short", gap.Reason);
        Assert.Equal(plan.Need, gap.Target);
        Assert.Equal(
            "contract.short version=" + LoadLog.Version
                + " day=2 street=ba:street_seventhavenue number=22 item=" + item
                + " amount=12 line=40",
            line);
    }

    [Fact]
    public void Cover_cuts_the_line_to_free_space()
    {
        var days = new[]
        {
            new CoverSale(3, 16, open: true),
            new CoverSale(4, 20, open: true),
        };

        var open = Wholesaler.Cover(
            Umbrella,
            currentDay: 5,
            nextDeliveryDay: 7,
            days,
            onHand: 10,
            fixtureCap: 30);
        var over = Wholesaler.Cover(
            Umbrella,
            currentDay: 5,
            nextDeliveryDay: 7,
            days,
            onHand: 40,
            fixtureCap: 30);

        Assert.Equal(20, open.Need);
        Assert.Equal(20, open.FixtureCap);
        Assert.Equal(0, over.Need);
        Assert.Equal(0, over.FixtureCap);
    }

    [Fact]
    public void Chain_on_hand_free_space_line_cuts_the_order()
    {
        // Chain: on hand → free space → line
        var days = new[]
        {
            new CoverSale(3, 16, open: true),
            new CoverSale(4, 20, open: true),
        };
        var onHand = 0;
        var capacity = Wholesaler.FixtureCapacity(
            Umbrella,
            new[] { new ShelfFixture("ba:itemname_productpanel", Umbrella, 22) });
        var space = Wholesaler.FreeSpace(onHand, capacity);
        var plan = Wholesaler.Cover(
            Umbrella,
            currentDay: 5,
            nextDeliveryDay: 7,
            days,
            onHand,
            capacity);

        Assert.Equal(22, capacity);
        Assert.Equal(22, space);
        Assert.Equal(space, plan.Need);
        Assert.Equal(space, plan.FixtureCap);
    }

    [Fact]
    public void Cover_keeps_a_line_that_fits_and_a_space_under_the_minimum()
    {
        var days = new[]
        {
            new CoverSale(3, 16, open: true),
            new CoverSale(4, 20, open: true),
        };

        var fits = Wholesaler.Cover(
            Umbrella,
            currentDay: 5,
            nextDeliveryDay: 7,
            days,
            onHand: 20,
            fixtureCap: 150);
        var shortSpace = Wholesaler.Cover(
            Umbrella,
            currentDay: 5,
            nextDeliveryDay: 7,
            days,
            onHand: 0,
            fixtureCap: 4);

        Assert.Equal(16, fits.Need);
        Assert.Equal(130, fits.FixtureCap);
        Assert.Equal(4, shortSpace.Need);
        Assert.Equal(4, shortSpace.FixtureCap);
    }

    [Fact]
    public void Chain_day_1519_paper_over_the_register_is_not_offered()
    {
        // Chain: on hand → register → free space → line
        var onHand = 265;
        var capacity = Wholesaler.FixtureCapacity(
            PaperBag,
            new[]
            {
                new ShelfFixture("ba:itemname_cashregister", PaperBag, Wholesaler.Hold("ba:itemname_cashregister", PaperBag)),
                new ShelfFixture("ba:itemname_productpanel", PaperBag, Wholesaler.Hold("ba:itemname_productpanel", PaperBag)),
            });
        var space = Wholesaler.FreeSpace(onHand, capacity);
        var plan = Wholesaler.Cover(
            PaperBag,
            currentDay: 1519,
            nextDeliveryDay: 1520,
            days: null,
            onHand: onHand,
            fixtureCap: capacity,
            stands: Wholesaler.RetailStands(PaperBag));
        var gap = Wholesaler.ShortLine("ba:street_seventhavenue 22", PaperBag, contractAmount: 10, plan.Need);

        Assert.Equal(Wholesaler.RegisterCap, capacity);
        Assert.Equal(0, space);
        Assert.Equal(space, plan.Need);
        Assert.Equal(space, plan.FixtureCap);
        Assert.Null(gap);
    }
}

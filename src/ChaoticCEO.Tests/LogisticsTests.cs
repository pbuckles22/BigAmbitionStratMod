using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class LogisticsTests
{
    [Fact]
    public void Logistics_a_stop_that_names_the_warehouse_still_serves_that_store()
    {
        var review = Logistics.Review(
            Api(),
            new[]
            {
                Stop("W1", "ba:street_firstavenue 38", Soda, null),
            },
            new[]
            {
                Hold("W1", "ba:street_fourthstreet", 46, Soda, 45),
            });

        Assert.Equal("W1", review.Services[0].Warehouse);
        Assert.Equal("ba:street_firstavenue 38", review.Services[0].Destination);
        Assert.Equal("food", review.Services[0].Family);
        Assert.Empty(review.Short);
        Assert.Null(review.Move);
    }

    [Fact]
    public void Logistics_the_larger_home_stock_beats_the_later_warehouse()
    {
        var review = Logistics.Review(
            Api(),
            new[]
            {
                Stop("ba:street_w0 1", "ba:street_side 9", Gift, null),
            },
            new[]
            {
                Hold("W0", "ba:street_w0", 1, Soda, 5),
                Hold("W1", "ba:street_a", 1, Gift, 30),
                Hold("W2", "ba:street_b", 2, Gift, 10),
            });

        Assert.Equal("W1", review.Move!.To);
        Assert.Equal("W0", review.Move.From);
        Assert.NotEqual("W2", review.Move.To);
    }

    [Fact]
    public void Logistics_the_serving_warehouse_is_the_hold_on_that_address()
    {
        var review = Logistics.Review(
            Api(),
            new[]
            {
                Stop("ba:street_fourthstreet 46", "ba:street_firstavenue 38", Soda, null),
            },
            new[]
            {
                Hold("W1", "ba:street_fourthstreet", 46, Soda, 45),
            });

        Assert.Single(review.Services);
        Assert.Equal("ba:street_firstavenue 38", review.Services[0].Destination);
        Assert.Equal("W1", review.Services[0].Warehouse);
        Assert.Equal("food", review.Services[0].Family);
        Assert.Equal(Soda, review.Services[0].Item);
        Assert.Empty(review.Mixed);
        Assert.Empty(review.Short);
        Assert.Null(review.Move);
    }

    [Fact]
    public void Logistics_one_mixed_family_names_the_warehouse()
    {
        var review = Logistics.Review(
            Api(),
            new[]
            {
                Stop("ba:street_fifthavenue 43", "ba:street_fifthavenue 36", Soda, null),
            },
            new[]
            {
                Hold("W1", "ba:street_fourthstreet", 46, Soda, 45),
                Hold("W2", "ba:street_fifthavenue", 43, Soda, 2),
                Hold("W2", "ba:street_fifthavenue", 43, Gift, 8),
                Hold("W2", "ba:street_fifthavenue", 43, Lamp, 4),
            });

        Assert.Single(review.Mixed);
        Assert.Equal("W2", review.Mixed[0].Warehouse);
        Assert.Equal("food, specialty", review.Mixed[0].Families);
        Assert.Equal("W2", review.Services[0].Warehouse);
        Assert.Empty(review.Short);
        Assert.Null(review.Move);
    }

    [Fact]
    public void Logistics_one_under_supplied_store_names_the_missing_family()
    {
        var review = Logistics.Review(
            Api(),
            new[]
            {
                Stop("ba:street_fourthstreet 46", "ba:street_side 9", Gift, null),
            },
            new[]
            {
                Hold("W1", "ba:street_fourthstreet", 46, Soda, 45),
            });

        Assert.Single(review.Short);
        Assert.Equal("ba:street_side 9", review.Short[0].Destination);
        Assert.Equal("W1", review.Short[0].Warehouse);
        Assert.Equal("specialty", review.Short[0].Family);
        Assert.Null(review.Move);
    }

    [Fact]
    public void Logistics_one_reassignment_moves_the_short_store()
    {
        var review = ReviewGiftOntoFood();

        Assert.NotNull(review.Move);
        Assert.Equal("ba:street_side 9", review.Move.Destination);
        Assert.Equal("W1", review.Move.From);
        Assert.Equal("W2", review.Move.To);
        Assert.Equal("specialty", review.Move.Family);
        Assert.Equal(
            "Reassign ba:street_side 9. From W1. To W2. Family specialty.",
            review.Move.Explain);
        Assert.DoesNotContain("AddDestination", review.Move.Explain);
        Assert.DoesNotContain("ShutdownBusiness", review.Move.Explain);
        Assert.DoesNotContain("SendBuyBuildingOffer", review.Move.Explain);
        Assert.DoesNotContain("SetPrice", review.Move.Explain);
    }

    [Fact]
    public void Logistics_a_supplied_store_keeps_no_reassignment()
    {
        var review = Logistics.Review(
            Api(),
            new[]
            {
                Stop("ba:street_fourthstreet 46", "ba:street_firstavenue 38", Soda, 1016),
            },
            new[]
            {
                Hold("W1", "ba:street_fourthstreet", 46, Soda, 45),
                Hold("W2", "ba:street_b", 2, Gift, 20),
            });

        Assert.Empty(review.Short);
        Assert.Null(review.Move);
    }

    [Fact]
    public void Logistics_a_mixed_warehouse_that_still_supplies_keeps_no_reassignment()
    {
        var review = Logistics.Review(
            Api(),
            new[]
            {
                Stop("ba:street_fifthavenue 43", "ba:street_fifthavenue 36", Soda, null),
            },
            new[]
            {
                Hold("W2", "ba:street_fifthavenue", 43, Soda, 5),
                Hold("W2", "ba:street_fifthavenue", 43, Gift, 8),
            });

        Assert.Single(review.Mixed);
        Assert.Empty(review.Short);
        Assert.Null(review.Move);
        Assert.Equal("food", review.Services[0].Family);
    }

    [Fact]
    public void Logistics_the_larger_cargo_amount_wins_the_serving_warehouse()
    {
        var review = Logistics.Review(
            Api(),
            new[]
            {
                Stop("ba:street_small 1", "ba:street_main 2", Soda, 1016),
                Stop("ba:street_big 1", "ba:street_main 2", Burger, 1016),
            },
            new[]
            {
                Hold("Wsmall", "ba:street_small", 1, Soda, 1),
                Hold("Wbig", "ba:street_big", 1, Burger, 100),
            });

        Assert.Equal("Wbig", review.Services[0].Warehouse);
        Assert.Equal("food", review.Services[0].Family);
        Assert.Null(review.Move);
    }

    [Fact]
    public void Logistics_a_tie_keeps_the_later_serving_warehouse()
    {
        var review = Logistics.Review(
            Api(),
            new[]
            {
                Stop("ba:street_a 1", "ba:street_main 2", Gift, 1016),
                Stop("ba:street_b 2", "ba:street_main 2", Gift, 1016),
            },
            new[]
            {
                Hold("W1", "ba:street_a", 1, Gift, 10),
                Hold("W2", "ba:street_b", 2, Gift, 10),
            });

        Assert.Equal("W2", review.Services[0].Warehouse);
        Assert.Empty(review.Short);
    }

    [Fact]
    public void Logistics_a_recurring_stop_beats_the_dated_warehouse()
    {
        var review = Logistics.Review(
            Api(),
            new[]
            {
                Stop("ba:street_big 1", "ba:street_main 2", Soda, 1016),
                Stop("ba:street_small 1", "ba:street_main 2", Burger, null),
            },
            new[]
            {
                Hold("Wbig", "ba:street_big", 1, Soda, 100),
                Hold("Wsmall", "ba:street_small", 1, Burger, 1),
            });

        Assert.Equal("Wsmall", review.Services[0].Warehouse);
        Assert.Equal(Burger, review.Services[0].Item);
        Assert.Empty(review.Short);
    }

    [Fact]
    public void Logistics_amount_zero_is_under_supplied()
    {
        var review = Logistics.Review(
            Api(),
            new[]
            {
                Stop("ba:street_a 1", "ba:street_side 9", Gift, null),
            },
            new[]
            {
                Hold("W1", "ba:street_a", 1, Gift, 0),
                Hold("W2", "ba:street_b", 2, Gift, 20),
            });

        Assert.Equal("W1", review.Short[0].Warehouse);
        Assert.Equal("W2", review.Move!.To);
        Assert.Equal("specialty", review.Move.Family);
    }

    [Fact]
    public void Logistics_a_zero_amount_second_family_still_mixes_and_is_not_a_target()
    {
        var review = Logistics.Review(
            Api(),
            new[]
            {
                Stop("ba:street_a 1", "ba:street_side 9", Gift, null),
            },
            new[]
            {
                Hold("W1", "ba:street_a", 1, Soda, 10),
                Hold("W1", "ba:street_a", 1, Gift, 0),
                Hold("W2", "ba:street_b", 2, Gift, 15),
            });

        Assert.Equal("W1", review.Mixed[0].Warehouse);
        Assert.Equal("food, specialty", review.Mixed[0].Families);
        Assert.Equal("W2", review.Move!.To);
        Assert.NotEqual("W1", review.Move.To);
    }

    [Fact]
    public void Logistics_a_lamp_is_not_a_service()
    {
        var review = Logistics.Review(
            Api(),
            new[]
            {
                Stop("ba:street_fourthstreet 46", "Lamp", Lamp, null),
                Stop("ba:street_fourthstreet 46", "ba:street_firstavenue 38", Soda, null),
            },
            new[]
            {
                Hold("W1", "ba:street_fourthstreet", 46, Lamp, 100),
                Hold("W1", "ba:street_fourthstreet", 46, Bag, 50),
                Hold("W1", "ba:street_fourthstreet", 46, Soda, 1),
            });

        Assert.Single(review.Services);
        Assert.Equal("ba:street_firstavenue 38", review.Services[0].Destination);
        Assert.Equal("food", review.Services[0].Family);
        Assert.Empty(review.Mixed);
        Assert.Null(review.Move);
    }

    [Fact]
    public void Logistics_the_same_family_on_another_item_supplies_the_store()
    {
        var review = Logistics.Review(
            Api(),
            new[]
            {
                Stop("ba:street_a 1", "ba:street_main 2", Soda, null),
            },
            new[]
            {
                Hold("W1", "ba:street_a", 1, Burger, 5),
            });

        Assert.Equal("food", review.Services[0].Family);
        Assert.Equal(Soda, review.Services[0].Item);
        Assert.Empty(review.Short);
        Assert.Null(review.Move);
    }

    [Fact]
    public void Logistics_same_family_amounts_add_before_the_warehouse_wins()
    {
        var review = Logistics.Review(
            Api(),
            new[]
            {
                Stop("ba:street_a 1", "ba:street_main 2", Soda, 1016),
                Stop("ba:street_b 2", "ba:street_main 2", Burger, 1016),
            },
            new[]
            {
                Hold("W1", "ba:street_a", 1, Soda, 3),
                Hold("W1", "ba:street_a", 1, Burger, 3),
                Hold("W2", "ba:street_b", 2, Soda, 4),
            });

        Assert.Equal("W1", review.Services[0].Warehouse);
        Assert.Empty(review.Short);
    }

    [Fact]
    public void Logistics_a_stocked_route_does_not_hide_a_short_family()
    {
        var review = Logistics.Review(
            Api(),
            new[]
            {
                Stop("ba:street_fourthstreet 46", "ba:street_main 2", Soda, null),
                Stop("ba:street_fourthstreet 46", "ba:street_main 2", Gift, null),
            },
            new[]
            {
                Hold("W1", "ba:street_fourthstreet", 46, Soda, 45),
                Hold("W2", "ba:street_b", 2, Gift, 20),
            });

        Assert.Equal(2, review.Services.Count);
        Assert.Equal("food", review.Services[0].Family);
        Assert.Equal("W1", review.Services[0].Warehouse);
        Assert.Equal("specialty", review.Services[1].Family);
        Assert.Single(review.Short);
        Assert.Equal("specialty", review.Short[0].Family);
        Assert.Equal("W2", review.Move!.To);
        Assert.Equal("ba:street_main 2", review.Move.Destination);
    }

    [Fact]
    public void Logistics_two_short_stores_keep_the_later_reassignment()
    {
        var review = Logistics.Review(
            Api(),
            new[]
            {
                Stop("ba:street_w0 1", "ba:street_a 1", Gift, null),
                Stop("ba:street_w0 1", "ba:street_b 2", Hair, null),
            },
            new[]
            {
                Hold("W0", "ba:street_w0", 1, Soda, 5),
                Hold("Wgift", "ba:street_gift", 3, Gift, 4),
                Hold("Wservice", "ba:street_service", 4, Hair, 4),
            });

        Assert.Equal(2, review.Short.Count);
        Assert.Equal("ba:street_b 2", review.Move!.Destination);
        Assert.Equal("Wservice", review.Move.To);
        Assert.Equal("service", review.Move.Family);
        Assert.Equal("W0", review.Move.From);
    }

    [Fact]
    public void Logistics_the_earlier_short_store_moves_when_the_later_one_has_no_home()
    {
        var review = Logistics.Review(
            Api(),
            new[]
            {
                Stop("ba:street_w0 1", "ba:street_a 1", Gift, null),
                Stop("ba:street_w0 1", "ba:street_b 2", Hair, null),
            },
            new[]
            {
                Hold("W0", "ba:street_w0", 1, Soda, 5),
                Hold("Wgift", "ba:street_gift", 3, Gift, 4),
            });

        Assert.Equal(2, review.Short.Count);
        Assert.Equal("ba:street_a 1", review.Move!.Destination);
        Assert.Equal("Wgift", review.Move.To);
        Assert.Equal("specialty", review.Move.Family);
    }

    [Fact]
    public void Logistics_a_tie_keeps_the_later_target_warehouse()
    {
        var review = Logistics.Review(
            Api(),
            new[]
            {
                Stop("ba:street_w0 1", "ba:street_side 9", Gift, null),
            },
            new[]
            {
                Hold("W0", "ba:street_w0", 1, Soda, 5),
                Hold("W1", "ba:street_a", 1, Gift, 10),
                Hold("W2", "ba:street_b", 2, Gift, 10),
            });

        Assert.Equal("W2", review.Move!.To);
        Assert.Equal("W0", review.Move.From);
    }

    [Fact]
    public void Logistics_a_mixed_warehouse_is_not_a_reassignment_target()
    {
        var review = Logistics.Review(
            Api(),
            new[]
            {
                Stop("ba:street_food 1", "ba:street_side 9", Gift, null),
            },
            new[]
            {
                Hold("Wfood", "ba:street_food", 1, Soda, 9),
                Hold("Wmix", "ba:street_mix", 2, Gift, 12),
                Hold("Wmix", "ba:street_mix", 2, Soda, 3),
            });

        Assert.Single(review.Short);
        Assert.Equal("Wmix", review.Mixed[0].Warehouse);
        Assert.Null(review.Move);
    }

    [Fact]
    public void Logistics_mixed_warehouses_keep_the_mix_order()
    {
        var review = Logistics.Review(
            Api(),
            Array.Empty<DeliveryStop>(),
            new[]
            {
                Hold("W10", "ba:street_a", 1, Burger, 1),
                Hold("W10", "ba:street_a", 1, "ba:itemname_notinstock", 1),
                Hold("W2", "ba:street_b", 2, Burger, 1),
                Hold("W2", "ba:street_b", 2, Gift, 1),
            });

        Assert.Equal(new[] { "W2", "W10" }, review.Mixed.Select(row => row.Warehouse));
        Assert.Equal("food, specialty", review.Mixed[0].Families);
        Assert.Equal("food, unknown", review.Mixed[1].Families);
        Assert.Null(review.Move);
    }

    [Fact]
    public void Logistics_an_unknown_item_can_move_to_the_warehouse_that_holds_it()
    {
        var review = Logistics.Review(
            Api(),
            new[]
            {
                Stop("ba:street_a 1", "ba:street_side 9", Unknown, null),
            },
            new[]
            {
                Hold("W1", "ba:street_a", 1, Soda, 4),
                Hold("W2", "ba:street_b", 2, Unknown, 3),
            });

        Assert.Equal("unknown", review.Short[0].Family);
        Assert.Equal("W2", review.Move!.To);
        Assert.Equal("unknown", review.Move.Family);
    }

    [Fact]
    public void Logistics_a_blank_destination_is_not_a_store()
    {
        var review = Logistics.Review(
            Api(),
            new DeliveryStop[]
            {
                null!,
                Stop("ba:street_fourthstreet 46", "", Soda, null),
                Stop("ba:street_fourthstreet 46", "ba:street_firstavenue 38", Soda, null),
            },
            new[]
            {
                Hold("W1", "ba:street_fourthstreet", 46, Soda, 45),
            });

        Assert.Single(review.Services);
        Assert.Equal("ba:street_firstavenue 38", review.Services[0].Destination);
    }

    [Fact]
    public void Logistics_an_empty_route_list_keeps_no_reassignment()
    {
        var review = Logistics.Review(Api(), Array.Empty<DeliveryStop>(), Array.Empty<WarehouseHold>());

        Assert.Empty(review.Services);
        Assert.Empty(review.Mixed);
        Assert.Empty(review.Short);
        Assert.Null(review.Move);
    }

    [Fact]
    public void Logistics_a_blank_api_keeps_no_review()
    {
        var stops = new[] { Stop("ba:street_fourthstreet 46", "ba:street_side 9", Gift, null) };
        var holds = new[]
        {
            Hold("W1", "ba:street_fourthstreet", 46, Soda, 45),
            Hold("W2", "ba:street_b", 2, Gift, 20),
        };

        Assert.Null(Logistics.Review(null, stops, holds).Move);
        Assert.Empty(Logistics.Review(null, stops, holds).Mixed);
        var blank = Logistics.Review(new ApiNote("", "", "", "", ""), stops, holds);
        Assert.Empty(blank.Services);
        Assert.Empty(blank.Short);
        Assert.Null(blank.Move);
    }

    [Fact]
    public void Logistics_a_recorded_build_reviews_without_calling_delivery()
    {
        var review = Logistics.Review(
            new ApiNote("3682", "", "", "", ""),
            new[]
            {
                Stop("ba:street_fourthstreet 46", "ba:street_side 9", Gift, null),
            },
            new[]
            {
                Hold("W1", "ba:street_fourthstreet", 46, Soda, 45),
                Hold("W2", "ba:street_b", 2, Gift, 20),
            });

        Assert.Equal("W2", review.Move!.To);
        Assert.DoesNotContain("AddDestination", review.Move.Explain);
    }

    [Fact]
    public void Logistics_from_routes_reads_the_written_file_and_the_score_does_not_choose_the_store()
    {
        using var dir = new TempRoutes();
        var scored = new[]
        {
            new CloseScoreLine(
                "ba:street_fourthavenue",
                28,
                "Close ba:street_fourthavenue 28. After rent 1. Streak 0. Trend 0."),
        };
        RouteDump.Write(dir.Path, GiftStops(), GiftHolds());

        var review = Logistics.FromRoutes(
            scored,
            Api(),
            RouteDump.RoutesPath(dir.Path),
            RouteDump.WarehousesPath(dir.Path));

        Assert.Equal("ba:street_side 9", review.Move!.Destination);
        Assert.Equal("W2", review.Move.To);
        Assert.DoesNotContain("fourthavenue", review.Move.Explain);
        Assert.NotEqual(scored[0].Street, review.Move.Destination);
    }

    [Fact]
    public void Logistics_an_empty_score_still_reads_the_routes()
    {
        using var dir = new TempRoutes();
        RouteDump.Write(dir.Path, GiftStops(), GiftHolds());

        var review = Logistics.FromRoutes(
            Array.Empty<CloseScoreLine>(),
            Api(),
            RouteDump.RoutesPath(dir.Path),
            RouteDump.WarehousesPath(dir.Path));

        Assert.Equal(
            "Reassign ba:street_side 9. From W1. To W2. Family specialty.",
            review.Move!.Explain);
    }

    [Fact]
    public void Logistics_a_missing_score_keeps_no_review()
    {
        using var dir = new TempRoutes();
        RouteDump.Write(dir.Path, GiftStops(), GiftHolds());

        var review = Logistics.FromRoutes(
            null,
            Api(),
            RouteDump.RoutesPath(dir.Path),
            RouteDump.WarehousesPath(dir.Path));

        Assert.Empty(review.Services);
        Assert.Null(review.Move);
    }

    [Fact]
    public void Logistics_from_routes_a_blank_api_keeps_no_reassignment()
    {
        using var dir = new TempRoutes();
        RouteDump.Write(dir.Path, GiftStops(), GiftHolds());

        var review = Logistics.FromRoutes(
            Array.Empty<CloseScoreLine>(),
            new ApiNote("", "", "", "", ""),
            RouteDump.RoutesPath(dir.Path),
            RouteDump.WarehousesPath(dir.Path));

        Assert.Null(review.Move);
        Assert.Empty(review.Services);
    }

    private static LogisticsReview ReviewGiftOntoFood()
    {
        return Logistics.Review(Api(), GiftStops(), GiftHolds());
    }

    private static DeliveryStop[] GiftStops()
    {
        return new[]
        {
            Stop("ba:street_fourthstreet 46", "ba:street_firstavenue 38", Soda, 1016),
            Stop("ba:street_fourthstreet 46", "ba:street_side 9", Gift, null),
        };
    }

    private static WarehouseHold[] GiftHolds()
    {
        return new[]
        {
            Hold("W1", "ba:street_fourthstreet", 46, Soda, 45),
            Hold("W2", "ba:street_b", 2, Gift, 20),
        };
    }

    private static ApiNote Api()
    {
        return new ApiNote(
            "3682",
            "Helpers.BusinessHelper.ShutdownBusiness",
            "BizManPresentation.SendBuyBuildingOffer",
            "Buildings.Office.Headquarters.PricingManagerPlan.SetPrice",
            "UI.Smartphone.Apps.BizMan.LogisticsManagers.LogisticsManagerPlanUI.AddDestination");
    }

    private static DeliveryStop Stop(string warehouse, string destination, string item, int? day)
    {
        return new DeliveryStop(warehouse, destination, item, day, "");
    }

    private static WarehouseHold Hold(string warehouse, string street, int number, string item, int amount)
    {
        return new WarehouseHold(warehouse, street, number, item, amount);
    }

    private const string Soda = "ba:itemname_sodacan";

    private const string Gift = "ba:itemname_cheapgift";

    private const string Burger = "ba:itemname_burger";

    private const string Hair = "ba:itemname_haircareproduct";

    private const string Lamp = "ba:itemname_ceilinglampwarehouse";

    private const string Bag = "ba:itemname_paperbag";

    private const string Unknown = "ba:itemname_notinstock";

    private sealed class TempRoutes : IDisposable
    {
        public TempRoutes()
        {
            Path = Directory.CreateTempSubdirectory("chaotic-logistics").FullName;
        }

        public string Path { get; }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}

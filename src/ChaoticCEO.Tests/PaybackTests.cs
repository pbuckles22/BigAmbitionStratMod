using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class PaybackTests
{
    [Fact]
    public void Payback_price_over_rent_is_the_payback_and_names_one_street_and_one_building()
    {
        var plan = Payback.Decide(
            Api(),
            Quiet(),
            new[]
            {
                Day(1013, Rent("ba:street_fourthavenue", 28, 1099f)),
            },
            new[]
            {
                new BuildingAsk("ba:street_fourthavenue", 28, 10990d),
            });

        Assert.True(plan.Bought);
        Assert.Equal(10d, plan.Days);
        Assert.Equal("ba:street_fourthavenue", plan.Street);
        Assert.Equal(28, plan.Number);
        Assert.Equal(
            "Open ba:street_fourthavenue. Buy ba:street_fourthavenue 28. Payback 10.",
            plan.Explain);
        Assert.DoesNotContain("SendBuyBuildingOffer", plan.Explain);
        Assert.DoesNotContain("ShutdownBusiness", plan.Explain);
        Assert.DoesNotContain("SetPrice", plan.Explain);
        Assert.DoesNotContain("AddDestination", plan.Explain);
    }

    [Fact]
    public void Payback_a_fractional_price_over_rent_stays_the_payback()
    {
        var plan = Payback.Decide(
            Api(),
            Quiet(),
            new[]
            {
                Day(1013, Rent("ba:street_main", 4, 40f)),
            },
            new[]
            {
                new BuildingAsk("ba:street_main", 4, 100d),
            });

        Assert.Equal(2.5d, plan.Days);
        Assert.Equal("ba:street_main", plan.Street);
        Assert.Equal(4, plan.Number);
        Assert.Equal("Open ba:street_main. Buy ba:street_main 4. Payback 2.5.", plan.Explain);
    }

    [Fact]
    public void Payback_the_shorter_payback_wins_over_the_later_building()
    {
        var plan = Payback.Decide(
            Api(),
            Quiet(),
            new[]
            {
                Day(
                    1013,
                    Rent("ba:street_early", 1, 20f),
                    Rent("ba:street_late", 9, 1000f)),
            },
            new[]
            {
                new BuildingAsk("ba:street_early", 1, 40d),
                new BuildingAsk("ba:street_late", 9, 50000d),
            });

        Assert.Equal("ba:street_early", plan.Street);
        Assert.Equal(1, plan.Number);
        Assert.Equal(2d, plan.Days);
        Assert.NotEqual("ba:street_late", plan.Street);
    }

    [Fact]
    public void Payback_the_shorter_payback_wins_over_the_larger_rent()
    {
        var plan = Payback.Decide(
            Api(),
            Quiet(),
            new[]
            {
                Day(
                    1013,
                    Rent("ba:street_big", 28, 1099f),
                    Rent("ba:street_small", 9, 4f)),
            },
            new[]
            {
                new BuildingAsk("ba:street_big", 28, 1099000d),
                new BuildingAsk("ba:street_small", 9, 8d),
            });

        Assert.Equal("ba:street_small", plan.Street);
        Assert.Equal(9, plan.Number);
        Assert.Equal(2d, plan.Days);
    }

    [Fact]
    public void Payback_a_tie_keeps_the_later_building()
    {
        var plan = Payback.Decide(
            Api(),
            Quiet(),
            new[]
            {
                Day(
                    1013,
                    Rent("ba:street_first", 1, 10f),
                    Rent("ba:street_second", 2, 5f)),
            },
            new[]
            {
                new BuildingAsk("ba:street_first", 1, 100d),
                new BuildingAsk("ba:street_second", 2, 50d),
            });

        Assert.Equal(10d, plan.Days);
        Assert.Equal("ba:street_second", plan.Street);
        Assert.Equal(2, plan.Number);
    }

    [Fact]
    public void Payback_one_street_keeps_the_shorter_building_on_that_street()
    {
        var plan = Payback.Decide(
            Api(),
            Quiet(),
            new[]
            {
                Day(
                    1013,
                    Rent("ba:street_main", 1, 10f),
                    Rent("ba:street_main", 2, 10f)),
            },
            new[]
            {
                new BuildingAsk("ba:street_main", 2, 100d),
                new BuildingAsk("ba:street_main", 1, 20d),
            });

        Assert.Equal("ba:street_main", plan.Street);
        Assert.Equal(1, plan.Number);
        Assert.Equal(2d, plan.Days);
    }

    [Fact]
    public void Payback_zero_rent_is_not_a_buy()
    {
        var plan = Payback.Decide(
            Api(),
            Quiet(),
            new[]
            {
                Day(
                    1013,
                    Rent("ba:street_free", 1, 0f),
                    Rent("ba:street_paid", 2, 10f)),
            },
            new[]
            {
                new BuildingAsk("ba:street_free", 1, 10d),
                new BuildingAsk("ba:street_paid", 2, 30d),
            });

        Assert.Equal("ba:street_paid", plan.Street);
        Assert.Equal(2, plan.Number);
        Assert.Equal(3d, plan.Days);
    }

    [Fact]
    public void Payback_a_building_with_no_positive_rent_keeps_no_buy()
    {
        var plan = Payback.Decide(
            Api(),
            Quiet(),
            new[]
            {
                Day(1013, Rent("ba:street_free", 1, 0f)),
            },
            new[]
            {
                new BuildingAsk("ba:street_free", 1, 500d),
            });

        Assert.False(plan.Bought);
        Assert.Equal("", plan.Street);
        Assert.Equal(0, plan.Number);
        Assert.Equal(0d, plan.Days);
        Assert.Equal("", plan.Explain);
    }

    [Fact]
    public void Payback_a_zero_price_is_not_a_buy()
    {
        var plan = Payback.Decide(
            Api(),
            Quiet(),
            new[]
            {
                Day(
                    1013,
                    Rent("ba:street_gift", 1, 80f),
                    Rent("ba:street_paid", 2, 10f)),
            },
            new[]
            {
                new BuildingAsk("ba:street_gift", 1, 0d),
                new BuildingAsk("ba:street_paid", 2, 40d),
            });

        Assert.Equal("ba:street_paid", plan.Street);
        Assert.Equal(2, plan.Number);
        Assert.Equal(4d, plan.Days);
    }

    [Fact]
    public void Payback_a_negative_price_is_not_a_buy()
    {
        var plan = Payback.Decide(
            Api(),
            Quiet(),
            new[]
            {
                Day(1013, Rent("ba:street_main", 3, 10f)),
            },
            new[]
            {
                new BuildingAsk("ba:street_main", 3, -20d),
            });

        Assert.False(plan.Bought);
        Assert.Equal("", plan.Street);
    }

    [Fact]
    public void Payback_a_negative_rent_is_not_a_buy()
    {
        var plan = Payback.Decide(
            Api(),
            Quiet(),
            new[]
            {
                Day(1013, Rent("ba:street_main", 3, -10f)),
            },
            new[]
            {
                new BuildingAsk("ba:street_main", 3, 100d),
            });

        Assert.False(plan.Bought);
    }

    [Fact]
    public void Payback_a_missing_statement_keeps_no_buy()
    {
        var plan = Payback.Decide(
            Api(),
            Quiet(),
            new[]
            {
                Day(1013, Rent("ba:street_other", 1, 50f)),
            },
            new[]
            {
                new BuildingAsk("ba:street_main", 8, 100d),
            });

        Assert.False(plan.Bought);
        Assert.Equal("", plan.Street);
    }

    [Fact]
    public void Payback_the_later_day_supplies_the_rent()
    {
        var plan = Payback.Decide(
            Api(),
            Quiet(),
            new[]
            {
                Day(1012, Rent("ba:street_main", 5, 100f)),
                Day(1013, Rent("ba:street_main", 5, 10f)),
            },
            new[]
            {
                new BuildingAsk("ba:street_main", 5, 100d),
            });

        Assert.Equal(10d, plan.Days);
        Assert.Equal(5, plan.Number);
        Assert.Equal("ba:street_main", plan.Street);
    }

    [Fact]
    public void Payback_a_later_zero_rent_clears_an_earlier_positive_rent()
    {
        var plan = Payback.Decide(
            Api(),
            Quiet(),
            new[]
            {
                Day(1012, Rent("ba:street_old", 1, 100f)),
                Day(
                    1013,
                    Rent("ba:street_old", 1, 0f),
                    Rent("ba:street_kept", 2, 10f)),
            },
            new[]
            {
                new BuildingAsk("ba:street_old", 1, 10d),
                new BuildingAsk("ba:street_kept", 2, 40d),
            });

        Assert.Equal("ba:street_kept", plan.Street);
        Assert.Equal(2, plan.Number);
        Assert.Equal(4d, plan.Days);
        Assert.DoesNotContain("old", plan.Explain);
    }

    [Fact]
    public void Payback_the_higher_day_number_supplies_the_rent_when_the_days_are_out_of_order()
    {
        var plan = Payback.Decide(
            Api(),
            Quiet(),
            new[]
            {
                Day(1013, Rent("ba:street_main", 5, 10f)),
                Day(1012, Rent("ba:street_main", 5, 100f)),
            },
            new[]
            {
                new BuildingAsk("ba:street_main", 5, 100d),
            });

        Assert.Equal(10d, plan.Days);
        Assert.Equal(5, plan.Number);
    }

    [Fact]
    public void Payback_a_blank_street_or_a_zero_number_is_not_a_buy()
    {
        var plan = Payback.Decide(
            Api(),
            Quiet(),
            new[]
            {
                Day(
                    1013,
                    Rent("", 4, 10f),
                    Rent("ba:street_main", 0, 10f),
                    Rent("ba:street_paid", 2, 10f)),
            },
            new[]
            {
                new BuildingAsk("", 4, 10d),
                new BuildingAsk("ba:street_main", 0, 10d),
                new BuildingAsk("ba:street_paid", 2, 40d),
            });

        Assert.Equal("ba:street_paid", plan.Street);
        Assert.Equal(2, plan.Number);
        Assert.Equal(4d, plan.Days);
    }

    [Fact]
    public void Payback_a_missing_ledger_file_keeps_no_building()
    {
        var plan = Payback.FromLedger(
            Quiet(),
            Api(),
            Path.Combine(Path.GetTempPath(), "chaotic-payback-missing", "statements.csv"),
            new[]
            {
                new BuildingAsk("ba:street_main", 1, 20d),
            });

        Assert.False(plan.Bought);
        Assert.Equal("", plan.Street);
    }

    [Fact]
    public void Payback_the_later_row_on_that_day_supplies_the_rent()
    {
        var plan = Payback.Decide(
            Api(),
            Quiet(),
            new[]
            {
                Day(
                    1013,
                    Rent("ba:street_main", 5, 1f),
                    Rent("ba:street_main", 5, 50f)),
            },
            new[]
            {
                new BuildingAsk("ba:street_main", 5, 100d),
            });

        Assert.Equal(2d, plan.Days);
        Assert.Equal("ba:street_main", plan.Street);
        Assert.Equal(5, plan.Number);
    }

    [Fact]
    public void Payback_the_logistics_move_does_not_choose_the_building()
    {
        var review = Logistics.Review(
            Api(),
            new[]
            {
                new DeliveryStop("ba:street_fourthstreet 46", "ba:street_side 9", "ba:itemname_cheapgift", null, ""),
            },
            new[]
            {
                new WarehouseHold("W1", "ba:street_fourthstreet", 46, "ba:itemname_sodacan", 45),
                new WarehouseHold("W2", "ba:street_b", 2, "ba:itemname_cheapgift", 20),
            });

        var plan = Payback.Decide(
            Api(),
            review,
            new[]
            {
                Day(
                    1013,
                    Rent("ba:street_fourthavenue", 28, 1099f),
                    Rent("ba:street_side", 9, 4f)),
            },
            new[]
            {
                new BuildingAsk("ba:street_fourthavenue", 28, 10990d),
                new BuildingAsk("ba:street_side", 9, 4000d),
            });

        Assert.Equal("ba:street_side 9", review.Move!.Destination);
        Assert.Equal("ba:street_fourthavenue", plan.Street);
        Assert.Equal(28, plan.Number);
        Assert.Equal(10d, plan.Days);
        Assert.DoesNotContain("side", plan.Explain);
    }

    [Fact]
    public void Payback_an_empty_offer_list_keeps_no_building()
    {
        var plan = Payback.Decide(
            Api(),
            Quiet(),
            new[]
            {
                Day(1013, Rent("ba:street_main", 1, 10f)),
            },
            Array.Empty<BuildingAsk>());

        Assert.False(plan.Bought);
        Assert.Equal("", plan.Street);
    }

    [Fact]
    public void Payback_a_blank_api_keeps_no_building()
    {
        var plan = Payback.Decide(
            new ApiNote("", "", "", "", ""),
            Quiet(),
            new[]
            {
                Day(1013, Rent("ba:street_main", 1, 10f)),
            },
            new[]
            {
                new BuildingAsk("ba:street_main", 1, 20d),
            });

        Assert.False(plan.Bought);
        Assert.Equal("", plan.Explain);
    }

    [Fact]
    public void Payback_a_missing_review_keeps_no_building()
    {
        var plan = Payback.Decide(
            Api(),
            null,
            new[]
            {
                Day(1013, Rent("ba:street_main", 1, 10f)),
            },
            new[]
            {
                new BuildingAsk("ba:street_main", 1, 20d),
            });

        Assert.False(plan.Bought);
    }

    [Fact]
    public void Payback_from_ledger_reads_the_written_rent()
    {
        using var dir = new TempPayback();
        StatementDump.Write(
            dir.Path,
            1014,
            new[]
            {
                Day(1012, Rent("ba:street_fourthavenue", 28, 1f)),
                Day(
                    1013,
                    Rent("ba:street_fourthavenue", 28, 100f),
                    Rent("ba:street_side", 9, 4f)),
            });

        var plan = Payback.FromLedger(
            Quiet(),
            Api(),
            StatementDump.CsvPath(dir.Path),
            new[]
            {
                new BuildingAsk("ba:street_fourthavenue", 28, 10d),
                new BuildingAsk("ba:street_side", 9, 8d),
                new BuildingAsk("ba:street_hq", 1, 10d),
            });

        Assert.Equal("ba:street_fourthavenue", plan.Street);
        Assert.Equal(28, plan.Number);
        Assert.Equal(0.1d, plan.Days);
        Assert.Equal(
            "Open ba:street_fourthavenue. Buy ba:street_fourthavenue 28. Payback 0.1.",
            plan.Explain);
        Assert.DoesNotContain("SendBuyBuildingOffer", plan.Explain);
    }

    [Fact]
    public void Payback_from_ledger_the_later_written_row_supplies_the_rent()
    {
        using var dir = new TempPayback();
        StatementDump.Write(
            dir.Path,
            1014,
            new[]
            {
                Day(
                    1013,
                    Rent("ba:street_main", 5, 1f),
                    Rent("ba:street_main", 5, 50f)),
            });

        var plan = Payback.FromLedger(
            Quiet(),
            Api(),
            StatementDump.CsvPath(dir.Path),
            new[]
            {
                new BuildingAsk("ba:street_main", 5, 100d),
            });

        Assert.Equal(2d, plan.Days);
        Assert.Equal("ba:street_main", plan.Street);
        Assert.Equal(5, plan.Number);
        Assert.Equal("Open ba:street_main. Buy ba:street_main 5. Payback 2.", plan.Explain);
    }

    private static LogisticsReview Quiet()
    {
        return new LogisticsReview(
            Array.Empty<StoreService>(),
            Array.Empty<MixedWarehouse>(),
            Array.Empty<ShortStore>(),
            null);
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

    private static FinancialDay Day(int day, params StatementRow[] rows)
    {
        return new FinancialDay(day, rows);
    }

    private static StatementRow Rent(string street, int number, float rent)
    {
        return new StatementRow(street, number, 0f, 0f, 0f, rent, 0f, 0f, 0f);
    }

    private sealed class TempPayback : IDisposable
    {
        public TempPayback()
        {
            Path = Directory.CreateTempSubdirectory("chaotic-payback").FullName;
        }

        public string Path { get; }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}

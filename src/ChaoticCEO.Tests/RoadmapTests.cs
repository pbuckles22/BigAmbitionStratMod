using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class RoadmapTests
{
    [Fact]
    public void Roadmap_the_larger_daily_gain_is_first_and_the_open_gain_is_the_rent()
    {
        var payback = new RentPayback("ba:street_fourthavenue", 28, 1d, true);
        var steps = Roadmap.Order(
            Api(),
            Lines("ba:street_side", 9, "ba:street_fourthavenue", 28),
            Quiet(),
            payback,
            new[]
            {
                Day(
                    1013,
                    Row("ba:street_fourthavenue", 28, 1099f, 50687f),
                    Row("ba:street_side", 9, 4f, -30f)),
            },
            3);

        Assert.Equal(2, steps.Count);
        Assert.Equal("open", steps[0].Kind);
        Assert.Equal("ba:street_fourthavenue", steps[0].Street);
        Assert.Equal(28, steps[0].Number);
        Assert.Equal(1099d, steps[0].DailyGain);
        Assert.NotEqual(payback.Days, steps[0].DailyGain);
        Assert.Equal(
            "Open ba:street_fourthavenue. Buy ba:street_fourthavenue 28. Daily gain 1099.",
            steps[0].Explain);
        Assert.Equal("close", steps[1].Kind);
        Assert.Equal("ba:street_side", steps[1].Street);
        Assert.Equal(9, steps[1].Number);
        Assert.Equal(30d, steps[1].DailyGain);
        Assert.Equal("Close ba:street_side 9. Daily gain 30.", steps[1].Explain);
        Assert.DoesNotContain("SendBuyBuildingOffer", steps[0].Explain);
        Assert.DoesNotContain("ShutdownBusiness", steps[0].Explain);
        Assert.DoesNotContain("SetPrice", steps[0].Explain);
        Assert.DoesNotContain("AddDestination", steps[0].Explain);
    }

    [Fact]
    public void Roadmap_the_larger_loss_is_first_when_it_beats_the_rent()
    {
        var steps = Roadmap.Order(
            Api(),
            Lines("ba:street_side", 9, "ba:street_fourthavenue", 28),
            Quiet(),
            new RentPayback("ba:street_fourthavenue", 28, 10d, true),
            new[]
            {
                Day(
                    1013,
                    Row("ba:street_fourthavenue", 28, 1099f, 100f),
                    Row("ba:street_side", 9, 4f, -5000f)),
            },
            3);

        Assert.Equal("close", steps[0].Kind);
        Assert.Equal("ba:street_side", steps[0].Street);
        Assert.Equal(9, steps[0].Number);
        Assert.Equal(5000d, steps[0].DailyGain);
        Assert.Equal("open", steps[1].Kind);
        Assert.Equal(1099d, steps[1].DailyGain);
        Assert.NotEqual("ba:street_fourthavenue", steps[0].Street);
    }

    [Fact]
    public void Roadmap_a_tie_keeps_the_later_step()
    {
        var steps = Roadmap.Order(
            Api(),
            Lines("ba:street_side", 9),
            Quiet(),
            new RentPayback("ba:street_fourthavenue", 28, 10d, true),
            new[]
            {
                Day(
                    1013,
                    Row("ba:street_fourthavenue", 28, 30f, 100f),
                    Row("ba:street_side", 9, 4f, -30f)),
            },
            3);

        Assert.Equal(2, steps.Count);
        Assert.Equal("open", steps[0].Kind);
        Assert.Equal(30d, steps[0].DailyGain);
        Assert.Equal("close", steps[1].Kind);
        Assert.Equal("ba:street_side", steps[1].Street);
        Assert.Equal(30d, steps[1].DailyGain);
    }

    [Fact]
    public void Roadmap_a_tie_keeps_the_later_store()
    {
        var steps = Roadmap.Order(
            Api(),
            Lines("ba:street_early", 1, "ba:street_late", 2),
            Quiet(),
            new RentPayback("", 0, 0d, false),
            new[]
            {
                Day(
                    1013,
                    Row("ba:street_early", 1, 0f, -10f),
                    Row("ba:street_late", 2, 0f, -10f)),
            },
            3);

        Assert.Equal(2, steps.Count);
        Assert.Equal("close", steps[0].Kind);
        Assert.Equal("ba:street_late", steps[0].Street);
        Assert.Equal(2, steps[0].Number);
        Assert.Equal(10d, steps[0].DailyGain);
        Assert.Equal("ba:street_early", steps[1].Street);
    }

    [Fact]
    public void Roadmap_top_n_keeps_the_larger_gains()
    {
        var steps = Roadmap.Order(
            Api(),
            Lines("ba:street_small", 1, "ba:street_big", 2),
            Quiet(),
            new RentPayback("ba:street_mid", 3, 1d, true),
            new[]
            {
                Day(
                    1013,
                    Row("ba:street_small", 1, 0f, -10f),
                    Row("ba:street_big", 2, 0f, -50f),
                    Row("ba:street_mid", 3, 30f, 1f)),
            },
            2);

        Assert.Equal(2, steps.Count);
        Assert.Equal("close", steps[0].Kind);
        Assert.Equal("ba:street_big", steps[0].Street);
        Assert.Equal(50d, steps[0].DailyGain);
        Assert.Equal("open", steps[1].Kind);
        Assert.Equal("ba:street_mid", steps[1].Street);
        Assert.Equal(30d, steps[1].DailyGain);
    }

    [Fact]
    public void Roadmap_a_wider_top_n_keeps_every_step_in_order()
    {
        var steps = Roadmap.Order(
            Api(),
            Lines("ba:street_side", 9),
            Quiet(),
            new RentPayback("ba:street_fourthavenue", 28, 1d, true),
            new[]
            {
                Day(
                    1013,
                    Row("ba:street_fourthavenue", 28, 1099f, 1f),
                    Row("ba:street_side", 9, 4f, -30f)),
            },
            9);

        Assert.Equal(2, steps.Count);
        Assert.Equal("open", steps[0].Kind);
        Assert.Equal("close", steps[1].Kind);
    }

    [Fact]
    public void Roadmap_a_fractional_loss_stays_the_daily_gain()
    {
        var steps = Roadmap.Order(
            Api(),
            Lines("ba:street_main", 4),
            Quiet(),
            new RentPayback("ba:street_open", 8, 1d, true),
            new[]
            {
                Day(
                    1013,
                    Row("ba:street_main", 4, 0f, -2.5f),
                    Row("ba:street_open", 8, 40f, 1f)),
            },
            3);

        Assert.Equal(40d, steps[0].DailyGain);
        Assert.Equal(2.5d, steps[1].DailyGain);
        Assert.Equal("Close ba:street_main 4. Daily gain 2.5.", steps[1].Explain);
        Assert.Equal("Open ba:street_open. Buy ba:street_open 8. Daily gain 40.", steps[0].Explain);
    }

    [Fact]
    public void Roadmap_a_later_day_supplies_the_profit_and_the_rent()
    {
        var steps = Roadmap.Order(
            Api(),
            Lines("ba:street_side", 9),
            Quiet(),
            new RentPayback("ba:street_fourthavenue", 28, 1d, true),
            new[]
            {
                Day(
                    1012,
                    Row("ba:street_fourthavenue", 28, 100f, 1f),
                    Row("ba:street_side", 9, 4f, -40f)),
                Day(
                    1013,
                    Row("ba:street_fourthavenue", 28, 7f, 1f),
                    Row("ba:street_side", 9, 4f, -5f)),
            },
            3);

        Assert.Equal(7d, steps[0].DailyGain);
        Assert.Equal("open", steps[0].Kind);
        Assert.Equal(5d, steps[1].DailyGain);
        Assert.Equal("close", steps[1].Kind);
    }

    [Fact]
    public void Roadmap_a_later_row_on_the_same_day_supplies_the_profit_and_the_rent()
    {
        var steps = Roadmap.Order(
            Api(),
            Lines("ba:street_side", 9),
            Quiet(),
            new RentPayback("ba:street_fourthavenue", 28, 1d, true),
            new[]
            {
                Day(
                    1013,
                    Row("ba:street_fourthavenue", 28, 1f, 1f),
                    Row("ba:street_side", 9, 4f, -10f),
                    Row("ba:street_fourthavenue", 28, 9f, 1f),
                    Row("ba:street_side", 9, 4f, -40f)),
            },
            3);

        Assert.Equal("close", steps[0].Kind);
        Assert.Equal(40d, steps[0].DailyGain);
        Assert.Equal("open", steps[1].Kind);
        Assert.Equal(9d, steps[1].DailyGain);
    }

    [Fact]
    public void Roadmap_a_later_zero_profit_clears_the_close()
    {
        var steps = Roadmap.Order(
            Api(),
            Lines("ba:street_side", 9),
            Quiet(),
            new RentPayback("ba:street_fourthavenue", 28, 1d, true),
            new[]
            {
                Day(1012, Row("ba:street_side", 9, 4f, -40f)),
                Day(
                    1013,
                    Row("ba:street_fourthavenue", 28, 12f, 1f),
                    Row("ba:street_side", 9, 4f, 0f)),
            },
            3);

        Assert.Single(steps);
        Assert.Equal("open", steps[0].Kind);
        Assert.Equal(12d, steps[0].DailyGain);
    }

    [Fact]
    public void Roadmap_a_later_zero_rent_clears_the_open()
    {
        var steps = Roadmap.Order(
            Api(),
            Lines("ba:street_side", 9),
            Quiet(),
            new RentPayback("ba:street_fourthavenue", 28, 10d, true),
            new[]
            {
                Day(1012, Row("ba:street_fourthavenue", 28, 100f, 1f)),
                Day(
                    1013,
                    Row("ba:street_fourthavenue", 28, 0f, 1f),
                    Row("ba:street_side", 9, 4f, -15f)),
            },
            3);

        Assert.Single(steps);
        Assert.Equal("close", steps[0].Kind);
        Assert.Equal(15d, steps[0].DailyGain);
    }

    [Fact]
    public void Roadmap_a_green_store_is_not_a_close()
    {
        var steps = Roadmap.Order(
            Api(),
            Lines("ba:street_side", 9),
            Quiet(),
            new RentPayback("", 0, 0d, false),
            new[]
            {
                Day(1012, Row("ba:street_side", 9, 4f, -40f)),
                Day(1013, Row("ba:street_side", 9, 4f, 8f)),
            },
            3);

        Assert.Empty(steps);
    }

    [Fact]
    public void Roadmap_an_unbought_payback_keeps_the_close()
    {
        var steps = Roadmap.Order(
            Api(),
            Lines("ba:street_side", 9, "ba:street_fourthavenue", 28),
            Quiet(),
            new RentPayback("ba:street_fourthavenue", 28, 10d, false),
            new[]
            {
                Day(
                    1013,
                    Row("ba:street_fourthavenue", 28, 1099f, 1f),
                    Row("ba:street_side", 9, 4f, -30f)),
            },
            3);

        Assert.Single(steps);
        Assert.Equal("close", steps[0].Kind);
        Assert.Equal(30d, steps[0].DailyGain);
    }

    [Fact]
    public void Roadmap_an_empty_score_keeps_the_open()
    {
        var steps = Roadmap.Order(
            Api(),
            Array.Empty<CloseScoreLine>(),
            Quiet(),
            new RentPayback("ba:street_fourthavenue", 28, 1d, true),
            new[]
            {
                Day(
                    1013,
                    Row("ba:street_fourthavenue", 28, 1099f, 1f),
                    Row("ba:street_side", 9, 4f, -30f)),
            },
            3);

        Assert.Single(steps);
        Assert.Equal("open", steps[0].Kind);
        Assert.Equal(1099d, steps[0].DailyGain);
    }

    [Fact]
    public void Roadmap_a_number_of_zero_is_not_a_step()
    {
        var steps = Roadmap.Order(
            Api(),
            Lines("ba:street_side", 0),
            Quiet(),
            new RentPayback("ba:street_fourthavenue", 0, 1d, true),
            new[]
            {
                Day(
                    1013,
                    Row("ba:street_fourthavenue", 0, 50f, 1f),
                    Row("ba:street_side", 0, 4f, -100f)),
            },
            3);

        Assert.Empty(steps);
    }

    [Fact]
    public void Roadmap_a_blank_street_is_not_a_step()
    {
        var steps = Roadmap.Order(
            Api(),
            new[]
            {
                new CloseScoreLine("", 9, ""),
            },
            Quiet(),
            new RentPayback("", 9, 1d, true),
            new[]
            {
                Day(1013, Row("", 9, 50f, -20f)),
            },
            3);

        Assert.Empty(steps);
    }

    [Fact]
    public void Roadmap_the_logistics_move_does_not_choose_the_first_action()
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

        var steps = Roadmap.Order(
            Api(),
            Lines("ba:street_side", 9, "ba:street_fourthavenue", 28),
            review,
            new RentPayback("ba:street_fourthavenue", 28, 1d, true),
            new[]
            {
                Day(
                    1013,
                    Row("ba:street_fourthavenue", 28, 1099f, 1f),
                    Row("ba:street_side", 9, 4f, -30f)),
            },
            3);

        Assert.Equal("ba:street_side 9", review.Move!.Destination);
        Assert.Equal("open", steps[0].Kind);
        Assert.Equal("ba:street_fourthavenue", steps[0].Street);
        Assert.Equal(1099d, steps[0].DailyGain);
        Assert.DoesNotContain("Reassign", steps[0].Explain);
        Assert.DoesNotContain("side", steps[0].Explain);
    }

    [Fact]
    public void Roadmap_a_top_of_zero_keeps_no_steps()
    {
        var steps = Roadmap.Order(
            Api(),
            Lines("ba:street_side", 9),
            Quiet(),
            new RentPayback("ba:street_fourthavenue", 28, 1d, true),
            new[]
            {
                Day(
                    1013,
                    Row("ba:street_fourthavenue", 28, 1099f, 1f),
                    Row("ba:street_side", 9, 4f, -30f)),
            },
            0);

        Assert.NotNull(steps);
        Assert.Empty(steps);
    }

    [Fact]
    public void Roadmap_a_repeated_score_line_stays_one_close()
    {
        var steps = Roadmap.Order(
            Api(),
            new[]
            {
                new CloseScoreLine("ba:street_early", 1, ""),
                new CloseScoreLine("ba:street_early", 1, ""),
                new CloseScoreLine("ba:street_late", 2, ""),
            },
            Quiet(),
            new RentPayback("", 0, 0d, false),
            new[]
            {
                Day(
                    1013,
                    Row("ba:street_early", 1, 0f, -10f),
                    Row("ba:street_late", 2, 0f, -10f)),
            },
            3);

        Assert.Equal(2, steps.Count);
        Assert.Equal("close", steps[0].Kind);
        Assert.Equal("ba:street_late", steps[0].Street);
        Assert.Equal(2, steps[0].Number);
        Assert.Equal(10d, steps[0].DailyGain);
        Assert.Equal("ba:street_early", steps[1].Street);
        Assert.Equal(1, steps[1].Number);
    }

    [Fact]
    public void Roadmap_a_blank_api_keeps_no_steps()
    {
        var steps = Roadmap.Order(
            new ApiNote("", "", "", "", ""),
            Lines("ba:street_side", 9),
            Quiet(),
            new RentPayback("ba:street_fourthavenue", 28, 1d, true),
            new[]
            {
                Day(1013, Row("ba:street_side", 9, 4f, -30f)),
            },
            3);

        Assert.Empty(steps);
    }

    [Fact]
    public void Roadmap_a_missing_review_keeps_no_steps()
    {
        var steps = Roadmap.Order(
            Api(),
            Lines("ba:street_side", 9),
            null,
            new RentPayback("ba:street_fourthavenue", 28, 1d, true),
            new[]
            {
                Day(1013, Row("ba:street_fourthavenue", 28, 1099f, 1f)),
            },
            3);

        Assert.Empty(steps);
    }

    private static CloseScoreLine[] Lines(params object[] streetThenNumber)
    {
        var lines = new CloseScoreLine[streetThenNumber.Length / 2];
        for (var i = 0; i < lines.Length; i++)
        {
            lines[i] = new CloseScoreLine((string)streetThenNumber[i * 2], (int)streetThenNumber[(i * 2) + 1], "");
        }

        return lines;
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

    private static StatementRow Row(string street, int number, float rent, float profit)
    {
        return new StatementRow(street, number, 0f, 0f, 0f, rent, 0f, 0f, profit);
    }
}

using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class CloseScoreTests
{
    [Fact]
    public void CloseScore_one_explain_line_names_after_rent_streak_and_trend()
    {
        var scored = CloseScore.Score(
            Api(),
            new[]
            {
                Day(1011, Row("ba:street_main", 2, -10f, 5f)),
                Day(1012, Row("ba:street_main", 2, -20f, 5f)),
                Day(1013, Row("ba:street_main", 2, -40f, 5f)),
            });

        Assert.Single(scored);
        Assert.Equal("ba:street_main", scored[0].Street);
        Assert.Equal(2, scored[0].Number);
        Assert.Equal(
            "Close ba:street_main 2. After rent -40. Streak 3. Trend -30.",
            scored[0].Explain);
    }

    [Fact]
    public void CloseScore_every_store_gets_one_line()
    {
        var scored = CloseScore.Score(
            Api(),
            new[]
            {
                Day(
                    1011,
                    Row("ba:street_main", 2, -10f, 1f),
                    Row("ba:street_side", 9, -2f, 1f)),
                Day(
                    1012,
                    Row("ba:street_main", 2, -10f, 1f),
                    Row("ba:street_side", 9, -2f, 1f)),
                Day(
                    1013,
                    Row("ba:street_main", 2, -10f, 1f),
                    Row("ba:street_side", 9, -2f, 1f)),
            });

        Assert.Equal(2, scored.Count);
        Assert.Equal("Close ba:street_main 2. After rent -10. Streak 3. Trend 0.", scored[0].Explain);
        Assert.Equal("Close ba:street_side 9. After rent -2. Streak 3. Trend 0.", scored[1].Explain);
    }

    [Fact]
    public void CloseScore_the_layout_clone_does_not_choose_the_store()
    {
        var layout = new LayoutClone("ba:street_main", "food", "soda-line", 1, 4);
        var closed = Close.Lock(
            layout,
            new[]
            {
                new LedgerDay("ba:street_main", 2, "food", 1011, -1f),
                new LedgerDay("ba:street_main", 2, "food", 1012, -1f),
                new LedgerDay("ba:street_main", 2, "food", 1013, -1f),
            },
            3,
            true,
            true);
        var scored = CloseScore.Score(
            Api(),
            new[]
            {
                Day(1011, Row("ba:street_main", 2, -1f, 3f), Row("ba:street_side", 9, -10f, 4f)),
                Day(1012, Row("ba:street_main", 2, -1f, 3f), Row("ba:street_side", 9, -20f, 4f)),
                Day(1013, Row("ba:street_main", 2, -1f, 3f), Row("ba:street_side", 9, -30f, 4f)),
            });

        Assert.True(closed.Closed);
        Assert.Equal("ba:street_main", closed.Street);
        Assert.Equal(2, closed.Number);
        Assert.Equal(2, scored.Count);
        Assert.Equal("Close ba:street_side 9. After rent -30. Streak 3. Trend -20.", scored[1].Explain);
        Assert.NotEqual(closed.Number, scored[1].Number);
    }

    [Fact]
    public void CloseScore_a_green_day_breaks_the_streak()
    {
        var scored = CloseScore.Score(
            Api(),
            new[]
            {
                Day(1011, Row("ba:street_main", 2, -10f, 1f)),
                Day(1012, Row("ba:street_main", 2, -20f, 1f)),
                Day(1013, Row("ba:street_main", 2, 5f, 1f)),
                Day(1014, Row("ba:street_main", 2, -2f, 1f)),
                Day(1015, Row("ba:street_main", 2, -4f, 1f)),
            });

        Assert.Equal(
            "Close ba:street_main 2. After rent -4. Streak 2. Trend 6.",
            scored[0].Explain);
    }

    [Fact]
    public void CloseScore_a_green_day_at_the_end_clears_the_streak()
    {
        var scored = CloseScore.Score(
            Api(),
            new[]
            {
                Day(1011, Row("ba:street_main", 2, -1f, 1f)),
                Day(1012, Row("ba:street_main", 2, -1f, 1f)),
                Day(1013, Row("ba:street_main", 2, -1f, 1f)),
                Day(1014, Row("ba:street_main", 2, 4f, 1f)),
            });

        Assert.Equal(
            "Close ba:street_main 2. After rent 4. Streak 0. Trend 5.",
            scored[0].Explain);
    }

    [Fact]
    public void CloseScore_profit_zero_is_not_a_red_day()
    {
        var scored = CloseScore.Score(
            Api(),
            new[]
            {
                Day(1011, Row("ba:street_main", 2, -1f, 1f)),
                Day(1012, Row("ba:street_main", 2, 0f, 1f)),
                Day(1013, Row("ba:street_main", 2, -1f, 1f)),
            });

        Assert.Equal(
            "Close ba:street_main 2. After rent -1. Streak 1. Trend 0.",
            scored[0].Explain);
    }

    [Fact]
    public void CloseScore_profit_after_rent_keeps_the_profit_column()
    {
        var scored = CloseScore.Score(
            Api(),
            new[]
            {
                Day(1013, new StatementRow("ba:street_main", 2, 100f, 0f, 0f, 40f, 0f, 0f, -15f)),
            });

        Assert.Equal(
            "Close ba:street_main 2. After rent -15. Streak 1. Trend 0.",
            scored[0].Explain);
        Assert.DoesNotContain("After rent 100", scored[0].Explain);
        Assert.DoesNotContain("After rent -55", scored[0].Explain);
        Assert.DoesNotContain("After rent 25", scored[0].Explain);
        Assert.DoesNotContain("After rent 85", scored[0].Explain);
    }

    [Fact]
    public void CloseScore_a_later_same_day_row_keeps_that_profit()
    {
        var scored = CloseScore.Score(
            Api(),
            new[]
            {
                Day(
                    1013,
                    Row("ba:street_main", 2, -10f, 1f),
                    Row("ba:street_main", 2, -40f, 1f)),
            });

        Assert.Single(scored);
        Assert.Equal(
            "Close ba:street_main 2. After rent -40. Streak 1. Trend 0.",
            scored[0].Explain);
    }

    [Fact]
    public void CloseScore_a_later_day_record_keeps_that_profit()
    {
        var scored = CloseScore.Score(
            Api(),
            new[]
            {
                Day(1011, Row("ba:street_main", 2, -10f, 1f)),
                Day(1011, Row("ba:street_main", 2, -40f, 1f)),
            });

        Assert.Single(scored);
        Assert.Equal(
            "Close ba:street_main 2. After rent -40. Streak 1. Trend 0.",
            scored[0].Explain);
    }

    [Fact]
    public void CloseScore_days_out_of_order_still_follow_the_day()
    {
        var scored = CloseScore.Score(
            Api(),
            new[]
            {
                Day(1013, Row("ba:street_main", 2, -40f, 1f)),
                Day(1011, Row("ba:street_main", 2, -10f, 1f)),
                Day(1012, Row("ba:street_main", 2, -20f, 1f)),
            });

        Assert.Equal(
            "Close ba:street_main 2. After rent -40. Streak 3. Trend -30.",
            scored[0].Explain);
    }

    [Fact]
    public void CloseScore_another_store_does_not_break_the_streak()
    {
        var scored = CloseScore.Score(
            Api(),
            new[]
            {
                Day(1011, Row("ba:street_main", 2, -1f, 1f)),
                Day(1012, Row("ba:street_main", 2, -1f, 1f), Row("ba:street_side", 9, 50f, 1f)),
                Day(1013, Row("ba:street_main", 2, -1f, 1f)),
            });

        Assert.Equal(
            "Close ba:street_main 2. After rent -1. Streak 3. Trend 0.",
            scored[0].Explain);
        Assert.Equal(
            "Close ba:street_side 9. After rent 50. Streak 0. Trend 0.",
            scored[1].Explain);
    }

    [Fact]
    public void CloseScore_an_improving_run_keeps_a_positive_trend()
    {
        var scored = CloseScore.Score(
            Api(),
            new[]
            {
                Day(1011, Row("ba:street_main", 2, -40f, 1f)),
                Day(1012, Row("ba:street_main", 2, -10f, 1f)),
            });

        Assert.Equal(
            "Close ba:street_main 2. After rent -10. Streak 2. Trend 30.",
            scored[0].Explain);
    }

    [Fact]
    public void CloseScore_a_green_decline_keeps_streak_zero()
    {
        var scored = CloseScore.Score(
            Api(),
            new[]
            {
                Day(1011, Row("ba:street_main", 2, 10f, 1f)),
                Day(1012, Row("ba:street_main", 2, 4f, 1f)),
            });

        Assert.Equal(
            "Close ba:street_main 2. After rent 4. Streak 0. Trend -6.",
            scored[0].Explain);
    }

    [Fact]
    public void CloseScore_a_missing_day_does_not_break_the_streak()
    {
        var scored = CloseScore.Score(
            Api(),
            new[]
            {
                Day(1011, Row("ba:street_main", 2, -5f, 1f)),
                Day(1013, Row("ba:street_main", 2, -8f, 1f)),
            });

        Assert.Equal(
            "Close ba:street_main 2. After rent -8. Streak 2. Trend -3.",
            scored[0].Explain);
    }

    [Fact]
    public void CloseScore_a_profitable_store_still_gets_a_line()
    {
        var scored = CloseScore.Score(
            Api(),
            new[]
            {
                Day(1013, Row("ba:street_main", 2, 12f, 3f)),
            });

        Assert.Equal(
            "Close ba:street_main 2. After rent 12. Streak 0. Trend 0.",
            scored[0].Explain);
    }

    [Fact]
    public void CloseScore_the_same_number_on_another_street_is_another_store()
    {
        var scored = CloseScore.Score(
            Api(),
            new[]
            {
                Day(1013, Row("ba:street_main", 2, -5f, 1f), Row("ba:street_side", 2, -8f, 1f)),
            });

        Assert.Equal(2, scored.Count);
        Assert.Equal("ba:street_main", scored[0].Street);
        Assert.Equal("ba:street_side", scored[1].Street);
        Assert.Equal(2, scored[0].Number);
        Assert.Equal(2, scored[1].Number);
    }

    [Fact]
    public void CloseScore_an_empty_ledger_has_no_line()
    {
        Assert.Empty(CloseScore.Score(Api(), null));
        Assert.Empty(CloseScore.Score(Api(), Array.Empty<FinancialDay>()));
        Assert.Empty(CloseScore.Score(Api(), new[] { new FinancialDay(1013, new StatementRow[] { null! }) }));
        Assert.Empty(CloseScore.Score(Api(), new FinancialDay[] { null! }));
    }

    [Fact]
    public void CloseScore_a_blank_street_is_not_a_store()
    {
        var scored = CloseScore.Score(
            Api(),
            new[]
            {
                Day(1013, Row("", 2, -10f, 1f), Row("ba:street_main", 2, -4f, 1f)),
            });

        Assert.Single(scored);
        Assert.Equal("ba:street_main", scored[0].Street);
        Assert.Equal(
            "Close ba:street_main 2. After rent -4. Streak 1. Trend 0.",
            scored[0].Explain);
    }

    [Fact]
    public void CloseScore_a_blank_api_keeps_no_line()
    {
        var days = new[] { Day(1013, Row("ba:street_main", 2, -10f, 1f)) };

        Assert.Empty(CloseScore.Score(null, days));
        Assert.Empty(CloseScore.Score(new ApiNote("", "", "", "", ""), days));
    }

    [Fact]
    public void CloseScore_a_recorded_build_scores_without_calling_close()
    {
        var scored = CloseScore.Score(
            new ApiNote("3682", "", "", "", ""),
            new[] { Day(1013, Row("ba:street_main", 2, -7f, 1f)) });

        Assert.Equal(
            "Close ba:street_main 2. After rent -7. Streak 1. Trend 0.",
            scored[0].Explain);
        Assert.DoesNotContain("ShutdownBusiness", scored[0].Explain);
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

    private static StatementRow Row(string street, int number, float profit, float rent)
    {
        return new StatementRow(street, number, 0f, 0f, 0f, rent, 0f, 0f, profit);
    }
}

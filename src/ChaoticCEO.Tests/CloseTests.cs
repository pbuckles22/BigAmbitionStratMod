using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class CloseTests
{
    [Fact]
    public void Close_lock_after_red_days()
    {
        var closed = Close.Lock(
            FoodLayout(),
            new[]
            {
                Day(2, 1011, -10f),
                Day(2, 1012, -4f),
                Day(2, 1013, -1f),
            },
            3,
            true,
            true);

        Assert.Equal("ba:street_main", closed.Street);
        Assert.Equal(2, closed.Number);
        Assert.Equal("food", closed.Family);
        Assert.True(closed.Closed);
    }

    [Fact]
    public void Close_fewer_than_n_red_days_does_not_close()
    {
        var closed = Close.Lock(
            FoodLayout(),
            new[]
            {
                Day(2, 1011, -10f),
                Day(2, 1012, -4f),
            },
            3,
            true,
            true);

        Assert.False(closed.Closed);
        Assert.Equal("", closed.Street);
        Assert.Equal(0, closed.Number);
        Assert.Equal("", closed.Family);
    }

    [Fact]
    public void Close_extra_red_days_still_keep_one_close()
    {
        var closed = Close.Lock(
            FoodLayout(),
            new[]
            {
                Day(2, 1011, -1f),
                Day(2, 1012, -1f),
                Day(2, 1013, -1f),
                Day(2, 1014, -1f),
                Day(2, 1015, -1f),
            },
            3,
            true,
            true);

        Assert.True(closed.Closed);
        Assert.Equal(2, closed.Number);
        Assert.Equal("food", closed.Family);
    }

    [Fact]
    public void Close_the_confirm_dialog_is_required()
    {
        var closed = Close.Lock(
            FoodLayout(),
            RedRun(2),
            3,
            false,
            true);

        Assert.False(closed.Closed);
        Assert.Equal(0, closed.Number);
    }

    [Fact]
    public void Close_the_toggle_is_required()
    {
        var closed = Close.Lock(
            FoodLayout(),
            RedRun(2),
            3,
            true,
            false);

        Assert.False(closed.Closed);
        Assert.Equal(0, closed.Number);
    }

    [Fact]
    public void Close_the_longer_streak_is_kept()
    {
        var closed = Close.Lock(
            FoodLayout(),
            new[]
            {
                Day(5, 1010, -1f),
                Day(5, 1011, -1f),
                Day(5, 1012, -1f),
                Day(5, 1013, -1f),
                Day(2, 1014, -1f),
                Day(2, 1015, -1f),
                Day(2, 1016, -1f),
            },
            3,
            true,
            true);

        Assert.True(closed.Closed);
        Assert.Equal(5, closed.Number);
        Assert.Equal("ba:street_main", closed.Street);
    }

    [Fact]
    public void Close_a_tie_keeps_the_later_store()
    {
        var closed = Close.Lock(
            FoodLayout(),
            new[]
            {
                Day(2, 1011, -1f),
                Day(2, 1012, -1f),
                Day(2, 1013, -1f),
                Day(5, 1014, -1f),
                Day(5, 1015, -1f),
                Day(5, 1016, -1f),
            },
            3,
            true,
            true);

        Assert.Equal(5, closed.Number);
        Assert.True(closed.Closed);
    }

    [Fact]
    public void Close_a_deeper_loss_does_not_beat_more_red_days()
    {
        var closed = Close.Lock(
            FoodLayout(),
            new[]
            {
                Day(2, 1011, -1000f),
                Day(5, 1012, -1f),
                Day(5, 1013, -1f),
                Day(5, 1014, -1f),
            },
            3,
            true,
            true);

        Assert.Equal(5, closed.Number);
        Assert.True(closed.Closed);
    }

    [Fact]
    public void Close_profit_zero_is_not_a_red_day()
    {
        var closed = Close.Lock(
            FoodLayout(),
            new[]
            {
                Day(2, 1011, -1f),
                Day(2, 1012, 0f),
                Day(2, 1013, -1f),
                Day(2, 1014, -1f),
            },
            3,
            true,
            true);

        Assert.False(closed.Closed);
        Assert.Equal(0, closed.Number);
    }

    [Fact]
    public void Close_a_green_day_breaks_the_streak()
    {
        var closed = Close.Lock(
            FoodLayout(),
            new[]
            {
                Day(2, 1011, -1f),
                Day(2, 1012, 8f),
                Day(2, 1013, -1f),
                Day(2, 1014, -1f),
            },
            3,
            true,
            true);

        Assert.False(closed.Closed);
        Assert.Equal(0, closed.Number);
    }

    [Fact]
    public void Close_a_recovery_does_not_close()
    {
        var closed = Close.Lock(
            FoodLayout(),
            new[]
            {
                Day(2, 1011, -1f),
                Day(2, 1012, -1f),
                Day(2, 1013, -1f),
                Day(2, 1014, 1f),
            },
            3,
            true,
            true);

        Assert.False(closed.Closed);
        Assert.Equal(0, closed.Number);
    }

    [Fact]
    public void Close_a_new_streak_after_a_green_day_can_close()
    {
        var closed = Close.Lock(
            FoodLayout(),
            new[]
            {
                Day(2, 1010, -1f),
                Day(2, 1011, -1f),
                Day(2, 1012, -1f),
                Day(2, 1013, 4f),
                Day(2, 1014, -1f),
                Day(2, 1015, -1f),
                Day(2, 1016, -1f),
            },
            3,
            true,
            true);

        Assert.True(closed.Closed);
        Assert.Equal(2, closed.Number);
    }

    [Fact]
    public void Close_another_store_does_not_reset_the_streak()
    {
        var closed = Close.Lock(
            FoodLayout(),
            new[]
            {
                Day(2, 1011, -1f),
                Day(9, 1012, 50f),
                Day(2, 1013, -1f),
                Day(2, 1014, -1f),
            },
            3,
            true,
            true);

        Assert.True(closed.Closed);
        Assert.Equal(2, closed.Number);
    }

    [Fact]
    public void Close_an_empty_layout_has_no_close()
    {
        var closed = Close.Lock(
            new LayoutClone("", "", "", 0, 0),
            RedRun(2),
            3,
            true,
            true);

        Assert.False(closed.Closed);
        Assert.Equal("", closed.Street);
        Assert.Equal(0, closed.Number);
    }

    [Fact]
    public void Close_a_blank_blueprint_is_not_a_layout()
    {
        var closed = Close.Lock(
            new LayoutClone("ba:street_main", "food", "", 0, 0),
            RedRun(2),
            3,
            true,
            true);

        Assert.False(closed.Closed);
        Assert.Equal(0, closed.Number);
    }

    [Fact]
    public void Close_another_street_does_not_close()
    {
        var closed = Close.Lock(
            FoodLayout(),
            new[]
            {
                new LedgerDay("ba:street_side", 2, "food", 1011, -1f),
                new LedgerDay("ba:street_side", 2, "food", 1012, -1f),
                new LedgerDay("ba:street_side", 2, "food", 1013, -1f),
            },
            3,
            true,
            true);

        Assert.False(closed.Closed);
        Assert.Equal(0, closed.Number);
    }

    [Fact]
    public void Close_a_broken_longer_streak_keeps_the_current_one()
    {
        var closed = Close.Lock(
            FoodLayout(),
            new[]
            {
                Day(5, 1010, -1f),
                Day(5, 1011, -1f),
                Day(5, 1012, -1f),
                Day(5, 1013, -1f),
                Day(5, 1014, 2f),
                Day(2, 1015, -1f),
                Day(2, 1016, -1f),
                Day(2, 1017, -1f),
            },
            3,
            true,
            true);

        Assert.True(closed.Closed);
        Assert.Equal(2, closed.Number);
        Assert.Equal("food", closed.Family);
    }

    [Fact]
    public void Close_n_of_zero_is_not_a_close()
    {
        var closed = Close.Lock(
            FoodLayout(),
            RedRun(2),
            0,
            true,
            true);

        Assert.False(closed.Closed);
        Assert.Equal(0, closed.Number);
        Assert.Equal("", closed.Street);
    }

    [Fact]
    public void Close_another_family_does_not_close()
    {
        var closed = Close.Lock(
            FoodLayout(),
            new[]
            {
                new LedgerDay("ba:street_main", 2, "specialty", 1011, -1f),
                new LedgerDay("ba:street_main", 2, "specialty", 1012, -1f),
                new LedgerDay("ba:street_main", 2, "specialty", 1013, -1f),
            },
            3,
            true,
            true);

        Assert.False(closed.Closed);
        Assert.Equal(0, closed.Number);
    }

    private static LayoutClone FoodLayout()
    {
        return new LayoutClone("ba:street_main", "food", "soda-line", 1, 4);
    }

    private static LedgerDay Day(int number, int day, float profit)
    {
        return new LedgerDay("ba:street_main", number, "food", day, profit);
    }

    private static LedgerDay[] RedRun(int number)
    {
        return new[]
        {
            Day(number, 1011, -10f),
            Day(number, 1012, -4f),
            Day(number, 1013, -1f),
        };
    }
}

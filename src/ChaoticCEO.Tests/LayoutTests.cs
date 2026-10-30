using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class LayoutTests
{
    [Fact]
    public void Layout_lock_one_clone()
    {
        var clone = Layout.Lock(
            FoodStaff(),
            new[] { FoodCluster() },
            new[]
            {
                Source(1, "soda-line", 12),
                NewStore(4),
            });

        Assert.Equal("ba:street_main", clone.Street);
        Assert.Equal("food", clone.Family);
        Assert.Equal("soda-line", clone.Blueprint);
        Assert.Equal(1, clone.Source);
        Assert.Equal(4, clone.Destination);
    }

    [Fact]
    public void Layout_the_larger_score_is_kept()
    {
        var clone = Layout.Lock(
            FoodStaff(),
            new[] { FoodCluster() },
            new[]
            {
                Source(1, "soda-line", 12),
                Source(2, "burger-line", 40),
                NewStore(4),
            });

        Assert.Equal("burger-line", clone.Blueprint);
        Assert.Equal(2, clone.Source);
        Assert.Equal(4, clone.Destination);
    }

    [Fact]
    public void Layout_a_tie_keeps_the_later_layout()
    {
        var clone = Layout.Lock(
            FoodStaff(),
            new[] { FoodCluster() },
            new[]
            {
                Source(1, "soda-line", 12),
                Source(2, "burger-line", 12),
                NewStore(4),
            });

        Assert.Equal("burger-line", clone.Blueprint);
        Assert.Equal(2, clone.Source);
        Assert.Equal(4, clone.Destination);
    }

    [Fact]
    public void Layout_score_zero_is_not_a_layout()
    {
        var clone = Layout.Lock(
            FoodStaff(),
            new[] { FoodCluster() },
            new[]
            {
                Source(1, "empty-line", 0),
                Source(2, "soda-line", 12),
                NewStore(4),
            });

        Assert.Equal("soda-line", clone.Blueprint);
        Assert.Equal(2, clone.Source);
    }

    [Fact]
    public void Layout_a_short_cluster_does_not_qualify()
    {
        var clone = Layout.Lock(
            FoodStaff(),
            new[] { new StreetCluster("ba:street_main", "food", 2) },
            new[]
            {
                Source(1, "soda-line", 12),
                NewStore(4),
            });

        Assert.Equal("", clone.Blueprint);
        Assert.Equal(0, clone.Source);
        Assert.Equal(0, clone.Destination);
    }

    [Fact]
    public void Layout_another_family_does_not_clone()
    {
        var clone = Layout.Lock(
            FoodStaff(),
            new[]
            {
                FoodCluster(),
                new StreetCluster("ba:street_main", "specialty", 4),
            },
            new[]
            {
                new ChainLayout("ba:street_main", "specialty", 8, "gift-line", 100, false),
                new ChainLayout("ba:street_main", "specialty", 9, "", 0, true),
                Source(1, "soda-line", 12),
                NewStore(4),
            });

        Assert.Equal("food", clone.Family);
        Assert.Equal("soda-line", clone.Blueprint);
        Assert.Equal(1, clone.Source);
        Assert.Equal(4, clone.Destination);
    }

    [Fact]
    public void Layout_a_store_without_a_blueprint_is_not_a_source()
    {
        var clone = Layout.Lock(
            FoodStaff(),
            new[] { FoodCluster() },
            new[]
            {
                Source(1, "", 100),
                Source(2, "soda-line", 12),
                NewStore(4),
            });

        Assert.Equal("soda-line", clone.Blueprint);
        Assert.Equal(2, clone.Source);
    }

    [Fact]
    public void Layout_a_later_zero_does_not_replace_the_layout()
    {
        var clone = Layout.Lock(
            FoodStaff(),
            new[] { FoodCluster() },
            new[]
            {
                Source(1, "soda-line", 12),
                Source(2, "empty-line", 0),
                NewStore(4),
            });

        Assert.Equal("soda-line", clone.Blueprint);
        Assert.Equal(1, clone.Source);
        Assert.Equal(4, clone.Destination);
    }

    [Fact]
    public void Layout_no_new_store_keeps_no_clone()
    {
        var clone = Layout.Lock(
            FoodStaff(),
            new[] { FoodCluster() },
            new[]
            {
                Source(1, "soda-line", 12),
            });

        Assert.Equal("", clone.Blueprint);
        Assert.Equal(0, clone.Destination);
    }

    [Fact]
    public void Layout_an_empty_plan_still_clones()
    {
        var clone = Layout.Lock(
            new StaffPlan("food", "", "", 0),
            new[] { FoodCluster() },
            new[]
            {
                Source(1, "soda-line", 12),
                NewStore(4),
            });

        Assert.Equal("food", clone.Family);
        Assert.Equal("soda-line", clone.Blueprint);
        Assert.Equal(1, clone.Source);
        Assert.Equal(4, clone.Destination);
    }

    [Fact]
    public void Layout_an_empty_role_has_no_clone()
    {
        var clone = Layout.Lock(
            new StaffPlan("", "", "", 0),
            new[] { FoodCluster() },
            new[]
            {
                Source(1, "soda-line", 12),
                NewStore(4),
            });

        Assert.Equal("", clone.Street);
        Assert.Equal("", clone.Family);
        Assert.Equal("", clone.Blueprint);
        Assert.Equal(0, clone.Source);
        Assert.Equal(0, clone.Destination);
    }

    [Fact]
    public void Layout_two_new_stores_keep_one_destination()
    {
        var clone = Layout.Lock(
            FoodStaff(),
            new[] { FoodCluster() },
            new[]
            {
                Source(1, "soda-line", 12),
                NewStore(4),
                NewStore(7),
            });

        Assert.Equal("soda-line", clone.Blueprint);
        Assert.Equal(1, clone.Source);
        Assert.Equal(7, clone.Destination);
    }

    [Fact]
    public void Layout_a_new_store_is_not_the_source()
    {
        var clone = Layout.Lock(
            FoodStaff(),
            new[] { FoodCluster() },
            new[]
            {
                Source(1, "soda-line", 12),
                new ChainLayout("ba:street_main", "food", 4, "decoy", 1000, true),
            });

        Assert.Equal("soda-line", clone.Blueprint);
        Assert.Equal(1, clone.Source);
        Assert.Equal(4, clone.Destination);
    }

    [Fact]
    public void Layout_another_street_does_not_clone()
    {
        var clone = Layout.Lock(
            FoodStaff(),
            new[] { FoodCluster() },
            new[]
            {
                new ChainLayout("ba:street_side", "food", 8, "side-line", 100, false),
                Source(1, "soda-line", 12),
                NewStore(4),
            });

        Assert.Equal("ba:street_main", clone.Street);
        Assert.Equal("soda-line", clone.Blueprint);
        Assert.Equal(1, clone.Source);
        Assert.Equal(4, clone.Destination);
    }

    [Fact]
    public void Layout_a_later_cluster_wins()
    {
        var clone = Layout.Lock(
            FoodStaff(),
            new[]
            {
                FoodCluster(),
                new StreetCluster("ba:street_side", "food", 3),
            },
            new[]
            {
                Source(1, "soda-line", 100),
                NewStore(4),
                new ChainLayout("ba:street_side", "food", 8, "side-line", 1, false),
                new ChainLayout("ba:street_side", "food", 9, "", 0, true),
            });

        Assert.Equal("ba:street_side", clone.Street);
        Assert.Equal("side-line", clone.Blueprint);
        Assert.Equal(8, clone.Source);
        Assert.Equal(9, clone.Destination);
    }

    [Fact]
    public void Layout_a_negative_score_is_not_a_layout()
    {
        var clone = Layout.Lock(
            FoodStaff(),
            new[] { FoodCluster() },
            new[]
            {
                Source(1, "soda-line", 12),
                Source(2, "open-line", -1),
                NewStore(4),
            });

        Assert.Equal("soda-line", clone.Blueprint);
        Assert.Equal(1, clone.Source);
        Assert.Equal(4, clone.Destination);
    }

    [Fact]
    public void Layout_a_later_short_cluster_keeps_the_clone()
    {
        var clone = Layout.Lock(
            FoodStaff(),
            new[]
            {
                FoodCluster(),
                new StreetCluster("ba:street_side", "food", 2),
            },
            new[]
            {
                Source(1, "soda-line", 12),
                NewStore(4),
                new ChainLayout("ba:street_side", "food", 8, "side-line", 100, false),
                new ChainLayout("ba:street_side", "food", 9, "", 0, true),
            });

        Assert.Equal("ba:street_main", clone.Street);
        Assert.Equal("soda-line", clone.Blueprint);
        Assert.Equal(1, clone.Source);
        Assert.Equal(4, clone.Destination);
    }

    [Fact]
    public void Layout_a_later_cluster_without_a_new_store_keeps_the_earlier_clone()
    {
        var clone = Layout.Lock(
            FoodStaff(),
            new[]
            {
                FoodCluster(),
                new StreetCluster("ba:street_side", "food", 3),
            },
            new[]
            {
                Source(1, "soda-line", 12),
                NewStore(4),
                new ChainLayout("ba:street_side", "food", 8, "side-line", 100, false),
            });

        Assert.Equal("ba:street_main", clone.Street);
        Assert.Equal("soda-line", clone.Blueprint);
        Assert.Equal(1, clone.Source);
        Assert.Equal(4, clone.Destination);
    }

    [Fact]
    public void Layout_a_blank_street_does_not_qualify()
    {
        var clone = Layout.Lock(
            FoodStaff(),
            new[] { new StreetCluster("", "food", 3) },
            new[]
            {
                new ChainLayout("", "food", 1, "soda-line", 12, false),
                new ChainLayout("", "food", 4, "", 0, true),
            });

        Assert.Equal("", clone.Street);
        Assert.Equal("", clone.Blueprint);
        Assert.Equal(0, clone.Destination);
    }

    [Fact]
    public void Layout_a_new_store_on_another_street_is_not_the_destination()
    {
        var clone = Layout.Lock(
            FoodStaff(),
            new[] { FoodCluster() },
            new[]
            {
                Source(1, "soda-line", 12),
                new ChainLayout("ba:street_side", "food", 9, "", 0, true),
            });

        Assert.Equal("", clone.Blueprint);
        Assert.Equal(0, clone.Destination);
    }

    private static StaffPlan FoodStaff()
    {
        return new StaffPlan("food", "hr", "hr-soda", 40);
    }

    private static StreetCluster FoodCluster()
    {
        return new StreetCluster("ba:street_main", "food", 3);
    }

    private static ChainLayout Source(int number, string blueprint, int score)
    {
        return new ChainLayout("ba:street_main", "food", number, blueprint, score, false);
    }

    private static ChainLayout NewStore(int number)
    {
        return new ChainLayout("ba:street_main", "food", number, "", 0, true);
    }
}

using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class RentTests
{
    [Fact]
    public void Rent_names_the_cheaper_lot_and_the_call()
    {
        var api = GameApi.Live();
        var chosen = Rent.Choose(
            new[]
            {
                new RentLot("ba:street_high", 2, 80m),
                new RentLot("ba:street_low", 9, 40m),
            },
            cash: 100m,
            anyRented: false,
            api);

        Assert.True(chosen.Rented);
        Assert.Equal("ba:street_low", chosen.Street);
        Assert.Equal(9, chosen.Number);
        Assert.Equal("Buildings.BuildingCommands.Command_RentBuilding", chosen.Call);
        Assert.Equal(chosen.Call, api.Call("rent"));
    }

    [Fact]
    public void Rent_a_tie_keeps_the_later_lot_and_skips_what_cannot_be_rented()
    {
        var api = GameApi.Live();
        var tie = Rent.Choose(
            new[]
            {
                new RentLot("ba:street_early", 1, 40m),
                new RentLot("ba:street_late", 8, 40m),
            },
            cash: 100m,
            anyRented: false,
            api);
        var overCash = Rent.Choose(
            new[] { new RentLot("ba:street_high", 2, 80m) },
            cash: 30m,
            anyRented: false,
            api);
        var blank = Rent.Choose(
            new[]
            {
                new RentLot("", 3, 1m),
                new RentLot("ba:street_zero", 0, 1m),
                new RentLot("ba:street_kept", 4, 20m),
            },
            cash: 100m,
            anyRented: false,
            api);
        var taken = Rent.Choose(
            new[] { new RentLot("ba:street_low", 9, 40m) },
            cash: 100m,
            anyRented: true,
            api);
        var unavailable = Rent.Choose(
            new[]
            {
                new RentLot("ba:street_cheap", 1, 5m, available: false),
                new RentLot("ba:street_open", 2, 50m),
            },
            cash: 100m,
            anyRented: false,
            api);

        Assert.Equal("ba:street_late", tie.Street);
        Assert.Equal(8, tie.Number);
        Assert.Equal(api.Call("rent"), tie.Call);
        Assert.False(overCash.Rented);
        Assert.Equal("", overCash.Call);
        Assert.Equal("ba:street_kept", blank.Street);
        Assert.False(taken.Rented);
        Assert.Equal("ba:street_open", unavailable.Street);
        Assert.Equal(2, unavailable.Number);
    }
}

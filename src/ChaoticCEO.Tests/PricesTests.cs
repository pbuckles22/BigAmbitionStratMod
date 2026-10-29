using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class PricesTests
{
    [Fact]
    public void Prices_lock_one_sold_price()
    {
        var price = Prices.Lock(
            "ba:itemname_sodacan",
            new[]
            {
                new ItemSoldPerPriceEntry("ba:itemname_sodacan", 2.75f, 8),
            });

        Assert.Equal("ba:itemname_sodacan", price.Item);
        Assert.Equal(2.75f, price.Price);
    }

    [Fact]
    public void Prices_the_larger_amount_is_the_sold_price()
    {
        var price = Prices.Lock(
            "ba:itemname_sodacan",
            new[]
            {
                new ItemSoldPerPriceEntry("ba:itemname_sodacan", 1.5f, 3),
                new ItemSoldPerPriceEntry("ba:itemname_sodacan", 2.75f, 8),
                new ItemSoldPerPriceEntry("ba:itemname_sodacan", 9f, 0),
                new ItemSoldPerPriceEntry("ba:itemname_sodacan", 1.25f, 4),
            });

        Assert.Equal("ba:itemname_sodacan", price.Item);
        Assert.Equal(2.75f, price.Price);
    }

    [Fact]
    public void Prices_a_tie_keeps_the_later_price()
    {
        var price = Prices.Lock(
            "ba:itemname_sodacan",
            new[]
            {
                new ItemSoldPerPriceEntry("ba:itemname_sodacan", 1.5f, 4),
                new ItemSoldPerPriceEntry("ba:itemname_sodacan", 4.25f, 4),
            });

        Assert.Equal("ba:itemname_sodacan", price.Item);
        Assert.Equal(4.25f, price.Price);
    }
}

using System;
using System.Collections.Generic;
using System.IO;

namespace ChaoticCEO.Tests
{
    public class DisplayStockTests
    {
        [Fact]
        public void Day4_empty_displays_order_above_zero()
        {
            var lines = new List<DisplayLine>();
            foreach (var item in Wholesaler.GiftShopWholesalersStock())
            {
                lines.Add(new DisplayLine(item, 0));
            }

            var morning = DisplayStock.Morning(4, lines);

            Assert.True(morning.Ordered > 0);
            Assert.Equal(Restock.Summary(4, morning.Ordered), morning.Line);
            foreach (var item in Wholesaler.GiftShopWholesalersStock())
            {
                Assert.True(morning.OnDisplay[item] > 0);
            }
        }

        [Fact]
        public void Stocked_display_is_kept_and_a_storage_shelf_is_not_ordered()
        {
            var morning = DisplayStock.Morning(
                4,
                new[]
                {
                    new DisplayLine("ba:itemname_cheapgift", 12),
                    new DisplayLine("ba:itemname_cheapflower", 0),
                    new DisplayLine(StorageShelf.Item, 0),
                });

            Assert.Equal(1, morning.Ordered);
            Assert.Equal(12, morning.OnDisplay["ba:itemname_cheapgift"]);
            Assert.True(morning.OnDisplay["ba:itemname_cheapflower"] > 0);
            Assert.False(morning.OnDisplay.ContainsKey(StorageShelf.Item));
        }

        [Fact]
        public void Chain_new_game_day_roll_display_stock_sets_g2()
        {
            // Chain: new game → day roll → display stock → g2
            var dir = Directory.CreateDirectory(
                Path.Combine(Path.GetTempPath(), "display-stock-" + Guid.NewGuid().ToString("N")));
            try
            {
                var shop = DisplayStock.Open();
                var days = new DayWatch();
                Assert.Null(days.Tick(
                    dir.FullName,
                    shop.Day,
                    new FinancialDay[0],
                    new DeliveryStop[0],
                    new WarehouseHold[0]));
                var rolled = days.Tick(
                    dir.FullName,
                    4,
                    new FinancialDay[0],
                    new DeliveryStop[0],
                    new WarehouseHold[0]);
                shop = DisplayStock.Roll(shop, rolled);
                var before = DisplayStock.Gate(shop);
                var morning = DisplayStock.Morning(shop);
                var after = DisplayStock.Gate(morning);

                Assert.Equal(4, shop.Day);
                Assert.False(before.Stocked);
                Assert.True(morning.Ordered > 0);
                Assert.True(after.Stocked);
            }
            finally
            {
                Directory.Delete(dir.FullName, true);
            }
        }

        [Fact]
        public void Chain_new_game_day_roll_display_stock_writes_the_day4_order()
        {
            // Chain: new game → day roll → display stock → g2
            var dir = Directory.CreateDirectory(
                Path.Combine(Path.GetTempPath(), "display-order-" + Guid.NewGuid().ToString("N")));
            try
            {
                var shop = DisplayStock.Open();
                var days = new DayWatch();
                days.Tick(
                    dir.FullName,
                    shop.Day,
                    new FinancialDay[0],
                    new DeliveryStop[0],
                    new WarehouseHold[0]);
                var rolled = days.Tick(
                    dir.FullName,
                    4,
                    new FinancialDay[0],
                    new DeliveryStop[0],
                    new WarehouseHold[0]);
                shop = DisplayStock.Roll(shop, rolled);
                var morning = DisplayStock.Morning(shop);
                var written = new RestockWatch().Tick(
                    dir.FullName,
                    new object(),
                    morning.Day,
                    GameApi.Live(),
                    new WarehouseLevel[0],
                    displayOrdered: morning.Ordered);
                var health = DisplayStock.Gate(morning);

                Assert.Equal(Restock.Summary(4, shop.Lines.Count), Assert.Single(written));
                Assert.True(health.Stocked);
            }
            finally
            {
                Directory.Delete(dir.FullName, true);
            }
        }

        [Fact]
        public void Warehouse_order_wins_over_an_empty_display_summary()
        {
            var dir = Directory.CreateDirectory(
                Path.Combine(Path.GetTempPath(), "display-winner-" + Guid.NewGuid().ToString("N")));
            try
            {
                var written = new RestockWatch().Tick(
                    dir.FullName,
                    new object(),
                    4,
                    GameApi.Live(),
                    new[] { new WarehouseLevel("W1", "food", onHand: 0, daily: 4, ordered: false) },
                    displayOrdered: 5);

                var line = Assert.Single(written);
                Assert.StartsWith("restock.ordered ", line);
                Assert.DoesNotContain(written, row => row.StartsWith("restock.summary "));
            }
            finally
            {
                Directory.Delete(dir.FullName, true);
            }
        }
    }
}

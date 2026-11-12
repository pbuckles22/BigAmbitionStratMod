using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace ChaoticCEO.Tests
{
    public class CleanerTests
    {
        [Fact]
        public void Cleaner_an_empty_station_is_hired_again()
        {
            var api = GameApi.Live();
            var absent = Cleaner.Cover(
                "ba:street_seventhavenue 22",
                cleanerStaffed: false,
                resigned: false,
                retired: false,
                poached: false,
                api);
            var quit = Cleaner.Cover(
                "ba:street_seventhavenue 22",
                cleanerStaffed: false,
                resigned: true,
                retired: false,
                poached: false,
                api);
            var retired = Cleaner.Cover(
                "ba:street_seventhavenue 22",
                cleanerStaffed: false,
                resigned: true,
                retired: true,
                poached: true,
                api);
            var kept = Cleaner.Cover(
                "ba:street_seventhavenue 22",
                cleanerStaffed: true,
                resigned: true,
                retired: false,
                poached: false,
                api);

            Assert.True(absent.Hire);
            Assert.Equal("absent", absent.Reason);
            Assert.Equal(Cleaner.RoleName, absent.Pick.Role);
            Assert.Equal(Cleaner.Skill, absent.Pick.Skill);
            Assert.Equal("ba:street_seventhavenue", absent.Pick.Street);
            Assert.Equal(22, absent.Pick.Number);
            Assert.Equal(api.Call("hire"), absent.Pick.Call);
            Assert.Equal(api.Call("cleaner"), absent.Pick.StationCall);
            Assert.Equal("quit", quit.Reason);
            Assert.Equal("retired", retired.Reason);
            Assert.False(kept.Hire);
        }

        [Fact]
        public void Cleaner_a_paused_morning_hires_the_station_and_pulls_the_bags()
        {
            // Chain: store health → cleaner cover → paused morning → bag cover
            var facts = new StoreFacts(
                "ba:street_seventhavenue 22",
                temporarilyClosed: false,
                neededItems: new[] { "ba:itemname_cheapgift" },
                cargoOnHand: new Dictionary<string, int> { ["ba:itemname_cheapgift"] = 40 },
                registerStaffed: true,
                cleanerStaffed: false,
                staffSatisfaction: 0f,
                scheduledOpen: true,
                dailyCustomers: 81,
                dailyRevenue: 2297m,
                recentOpenDayProfits: new[] { 1442.656m, 1596.089m });
            var health = StoreHealth.Assess(facts);
            var gap = Cleaner.Cover(
                health.Place,
                facts.CleanerStaffed,
                resigned: false,
                retired: false,
                poached: false,
                GameApi.Live());
            var day = Morning.CoverDay(gap, autopilotOn: false, day: 1514, coveredDay: 1505);
            var bags = day == 0
                ? null
                : Wholesaler.Cover(
                    Wholesaler.PaperBag,
                    day,
                    nextDeliveryDay: 1520,
                    new[]
                    {
                        new CoverSale(1510, 77, open: true),
                        new CoverSale(1511, 81, open: true),
                    },
                    onHand: 0);
            var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "ceo-cleaner-" + Guid.NewGuid().ToString("N")));
            try
            {
                var line = Cleaner.Replaced(dir.FullName, gap.Pick, day, gap.Reason);
                Assert.Equal("restaff", StoreHeal.Decide(health).Action);
                Assert.False(health.Staffed);
                Assert.True(gap.Hire);
                Assert.Equal("absent", gap.Reason);
                Assert.Equal(1514, day);
                Assert.Equal(0, Morning.CoverDay(gap, autopilotOn: false, day: 1514, coveredDay: 1514));
                Assert.NotNull(bags);
                Assert.True(bags!.Replace);
                Assert.Equal(1515, bags.NextDay);
                Assert.Equal(bags.Target, bags.Need);
                Assert.True(bags.Need > Wholesaler.MinLineAmount);
                Assert.Contains("cleaner.replaced version=" + LoadLog.Version, line);
                Assert.Contains("reason=absent", line);
                Assert.Contains("street=ba:street_seventhavenue", line);
                Assert.Contains("number=22", line);
            }
            finally
            {
                dir.Delete(true);
            }
        }
    }
}

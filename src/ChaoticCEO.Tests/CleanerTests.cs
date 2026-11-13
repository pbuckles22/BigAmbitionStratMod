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

        [Fact]
        public void Cleaner_a_later_morning_leaves_a_staffed_station()
        {
            var staff = StoreStaff.FromShifts(
                new[] { new StaffShift("cleaner-1", "station-1", StaffShift.Cleaner) },
                warnedNoEmployee: false);
            var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "ceo-cleaner-kept-" + Guid.NewGuid().ToString("N")));
            try
            {
                var fromShift = Cleaner.Leave(
                    dir.FullName,
                    1521,
                    staff.CleanerStaffed,
                    controllerStaffed: false,
                    "ba:street_seventhavenue 22",
                    resigned: false,
                    retired: false,
                    poached: false,
                    GameApi.Live());
                var fromController = Cleaner.Leave(
                    dir.FullName,
                    1521,
                    shiftStaffed: false,
                    controllerStaffed: true,
                    "ba:street_seventhavenue 22",
                    resigned: false,
                    retired: false,
                    poached: false,
                    GameApi.Live());

                Assert.True(Cleaner.OnStation(shiftStaffed: true, controllerStaffed: false));
                Assert.True(Cleaner.OnStation(shiftStaffed: false, controllerStaffed: true));
                Assert.True(staff.CleanerStaffed);
                Assert.Equal("", fromShift);
                Assert.Equal("", fromController);
                Assert.False(File.Exists(LoadLog.CeoLogPath(dir.FullName)));
            }
            finally
            {
                dir.Delete(true);
            }
        }

        [Fact]
        public void Chain_day_roll_cleaner_on_station_does_not_hire()
        {
            // Chain: day roll → cleaner on station → no hire
            var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "ceo-cleaner-roll-" + Guid.NewGuid().ToString("N")));
            try
            {
                var firstRoll = DayRoll.Apply(
                    dir.FullName,
                    1519,
                    1520,
                    Array.Empty<FinancialDay>(),
                    Array.Empty<DeliveryStop>(),
                    Array.Empty<WarehouseHold>());
                var secondRoll = DayRoll.Apply(
                    dir.FullName,
                    1520,
                    1521,
                    Array.Empty<FinancialDay>(),
                    Array.Empty<DeliveryStop>(),
                    Array.Empty<WarehouseHold>());
                var firstDay = TopBar.Day(firstRoll);
                var secondDay = TopBar.Day(secondRoll);
                var staff = StoreStaff.FromShifts(
                    new[] { new StaffShift("cleaner-1", "station-1", StaffShift.Cleaner) },
                    warnedNoEmployee: false);
                var first = Cleaner.Leave(
                    dir.FullName,
                    firstDay,
                    staff.CleanerStaffed,
                    controllerStaffed: false,
                    "ba:street_seventhavenue 22",
                    resigned: false,
                    retired: false,
                    poached: false,
                    GameApi.Live());
                var second = Cleaner.Leave(
                    dir.FullName,
                    secondDay,
                    staff.CleanerStaffed,
                    controllerStaffed: false,
                    "ba:street_seventhavenue 22",
                    resigned: false,
                    retired: false,
                    poached: false,
                    GameApi.Live());

                Assert.Equal(1520, firstDay);
                Assert.Equal(1521, secondDay);
                Assert.True(staff.CleanerStaffed);
                Assert.True(Cleaner.OnStation(staff.CleanerStaffed, controllerStaffed: false));
                Assert.Equal("", first);
                Assert.Equal("", second);
                Assert.DoesNotContain(
                    "cleaner.replaced",
                    File.ReadAllText(LoadLog.CeoLogPath(dir.FullName)));
            }
            finally
            {
                dir.Delete(true);
            }
        }

        [Fact]
        public void Cleaner_a_later_morning_hires_an_empty_station()
        {
            var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "ceo-cleaner-empty-" + Guid.NewGuid().ToString("N")));
            try
            {
                var first = Cleaner.Leave(
                    dir.FullName,
                    1520,
                    shiftStaffed: false,
                    controllerStaffed: false,
                    "ba:street_seventhavenue 22",
                    resigned: false,
                    retired: false,
                    poached: false,
                    GameApi.Live());
                var second = Cleaner.Leave(
                    dir.FullName,
                    1521,
                    shiftStaffed: false,
                    controllerStaffed: false,
                    "ba:street_seventhavenue 22",
                    resigned: false,
                    retired: false,
                    poached: false,
                    GameApi.Live());

                Assert.False(Cleaner.OnStation(shiftStaffed: false, controllerStaffed: false));
                Assert.Contains("cleaner.replaced version=" + LoadLog.Version, first);
                Assert.Contains("day=1520", first);
                Assert.Contains("reason=absent", first);
                Assert.Contains("day=1521", second);
                Assert.Contains("reason=absent", second);
                Assert.Contains("street=ba:street_seventhavenue", second);
                Assert.Contains("number=22", second);
            }
            finally
            {
                dir.Delete(true);
            }
        }

        [Fact]
        public void Chain_day_roll_empty_station_hires_the_cleaner()
        {
            // Chain: day roll → empty station → hire
            var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "ceo-cleaner-empty-roll-" + Guid.NewGuid().ToString("N")));
            try
            {
                var firstRoll = DayRoll.Apply(
                    dir.FullName,
                    1519,
                    1520,
                    Array.Empty<FinancialDay>(),
                    Array.Empty<DeliveryStop>(),
                    Array.Empty<WarehouseHold>());
                var secondRoll = DayRoll.Apply(
                    dir.FullName,
                    1520,
                    1521,
                    Array.Empty<FinancialDay>(),
                    Array.Empty<DeliveryStop>(),
                    Array.Empty<WarehouseHold>());
                var firstDay = TopBar.Day(firstRoll);
                var secondDay = TopBar.Day(secondRoll);
                var staff = StoreStaff.FromShifts(Array.Empty<StaffShift>(), warnedNoEmployee: false);
                var first = Cleaner.Leave(
                    dir.FullName,
                    firstDay,
                    staff.CleanerStaffed,
                    controllerStaffed: false,
                    "ba:street_seventhavenue 22",
                    resigned: false,
                    retired: false,
                    poached: false,
                    GameApi.Live());
                var second = Cleaner.Leave(
                    dir.FullName,
                    secondDay,
                    staff.CleanerStaffed,
                    controllerStaffed: false,
                    "ba:street_seventhavenue 22",
                    resigned: false,
                    retired: false,
                    poached: false,
                    GameApi.Live());

                Assert.Equal(1520, firstDay);
                Assert.Equal(1521, secondDay);
                Assert.False(staff.CleanerStaffed);
                Assert.Contains("day=1520", first);
                Assert.Contains("reason=absent", first);
                Assert.Contains("day=1521", second);
                Assert.Contains("reason=absent", second);
            }
            finally
            {
                dir.Delete(true);
            }
        }
    }
}

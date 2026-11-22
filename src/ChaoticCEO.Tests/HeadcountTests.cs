using System.Collections.Generic;
using System.IO;

namespace ChaoticCEO.Tests
{
    public class HeadcountTests
    {
        [Fact]
        public void Headcount_one_cashier_and_one_cleaner_hires_nobody()
        {
            var plan = Headcount.Decide(new[]
            {
                Seat("cashier", Cashier.Skill, 12m, scheduled: true),
                Seat("cleaner", Cleaner.Skill, 8m, scheduled: true),
            });

            Assert.Equal(1, plan.Cashier.Count);
            Assert.Equal(1, plan.Cleaner.Count);
            Assert.False(plan.Cashier.Hire);
            Assert.False(plan.Cleaner.Hire);
            Assert.Equal("cashier", plan.Cashier.Keep);
            Assert.Equal("cleaner", plan.Cleaner.Keep);
            Assert.Empty(plan.Cashier.Drop);
            Assert.Empty(plan.Cleaner.Drop);
        }

        [Fact]
        public void Headcount_an_empty_roster_hires_one_of_each()
        {
            var plan = Headcount.Decide(new HeadcountSeat[0]);

            Assert.Equal(0, plan.Cashier.Count);
            Assert.Equal(0, plan.Cleaner.Count);
            Assert.True(plan.Cashier.Hire);
            Assert.True(plan.Cleaner.Hire);
            Assert.Empty(plan.Cashier.Drop);
            Assert.Empty(plan.Cleaner.Drop);
        }

        [Fact]
        public void Headcount_a_zero_wage_hire_still_counts()
        {
            var plan = Headcount.Decide(new[]
            {
                Seat("cashier", Cashier.Skill, 12m, scheduled: true),
                Seat("zero", Cleaner.Skill, 0m, scheduled: false),
            });

            Assert.Equal(1, plan.Cashier.Count);
            Assert.Equal(1, plan.Cleaner.Count);
            Assert.False(plan.Cashier.Hire);
            Assert.False(plan.Cleaner.Hire);
            Assert.Equal("zero", plan.Cleaner.Keep);
            Assert.Empty(plan.Cleaner.Drop);
        }

        [Fact]
        public void Headcount_extra_zero_wage_cleaners_are_not_hired()
        {
            var plan = Headcount.Decide(new[]
            {
                Seat("cashier", Cashier.Skill, 12m, scheduled: true),
                Seat("first", Cleaner.Skill, 0m, scheduled: false),
                Seat("second", Cleaner.Skill, 0m, scheduled: false),
                Seat("third", Cleaner.Skill, 0m, scheduled: false),
            });

            Assert.Equal(3, plan.Cleaner.Count);
            Assert.False(plan.Cleaner.Hire);
            Assert.Equal("third", plan.Cleaner.Keep);
            Assert.Equal(new[] { "first", "second" }, plan.Cleaner.Drop);
        }

        [Fact]
        public void Headcount_a_scheduled_cleaner_beats_a_later_zero_wage_row()
        {
            var plan = Headcount.Decide(new[]
            {
                Seat("on-shift", Cleaner.Skill, 0m, scheduled: true),
                Seat("extra", Cleaner.Skill, 0m, scheduled: false),
            });

            Assert.Equal(2, plan.Cleaner.Count);
            Assert.False(plan.Cleaner.Hire);
            Assert.Equal("on-shift", plan.Cleaner.Keep);
            Assert.Equal(new[] { "extra" }, plan.Cleaner.Drop);
        }

        [Fact]
        public void Headcount_a_zero_wage_cashier_still_counts()
        {
            var plan = Headcount.Decide(new[]
            {
                Seat("paid", Cashier.Skill, 12m, scheduled: true),
                Seat("zero", Cashier.Skill, 0m, scheduled: false),
            });

            Assert.Equal(2, plan.Cashier.Count);
            Assert.False(plan.Cashier.Hire);
            Assert.Equal("paid", plan.Cashier.Keep);
            Assert.Equal(new[] { "zero" }, plan.Cashier.Drop);
        }

        [Fact]
        public void Headcount_another_skill_is_not_a_cashier_or_a_cleaner()
        {
            var plan = Headcount.Decide(new[]
            {
                Seat("driver", "ba:skill_deliverydriver", 15m, scheduled: true),
            });

            Assert.Equal(0, plan.Cashier.Count);
            Assert.Equal(0, plan.Cleaner.Count);
            Assert.True(plan.Cashier.Hire);
            Assert.True(plan.Cleaner.Hire);
        }

        [Fact]
        public void Chain_morning_keeps_one_cashier_and_one_cleaner()
        {
            // Chain: morning → one cashier → one cleaner
            var day = Headcount.Open(1645, 1644);
            var roster = new[]
            {
                Seat("cashier", Cashier.Skill, 12m, scheduled: true),
                Seat("on-shift", Cleaner.Skill, 0m, scheduled: true),
                Seat("extra-a", Cleaner.Skill, 0m, scheduled: false),
                Seat("extra-b", Cleaner.Skill, 0m, scheduled: false),
            };
            var cashier = Headcount.Cashier(day, roster);
            var cleaner = Headcount.Cleaner(cashier, roster);
            var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "ceo-headcount-" + System.Guid.NewGuid().ToString("N")));
            try
            {
                var line = Headcount.Record(dir.FullName, cashier, cleaner);

                Assert.Equal(1645, day);
                Assert.Equal(1645, cashier.Day);
                Assert.Equal(1, cashier.Count);
                Assert.False(cashier.Hire);
                Assert.Empty(cashier.Drop);
                Assert.Equal(1645, cleaner.Day);
                Assert.Equal(3, cleaner.Count);
                Assert.False(cleaner.Hire);
                Assert.Equal("on-shift", cleaner.Keep);
                Assert.Equal(new[] { "extra-a", "extra-b" }, cleaner.Drop);
                Assert.Equal(
                    "headcount.held version=" + LoadLog.Version
                        + " day=1645 cashiers=1 cleaners=3 dropped=2",
                    line);
            }
            finally
            {
                dir.Delete(true);
            }
        }

        [Fact]
        public void Chain_a_closed_morning_does_not_drop_the_extra_cleaner()
        {
            // Chain: morning → one cashier → one cleaner
            var day = Headcount.Open(1645, 1645);
            var roster = new[]
            {
                Seat("cashier", Cashier.Skill, 12m, scheduled: true),
                Seat("on-shift", Cleaner.Skill, 0m, scheduled: true),
                Seat("extra", Cleaner.Skill, 0m, scheduled: false),
            };
            var cashier = Headcount.Cashier(day, roster);
            var cleaner = Headcount.Cleaner(cashier, roster);

            Assert.Equal(0, day);
            Assert.Equal(0, cashier.Day);
            Assert.False(cashier.Hire);
            Assert.Empty(cashier.Drop);
            Assert.Equal(0, cleaner.Day);
            Assert.Equal(2, cleaner.Count);
            Assert.False(cleaner.Hire);
            Assert.Empty(cleaner.Drop);
            Assert.Equal("", Headcount.Record(Path.GetTempPath(), cashier, cleaner));
        }

        [Fact]
        public void Headcount_a_cashier_at_another_building_is_kept()
        {
            var plan = Headcount.Decide(new[]
            {
                Seat("seventh", Cashier.Skill, 12m, scheduled: true, "ba:street_seventhavenue 15"),
                Seat("ninth", Cashier.Skill, 0m, scheduled: true, "ba:street_ninthavenue 15"),
                Seat("cleaner", Cleaner.Skill, 8m, scheduled: true, "ba:street_seventhavenue 15"),
            });

            Assert.Equal(2, plan.Cashier.Count);
            Assert.False(plan.Cashier.Hire);
            Assert.Empty(plan.Cashier.Drop);
            Assert.Empty(Headcount.Extra(plan.Cashier, plan.Cleaner));
        }

        [Fact]
        public void Headcount_a_second_cashier_at_the_same_building_is_extra()
        {
            var plan = Headcount.Decide(new[]
            {
                Seat("ada", Cashier.Skill, 12m, scheduled: true, "ba:street_ninthavenue 15"),
                Seat("bob", Cashier.Skill, 0m, scheduled: false, "ba:street_ninthavenue 15"),
            });

            Assert.Equal("ada", plan.Cashier.Keep);
            Assert.Equal(new[] { "bob" }, plan.Cashier.Drop);
        }

        [Fact]
        public void Chain_morning_keeps_a_cashier_at_the_next_building()
        {
            // Chain: morning → one cashier → one cleaner → cashier at the next building
            var day = Headcount.Open(1645, 1644);
            var roster = new[]
            {
                Seat("seventh", Cashier.Skill, 12m, scheduled: true, "ba:street_seventhavenue 15"),
                Seat("ninth", Cashier.Skill, 0m, scheduled: true, "ba:street_ninthavenue 15"),
                Seat("cleaner", Cleaner.Skill, 8m, scheduled: true, "ba:street_seventhavenue 15"),
            };
            var cashier = Headcount.Cashier(day, roster);
            var cleaner = Headcount.Cleaner(cashier, roster);

            Assert.Equal(1645, day);
            Assert.Equal(2, cashier.Count);
            Assert.False(cashier.Hire);
            Assert.Empty(cashier.Drop);
            Assert.Equal(1, cleaner.Count);
            Assert.Empty(Headcount.Extra(cashier, cleaner));
        }

        private static HeadcountSeat Seat(string id, string skill, decimal hourly, bool scheduled, string building = "")
        {
            return new HeadcountSeat(id, skill, hourly, scheduled, building);
        }
    }
}

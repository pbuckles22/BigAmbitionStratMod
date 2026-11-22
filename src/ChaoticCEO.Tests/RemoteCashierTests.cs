using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class RemoteCashierTests
{
    [Fact]
    public void Staying_outside_still_gets_a_cashier_and_a_closed_day_gets_no_shift()
    {
        var dir = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "ceo-remote-cashier-" + Guid.NewGuid().ToString("N")));
        try
        {
            var api = GameApi.Live();
            var rent = new BuildingRent("ba:street_ninthavenue", 15, true, api.Call("rent"));
            var pick = Starter.Choose(Starter.StageOne(), alreadyOpen: false, api);
            var laid = RemoteLayout.Decide(entered: false, rent, "ba:businesstype_empty", pick);
            var signed = Wholesaler.Sign(rent, pick, Wholesaler.StageOne(), alreadySigned: false, api);
            var hired = Cashier.Hire(rent, pick, signed, alreadyHired: false, api);
            var registers = new[]
            {
                new ServiceRegister("reg-1", Cashier.Skill),
                new ServiceRegister("station-1", StoreOps.CleanerSkill),
                new ServiceRegister("", Cashier.Skill),
            };
            var open = OpenWeek();
            var posted = new[] { new PostedShift(6, "ada", "reg-1") };
            var plan = RemoteCashier.Decide(false, hired, registers, open, posted, new[] { "ada" });
            var shifts = RemoteCashier.Shifts(plan, "reg-1");
            var written = RemoteCashier.Open(
                dir.FullName,
                rent,
                entered: false,
                laid,
                "ops.assigned street=ba:street_ninthavenue number=15",
                pick,
                23,
                registers,
                open,
                posted,
                new[] { "ada" });
            var text = File.ReadAllText(LoadLog.CeoLogPath(dir.FullName));

            Assert.True(hired.Hired);
            Assert.Equal(Cashier.Skill, hired.Skill);
            Assert.True(plan.Applied);
            Assert.Equal("", plan.Reason);
            Assert.Equal(Cashier.Skill, plan.Skill);
            Assert.Equal("reg-1", Assert.Single(plan.RegisterIds));
            Assert.Equal(new[] { 0, 1, 2, 3, 4, 5 }, plan.ShiftDays);
            Assert.DoesNotContain(6, plan.ShiftDays);
            Assert.Equal(6, plan.Shifts);
            Assert.Equal(1, plan.ClosedDays);
            Assert.Equal(1, plan.Purged);
            Assert.Equal(6, Assert.Single(plan.Stale).DayIndex);
            Assert.True(plan.Hire);
            Assert.Equal("", plan.EmployeeId);
            Assert.Equal(8, RemoteCashier.StartHour);
            Assert.Equal(16, RemoteCashier.EndHour);
            Assert.Equal(StaffShift.Register, Assert.Single(shifts).Kind);
            Assert.True(StoreStaff.FromShifts(shifts, false).RegisterStaffed);
            Assert.False(StoreStaff.FromShifts(shifts, false).CleanerStaffed);
            Assert.Contains("hire.cashier ", written);
            Assert.Contains("skill=" + Cashier.Skill, written);
            Assert.Contains("call=" + api.Call("hire"), written);
            Assert.Contains("cashier.shift ", text);
            Assert.Contains("street=ba:street_ninthavenue", text);
            Assert.Contains("number=15", text);
            Assert.Contains("register=reg-1", text);
            Assert.Contains("shifts=6", text);
            Assert.Contains("purged=1", text);
            Assert.Contains("closed=1", text);
            Assert.Contains("person=false", text);
            Assert.DoesNotContain("hire.missed", text);
            Assert.True(text.IndexOf("hire.cashier ", StringComparison.Ordinal) < text.IndexOf("cashier.shift ", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void A_store_with_no_cashier_still_recruits_when_another_store_has_one()
    {
        Assert.True(RemoteCashier.Recruit(true, 0));
        Assert.False(RemoteCashier.Recruit(true, 1));
        Assert.False(RemoteCashier.Recruit(false, 0));
    }

    [Fact]
    public void A_closed_week_does_not_hire_a_cashier()
    {
        var api = GameApi.Live();
        var rent = new BuildingRent("ba:street_ninthavenue", 15, true, api.Call("rent"));
        var pick = Starter.Choose(Starter.StageOne(), alreadyOpen: false, api);
        var signed = Wholesaler.Sign(rent, pick, Wholesaler.StageOne(), alreadySigned: false, api);
        var hired = Cashier.Hire(rent, pick, signed, alreadyHired: false, api);
        var plan = RemoteCashier.Decide(
            false,
            hired,
            new[] { new ServiceRegister("reg-1", Cashier.Skill) },
            new[] { false, false, false, false, false, false, false },
            null,
            null);

        Assert.False(plan.Applied);
        Assert.Equal("closed", plan.Reason);
        Assert.Empty(plan.ShiftDays);
        Assert.Equal(0, plan.Shifts);
        Assert.False(plan.Hire);
        Assert.Empty(RemoteCashier.Shifts(plan, "reg-1"));
    }

    [Fact]
    public void A_live_cashier_keeps_the_open_days_and_a_stale_shift_is_purged()
    {
        var api = GameApi.Live();
        var rent = new BuildingRent("ba:street_ninthavenue", 15, true, api.Call("rent"));
        var pick = Starter.Choose(Starter.StageOne(), alreadyOpen: false, api);
        var signed = Wholesaler.Sign(rent, pick, Wholesaler.StageOne(), alreadySigned: false, api);
        var hired = Cashier.Hire(rent, pick, signed, alreadyHired: false, api);
        var posted = new[]
        {
            new PostedShift(6, "ada", "reg-1"),
            new PostedShift(1, "ghost", "reg-1"),
            new PostedShift(0, "bob", "gone-reg", orphan: true),
            new PostedShift(2, "ada", "reg-2"),
        };
        var plan = RemoteCashier.Decide(
            false,
            hired,
            new[]
            {
                new ServiceRegister("reg-2", Cashier.Skill),
                new ServiceRegister("reg-1", Cashier.Skill),
                new ServiceRegister("station-1", StoreOps.CleanerSkill),
            },
            OpenWeek(),
            posted,
            new[] { "ada" });

        Assert.True(plan.Applied);
        Assert.False(plan.Hire);
        Assert.Equal("ada", plan.EmployeeId);
        Assert.Equal(new[] { "reg-2", "reg-1" }, plan.RegisterIds);
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5 }, plan.ShiftDays);
        Assert.Equal(12, plan.Shifts);
        Assert.Equal(3, plan.Purged);
        Assert.Contains(plan.Stale, shift => shift.DayIndex == 6 && shift.EmployeeId == "ada");
        Assert.Contains(plan.Stale, shift => shift.EmployeeId == "ghost");
        Assert.Contains(plan.Stale, shift => shift.Orphan && shift.EmployeeId == "bob");
        Assert.DoesNotContain(plan.Stale, shift => shift.DayIndex == 2 && shift.EmployeeId == "ada");
        Assert.Equal(StaffShift.Register, Assert.Single(RemoteCashier.Shifts(plan, "ada")).Kind);
    }

    [Fact]
    public void Walking_in_does_not_take_the_remote_cashier()
    {
        var dir = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "ceo-remote-cashier-inside-" + Guid.NewGuid().ToString("N")));
        try
        {
            var api = GameApi.Live();
            var rent = new BuildingRent("ba:street_ninthavenue", 15, true, api.Call("rent"));
            var pick = Starter.Choose(Starter.StageOne(), alreadyOpen: false, api);
            var laid = RemoteLayout.Decide(entered: true, rent, "", pick);
            var signed = Wholesaler.Sign(rent, pick, Wholesaler.StageOne(), alreadySigned: false, api);
            var hired = Cashier.Hire(rent, pick, signed, alreadyHired: false, api);
            var plan = RemoteCashier.Decide(
                true,
                hired,
                new[] { new ServiceRegister("reg-1", Cashier.Skill) },
                OpenWeek(),
                null,
                null);
            var written = RemoteCashier.Open(
                dir.FullName,
                rent,
                entered: true,
                laid,
                "ops.assigned ",
                pick,
                23,
                new[] { new ServiceRegister("reg-1", Cashier.Skill) },
                OpenWeek(),
                null,
                null);

            Assert.True(laid.Applied);
            Assert.False(plan.Applied);
            Assert.Equal("inscene", plan.Reason);
            Assert.False(plan.Hire);
            Assert.Equal("", written);
            Assert.False(File.Exists(LoadLog.CeoLogPath(dir.FullName)));
        }
        finally
        {
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void No_customer_service_register_does_not_hire()
    {
        var dir = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "ceo-remote-cashier-miss-" + Guid.NewGuid().ToString("N")));
        try
        {
            var api = GameApi.Live();
            var rent = new BuildingRent("ba:street_ninthavenue", 15, true, api.Call("rent"));
            var pick = Starter.Choose(Starter.StageOne(), alreadyOpen: false, api);
            var laid = RemoteLayout.Decide(entered: false, rent, "", pick);
            var plan = RemoteCashier.Decide(
                false,
                Cashier.Hire(
                    rent,
                    pick,
                    Wholesaler.Sign(rent, pick, Wholesaler.StageOne(), false, api),
                    false,
                    api),
                new[] { new ServiceRegister("station-1", StoreOps.CleanerSkill) },
                OpenWeek(),
                null,
                null);
            var written = RemoteCashier.Open(
                dir.FullName,
                rent,
                false,
                laid,
                "ops.assigned ",
                pick,
                23,
                new[] { new ServiceRegister("station-1", StoreOps.CleanerSkill) },
                OpenWeek(),
                null,
                null);
            var text = File.ReadAllText(LoadLog.CeoLogPath(dir.FullName));

            Assert.False(plan.Applied);
            Assert.Equal("nostation", plan.Reason);
            Assert.Empty(plan.RegisterIds);
            Assert.Contains("hire.missed ", written);
            Assert.Contains("reason=nostation", written);
            Assert.DoesNotContain("hire.cashier", text);
            Assert.DoesNotContain("cashier.shift", text);
        }
        finally
        {
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void Chain_remote_layout_ops_remote_cashier_remote()
    {
        // Chain: remote layout → ops remote → cashier remote
        var dir = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "ceo-remote-cashier-chain-" + Guid.NewGuid().ToString("N")));
        try
        {
            var api = GameApi.Live();
            var rented = Rent.Choose(
                new[]
                {
                    new RentLot("ba:street_high", 2, 80m),
                    new RentLot("ba:street_ninthavenue", 15, 33m),
                },
                cash: 15000m,
                anyRented: false,
                api);
            var rentLine = Rent.Record(dir.FullName, rented, 23);
            var pick = Starter.Choose(Starter.StageOne(), alreadyOpen: false, api);
            var laid = RemoteLayout.Decide(entered: false, rented, "ba:businesstype_empty", pick);
            var layoutLine = RemoteLayout.Open(dir.FullName, rented, entered: false, "ba:businesstype_empty", pick, 23);
            var opsLine = RemoteOps.Open(
                dir.FullName,
                rented,
                entered: false,
                laid,
                pick,
                StoreFixtures.GiftShopWholesalers(),
                StoreOps.Opening(),
                23,
                "station-1");
            var registers = new[]
            {
                new ServiceRegister("reg-1", Cashier.Skill),
                new ServiceRegister("station-1", StoreOps.CleanerSkill),
            };
            var hired = Cashier.Hire(
                rented,
                pick,
                Wholesaler.Sign(rented, pick, Wholesaler.StageOne(), alreadySigned: false, api),
                alreadyHired: false,
                api);
            var plan = RemoteCashier.Decide(false, hired, registers, OpenWeek(), null, null);
            var cashierLine = RemoteCashier.Open(
                dir.FullName,
                rented,
                entered: false,
                laid,
                opsLine,
                pick,
                23,
                registers,
                OpenWeek(),
                null,
                null);
            var text = File.ReadAllText(LoadLog.CeoLogPath(dir.FullName));

            Assert.Contains("street=ba:street_ninthavenue", rentLine);
            Assert.Equal(15, rented.Number);
            Assert.True(laid.Applied);
            Assert.Equal("ba:businesstype_giftshop", laid.BusinessType);
            Assert.Contains("type=ba:businesstype_giftshop", layoutLine);
            Assert.Contains("ops.assigned ", opsLine);
            Assert.Contains("street=" + rented.Street, opsLine);
            Assert.Contains("number=15", opsLine);
            Assert.True(hired.Hired);
            Assert.Equal(Cashier.Skill, hired.Skill);
            Assert.Equal("Helpers.EmployeeHelper.HireCandidate", hired.Call);
            Assert.True(plan.Applied);
            Assert.Equal("reg-1", Assert.Single(plan.RegisterIds));
            Assert.DoesNotContain(6, plan.ShiftDays);
            Assert.Equal(6, plan.Shifts);
            Assert.Contains("hire.cashier ", cashierLine);
            Assert.Contains("street=" + rented.Street, cashierLine);
            Assert.Contains("number=" + rented.Number.ToString(), cashierLine);
            Assert.Contains("skill=" + Cashier.Skill, cashierLine);
            Assert.Contains("call=" + hired.Call, cashierLine);
            Assert.Contains("cashier.shift ", text);
            Assert.Contains("register=reg-1", text);
            Assert.Contains("shifts=6", text);
            Assert.Contains("closed=1", text);
            Assert.Contains("person=false", text);
            Assert.True(text.IndexOf(layoutLine, StringComparison.Ordinal) < text.IndexOf(opsLine, StringComparison.Ordinal));
            Assert.True(text.IndexOf(opsLine, StringComparison.Ordinal) < text.IndexOf("hire.cashier ", StringComparison.Ordinal));
            Assert.DoesNotContain("hire.missed", text);
            Assert.DoesNotContain("not-inside", text);
            Assert.DoesNotContain("nostation", text);
        }
        finally
        {
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void Chain_remote_layout_ops_remote_cashier_remote_purges_a_stale_shift_and_skips_the_closed_day()
    {
        // Chain: remote layout → ops remote → cashier remote
        var dir = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "ceo-remote-cashier-stale-" + Guid.NewGuid().ToString("N")));
        try
        {
            var api = GameApi.Live();
            var rented = Rent.Choose(
                new[] { new RentLot("ba:street_ninthavenue", 15, 33m) },
                cash: 15000m,
                anyRented: false,
                api);
            var rentLine = Rent.Record(dir.FullName, rented, 23);
            var pick = Starter.Choose(Starter.StageOne(), alreadyOpen: false, api);
            var laid = RemoteLayout.Decide(entered: false, rented, "ba:businesstype_empty", pick);
            var layoutLine = RemoteLayout.Open(dir.FullName, rented, entered: false, "ba:businesstype_empty", pick, 23);
            var opsLine = RemoteOps.Open(
                dir.FullName,
                rented,
                entered: false,
                laid,
                pick,
                StoreFixtures.GiftShopWholesalers(),
                StoreOps.Opening(),
                23,
                "station-1");
            var registers = new[] { new ServiceRegister("reg-1", Cashier.Skill) };
            var posted = new[]
            {
                new PostedShift(6, "ada", "reg-1"),
                new PostedShift(2, "ada", "reg-1"),
                new PostedShift(1, "ghost", "reg-1"),
            };
            var hired = Cashier.Hire(
                rented,
                pick,
                Wholesaler.Sign(rented, pick, Wholesaler.StageOne(), false, api),
                false,
                api);
            var plan = RemoteCashier.Decide(false, hired, registers, OpenWeek(), posted, new[] { "ada" });
            var cashierLine = RemoteCashier.Open(
                dir.FullName,
                rented,
                false,
                laid,
                opsLine,
                pick,
                23,
                registers,
                OpenWeek(),
                posted,
                new[] { "ada" });
            var text = File.ReadAllText(LoadLog.CeoLogPath(dir.FullName));

            Assert.Contains("number=15", rentLine);
            Assert.True(laid.Applied);
            Assert.Contains("layout.loaded ", layoutLine);
            Assert.Contains("ops.assigned ", opsLine);
            Assert.Contains("cleaner=Cleaner", opsLine);
            Assert.False(plan.Hire);
            Assert.Equal("ada", plan.EmployeeId);
            Assert.DoesNotContain(6, plan.ShiftDays);
            Assert.Equal(2, plan.Purged);
            Assert.Contains("cashier.shift ", cashierLine);
            Assert.Contains("street=" + rented.Street, cashierLine);
            Assert.Contains("employee=ada", cashierLine);
            Assert.Contains("shifts=6", cashierLine);
            Assert.Contains("purged=2", cashierLine);
            Assert.Contains("closed=1", cashierLine);
            Assert.DoesNotContain("hire.cashier", text);
            Assert.True(text.IndexOf(opsLine, StringComparison.Ordinal) < text.IndexOf(cashierLine, StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void Chain_remote_layout_ops_remote_cashier_remote_stays_quiet_without_ops()
    {
        // Chain: remote layout → ops remote → cashier remote
        var dir = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "ceo-remote-cashier-quiet-" + Guid.NewGuid().ToString("N")));
        try
        {
            var api = GameApi.Live();
            var rented = Rent.Choose(
                new[] { new RentLot("ba:street_ninthavenue", 15, 33m) },
                cash: 15000m,
                anyRented: false,
                api);
            var rentLine = Rent.Record(dir.FullName, rented, 23);
            var pick = Starter.Choose(Starter.StageOne(), alreadyOpen: true, api);
            var laid = RemoteLayout.Decide(entered: false, rented, "", pick);
            var layoutLine = RemoteLayout.Open(dir.FullName, rented, entered: false, "", pick, 23);
            var opsLine = RemoteOps.Open(
                dir.FullName,
                rented,
                entered: false,
                laid,
                pick,
                StoreFixtures.GiftShopWholesalers(),
                StoreOps.Opening(),
                23,
                "station-1");
            var cashierLine = RemoteCashier.Open(
                dir.FullName,
                rented,
                false,
                laid,
                opsLine,
                pick,
                23,
                new[] { new ServiceRegister("reg-1", Cashier.Skill) },
                OpenWeek(),
                null,
                null);
            var text = File.ReadAllText(LoadLog.CeoLogPath(dir.FullName));

            Assert.Contains("street=" + rented.Street, rentLine);
            Assert.False(laid.Applied);
            Assert.Equal("", layoutLine);
            Assert.Equal("", opsLine);
            Assert.Equal("", cashierLine);
            Assert.DoesNotContain("hire.cashier", text);
            Assert.DoesNotContain("cashier.shift", text);
            Assert.DoesNotContain("hire.missed", text);
        }
        finally
        {
            Directory.Delete(dir.FullName, true);
        }
    }

    private static bool[] OpenWeek()
    {
        return new[] { true, true, true, true, true, true, false };
    }
}

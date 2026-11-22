using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class RemoteOpsTests
{
    [Fact]
    public void Open_address_the_player_does_not_enter_still_gets_a_cleaner()
    {
        var dir = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "ceo-remote-ops-" + Guid.NewGuid().ToString("N")));
        try
        {
            var api = GameApi.Live();
            var rent = new BuildingRent("ba:street_seventhavenue", 15, true, api.Call("rent"));
            var pick = Starter.Choose(Starter.StageOne(), alreadyOpen: false, api);
            var laid = RemoteLayout.Decide(entered: false, rent, "ba:businesstype_empty", pick);
            var fixtures = StoreFixtures.GiftShopWholesalers();
            var assigned = StoreOps.Assign(pick, StoreOps.Opening(), fixtures, api);
            var plan = RemoteOps.Decide(
                inScene: false,
                assigned,
                "station-1",
                "",
                uniformLocker: true,
                uniformSaveReady: true,
                presetIds: null,
                scheduleDays: 7,
                existingCleanerId: null,
                existingGuardId: null);
            var shifts = RemoteOps.Shifts(plan, "station-1");
            var staff = StoreStaff.FromShifts(shifts, warnedNoEmployee: false);
            var written = RemoteOps.Open(
                dir.FullName,
                rent,
                entered: false,
                laid,
                pick,
                fixtures,
                StoreOps.Opening(),
                1,
                "station-1");
            var text = File.ReadAllText(LoadLog.CeoLogPath(dir.FullName));

            Assert.True(laid.Applied);
            Assert.Equal("ba:businesstype_giftshop", laid.BusinessType);
            Assert.Equal("Cleaner", assigned.Cleaner);
            Assert.Equal("", assigned.Guard);
            Assert.False(assigned.Uniforms);
            Assert.True(plan.Applied);
            Assert.Equal("", plan.Reason);
            Assert.Equal("station-1", plan.CleanerItemId);
            Assert.Equal("", plan.GuardItemId);
            Assert.True(plan.HireCleaner);
            Assert.False(plan.HireGuard);
            Assert.Equal(8, RemoteOps.StartHour);
            Assert.Equal(16, RemoteOps.EndHour);
            Assert.Equal(StaffShift.Cleaner, Assert.Single(shifts).Kind);
            Assert.True(staff.CleanerStaffed);
            Assert.False(staff.RegisterStaffed);
            Assert.Contains("ops.assigned ", written);
            Assert.Contains("street=ba:street_seventhavenue", written);
            Assert.Contains("number=15", written);
            Assert.Contains("cleaner=Cleaner", written);
            Assert.Contains("guard=", written);
            Assert.Contains("uniforms=false", written);
            Assert.Contains("cleanerCall=" + assigned.CleanerCall, written);
            Assert.Contains("ops.cleaner ", text);
            Assert.Contains("shift=true", text);
            Assert.Contains("person=false", text);
            Assert.Contains("type=CleanerEmployee", text);
            Assert.Contains("placed=false", text);
            Assert.Contains("employee=station-1", text);
            Assert.Contains("station=station-1", text);
            Assert.DoesNotContain("ops.missed", text);
            Assert.DoesNotContain("not-inside", text);
            Assert.DoesNotContain("nostation", text);
        }
        finally
        {
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void A_guard_outside_the_scene_is_scheduled_and_is_not_a_cashier()
    {
        var api = GameApi.Live();
        var rent = new BuildingRent("ba:street_seventhavenue", 15, true, api.Call("rent"));
        var pick = Starter.Choose(Starter.StageOne(), alreadyOpen: false, api);
        var facts = new StoreOpsFacts(1, 100m, 2m, uniformsDemanded: true, canMeetUniforms: true);
        var assigned = StoreOps.Assign(pick, facts, StoreFixtures.Present(), api);
        var plan = RemoteOps.Decide(
            false,
            assigned,
            "station-1",
            "locker-1",
            uniformLocker: true,
            uniformSaveReady: true,
            new[] { "preset-1" },
            7,
            null,
            null);

        Assert.Equal("Guard", assigned.Guard);
        Assert.True(assigned.Uniforms);
        Assert.True(plan.Applied);
        Assert.Equal("locker-1", plan.GuardItemId);
        Assert.True(plan.HireGuard);
        Assert.Equal("preset-1", plan.PresetId);
        Assert.Equal("", RemoteOps.ShiftKind("ba:itemname_securityguardlocker", cleaningShift: false));
        Assert.Equal(StaffShift.Cleaner, RemoteOps.ShiftKind("ba:itemname_cleaningstation", cleaningShift: false));
        Assert.Equal(StaffShift.Cleaner, RemoteOps.ShiftKind("ba:itemname_cashregister", cleaningShift: true));
        Assert.Equal(StaffShift.Register, RemoteOps.ShiftKind("ba:itemname_cashregister", cleaningShift: false));
        Assert.DoesNotContain(RemoteOps.Shifts(plan, "station-1"), shift => shift.Kind == StaffShift.Register);
    }

    [Fact]
    public void Uniforms_outside_the_scene_dress_the_cleaner_skill()
    {
        var api = GameApi.Live();
        var pick = Starter.Choose(Starter.StageOne(), alreadyOpen: false, api);
        var facts = new StoreOpsFacts(1, 100m, 2m, true, true);
        var assigned = StoreOps.Assign(pick, facts, StoreFixtures.GiftShopWholesalers(), api);
        var plan = RemoteOps.Decide(
            false,
            assigned,
            "station-1",
            "",
            true,
            true,
            new[] { "preset-1" },
            7,
            null,
            null);
        var skills = RemoteOps.UniformSkillList(plan);
        var bySkill = new Dictionary<string, string>();
        var seeded = StoreOps.SeedUniforms(bySkill, skills, plan.PresetId);

        Assert.Equal("", assigned.Guard);
        Assert.True(plan.Applied);
        Assert.True(plan.Uniforms);
        Assert.Equal("preset-1", plan.PresetId);
        Assert.Contains(StoreOps.CleanerSkill, skills);
        Assert.Contains(StoreOps.CleanerSkill, seeded);
        Assert.True(StoreOps.Dressed(bySkill, StoreOps.CleanerSkill));
        Assert.Equal("preset-1", bySkill[StoreOps.CleanerSkill]);
    }

    [Fact]
    public void No_cleaning_station_in_the_save_does_not_get_a_cleaner()
    {
        var dir = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "ceo-remote-ops-miss-" + Guid.NewGuid().ToString("N")));
        try
        {
            var api = GameApi.Live();
            var rent = new BuildingRent("ba:street_seventhavenue", 15, true, api.Call("rent"));
            var pick = Starter.Choose(Starter.StageOne(), alreadyOpen: false, api);
            var laid = RemoteLayout.Decide(entered: false, rent, "", pick);
            var fixtures = StoreFixtures.GiftShopWholesalers();
            var assigned = StoreOps.Assign(pick, StoreOps.Opening(), fixtures, api);
            var plan = RemoteOps.Decide(
                false,
                assigned,
                "",
                "",
                true,
                true,
                null,
                7,
                null,
                null);
            var written = RemoteOps.Open(
                dir.FullName,
                rent,
                false,
                laid,
                pick,
                fixtures,
                StoreOps.Opening(),
                1,
                "");
            var text = File.ReadAllText(LoadLog.CeoLogPath(dir.FullName));

            Assert.False(plan.Applied);
            Assert.Equal("nostation", plan.Reason);
            Assert.Equal("", plan.CleanerItemId);
            Assert.False(plan.HireCleaner);
            Assert.Empty(RemoteOps.Shifts(plan, "station-1"));
            Assert.False(StoreStaff.FromShifts(RemoteOps.Shifts(plan, "station-1"), false).CleanerStaffed);
            Assert.Contains("ops.missed ", written);
            Assert.Contains("reason=nostation", written);
            Assert.DoesNotContain("ops.assigned", text);
            Assert.DoesNotContain("ops.cleaner", text);
        }
        finally
        {
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void Walking_in_does_not_take_the_remote_schedule()
    {
        var dir = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "ceo-remote-ops-inside-" + Guid.NewGuid().ToString("N")));
        try
        {
            var api = GameApi.Live();
            var rent = new BuildingRent("ba:street_seventhavenue", 15, true, api.Call("rent"));
            var pick = Starter.Choose(Starter.StageOne(), alreadyOpen: false, api);
            var laid = RemoteLayout.Decide(entered: true, rent, "", pick);
            var plan = RemoteOps.Decide(
                true,
                StoreOps.Assign(pick, StoreOps.Opening(), StoreFixtures.GiftShopWholesalers(), api),
                "station-1",
                "",
                true,
                true,
                null,
                7,
                null,
                null);
            var written = RemoteOps.Open(
                dir.FullName,
                rent,
                entered: true,
                laid,
                pick,
                StoreFixtures.GiftShopWholesalers(),
                StoreOps.Opening(),
                1,
                "station-1");

            Assert.True(laid.Applied);
            Assert.False(plan.Applied);
            Assert.Equal("inscene", plan.Reason);
            Assert.False(plan.HireCleaner);
            Assert.Equal("", written);
            Assert.False(File.Exists(LoadLog.CeoLogPath(dir.FullName)));
        }
        finally
        {
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void A_missing_guard_item_does_not_claim_the_cleaner()
    {
        var api = GameApi.Live();
        var pick = Starter.Choose(Starter.StageOne(), alreadyOpen: false, api);
        var assigned = StoreOps.Assign(
            pick,
            new StoreOpsFacts(1, 100m, 2m, false, false),
            StoreFixtures.Present(),
            api);
        var plan = RemoteOps.Decide(false, assigned, "station-1", "", false, true, null, 7, null, null);

        Assert.Equal("Guard", assigned.Guard);
        Assert.False(plan.Applied);
        Assert.Equal("nostation", plan.Reason);
        Assert.Equal("", plan.CleanerItemId);
        Assert.False(plan.HireCleaner);
        Assert.Empty(RemoteOps.Shifts(plan, "station-1"));
    }

    [Fact]
    public void A_missing_uniform_locker_does_not_claim_the_ops()
    {
        var api = GameApi.Live();
        var pick = Starter.Choose(Starter.StageOne(), alreadyOpen: false, api);
        var assigned = StoreOps.Assign(
            pick,
            new StoreOpsFacts(1, 100m, 2m, true, true),
            StoreFixtures.Present(),
            api);
        var plan = RemoteOps.Decide(
            false,
            assigned,
            "station-1",
            "locker-1",
            false,
            true,
            new[] { "preset-1" },
            7,
            null,
            null);

        Assert.True(assigned.Uniforms);
        Assert.False(plan.Applied);
        Assert.Equal("nolocker", plan.Reason);
        Assert.False(plan.Uniforms);
    }

    [Fact]
    public void No_saved_days_does_not_schedule_a_cleaner()
    {
        var api = GameApi.Live();
        var pick = Starter.Choose(Starter.StageOne(), alreadyOpen: false, api);
        var assigned = StoreOps.Assign(pick, StoreOps.Opening(), StoreFixtures.GiftShopWholesalers(), api);
        var plan = RemoteOps.Decide(false, assigned, "station-1", "", true, true, null, 0, null, null);

        Assert.False(plan.Applied);
        Assert.Equal("nodays", plan.Reason);
        Assert.Empty(RemoteOps.Shifts(plan, "station-1"));
    }

    [Fact]
    public void An_existing_cleaner_is_kept()
    {
        var api = GameApi.Live();
        var pick = Starter.Choose(Starter.StageOne(), alreadyOpen: false, api);
        var assigned = StoreOps.Assign(pick, StoreOps.Opening(), StoreFixtures.GiftShopWholesalers(), api);
        var plan = RemoteOps.Decide(false, assigned, "station-1", "", true, true, null, 7, "kept-1", null);
        var staff = StoreStaff.FromShifts(RemoteOps.Shifts(plan, "kept-1"), false);

        Assert.True(plan.Applied);
        Assert.False(plan.HireCleaner);
        Assert.Equal("station-1", Assert.Single(RemoteOps.Shifts(plan, "kept-1")).ItemInstanceId);
        Assert.True(staff.CleanerStaffed);
    }

    [Fact]
    public void A_named_layout_with_no_fixtures_is_applied_before_ops()
    {
        var api = GameApi.Live();
        var pick = Starter.Choose(Starter.StageOne(), alreadyOpen: false, api);

        Assert.Equal("GiftShopWholesalers", pick.Layout);
        Assert.Equal("apply", FixtureGate.Next(false, pick.Layout));
        Assert.Equal("ops", FixtureGate.Next(true, pick.Layout));
        Assert.Equal("no-items", FixtureGate.Next(false, ""));
        Assert.Equal("no-items", FixtureGate.Next(false, null));
    }

    [Fact]
    public void Chain_remote_layout_applies_missing_fixtures_then_ops_remote()
    {
        // Chain: remote layout → ops remote
        var dir = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "ceo-remote-ops-fixtures-" + Guid.NewGuid().ToString("N")));
        try
        {
            var api = GameApi.Live();
            var rented = Rent.Choose(
                new[] { new RentLot("ba:street_ninthavenue", 15, 33m) },
                cash: 15000m,
                anyRented: false,
                api);
            var rentLine = Rent.Record(dir.FullName, rented, 20);
            var pick = Starter.Choose(Starter.StageOne(), alreadyOpen: false, api);
            var laid = RemoteLayout.Decide(entered: false, rented, "ba:businesstype_empty", pick);
            var layoutLine = RemoteLayout.Open(dir.FullName, rented, entered: false, "ba:businesstype_empty", pick, 20);
            var gate = FixtureGate.Next(hasItems: false, pick.Layout);
            var opsLine = RemoteOps.Open(
                dir.FullName,
                rented,
                entered: false,
                laid,
                pick,
                StoreFixtures.GiftShopWholesalers(),
                StoreOps.Opening(),
                20,
                "station-1");
            var text = File.ReadAllText(LoadLog.CeoLogPath(dir.FullName));

            Assert.Contains("street=ba:street_ninthavenue", rentLine);
            Assert.Equal(15, rented.Number);
            Assert.True(laid.Applied);
            Assert.Contains("type=ba:businesstype_giftshop", layoutLine);
            Assert.Equal("apply", gate);
            Assert.Contains("ops.assigned ", opsLine);
            Assert.Contains("street=" + rented.Street, opsLine);
            Assert.Contains("number=15", opsLine);
            Assert.Contains("cleaner=Cleaner", opsLine);
            Assert.DoesNotContain("no-items", text);
            Assert.DoesNotContain("ops.missed", text);
        }
        finally
        {
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void Uniforms_without_a_preset_do_not_apply()
    {
        var api = GameApi.Live();
        var pick = Starter.Choose(Starter.StageOne(), alreadyOpen: false, api);
        var assigned = StoreOps.Assign(
            pick,
            new StoreOpsFacts(1, 100m, 2m, true, true),
            StoreFixtures.GiftShopWholesalers(),
            api);
        var plan = RemoteOps.Decide(false, assigned, "station-1", "", true, true, null, 7, null, null);

        Assert.True(assigned.Uniforms);
        Assert.False(plan.Applied);
        Assert.Equal("nopreset", plan.Reason);
        Assert.False(plan.Uniforms);
        Assert.Empty(RemoteOps.UniformSkillList(plan));
    }

    [Fact]
    public void Fixtures_with_no_station_stay_unassigned()
    {
        var dir = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "ceo-remote-ops-empty-" + Guid.NewGuid().ToString("N")));
        try
        {
            var api = GameApi.Live();
            var rent = new BuildingRent("ba:street_seventhavenue", 15, true, api.Call("rent"));
            var pick = Starter.Choose(Starter.StageOne(), alreadyOpen: false, api);
            var laid = RemoteLayout.Decide(entered: false, rent, "", pick);
            var written = RemoteOps.Open(
                dir.FullName,
                rent,
                false,
                laid,
                pick,
                new StoreFixtures(false, false, false),
                StoreOps.Opening(),
                1,
                "station-1");
            var text = File.ReadAllText(LoadLog.CeoLogPath(dir.FullName));

            Assert.Contains("ops.missed ", written);
            Assert.Contains("reason=empty", written);
            Assert.DoesNotContain("ops.assigned", text);
        }
        finally
        {
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void Chain_open_rent_remote_layout_ops_remote()
    {
        // Chain: open rent → remote layout → ops remote
        var dir = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "ceo-remote-ops-chain-" + Guid.NewGuid().ToString("N")));
        try
        {
            var api = GameApi.Live();
            var rented = Rent.Choose(
                new[]
                {
                    new RentLot("ba:street_high", 2, 80m),
                    new RentLot("ba:street_seventhavenue", 15, 40m),
                },
                cash: 15000m,
                anyRented: false,
                api);
            var rentLine = Rent.Record(dir.FullName, rented, 1);
            var pick = Starter.Choose(Starter.StageOne(), alreadyOpen: false, api);
            var laid = RemoteLayout.Decide(entered: false, rented, "", pick);
            var layoutLine = RemoteLayout.Open(dir.FullName, rented, entered: false, "", pick, 1);
            var fixtures = StoreFixtures.GiftShopWholesalers();
            var assigned = StoreOps.Assign(pick, StoreOps.Opening(), fixtures, api);
            var plan = RemoteOps.Decide(
                false,
                assigned,
                "station-1",
                "",
                fixtures.UniformLocker,
                true,
                null,
                7,
                null,
                null);
            var opsLine = RemoteOps.Open(
                dir.FullName,
                rented,
                entered: false,
                laid,
                pick,
                fixtures,
                StoreOps.Opening(),
                1,
                "station-1",
                "",
                fixtures.UniformLocker);
            var staff = StoreStaff.FromShifts(RemoteOps.Shifts(plan, "station-1"), false);
            var text = File.ReadAllText(LoadLog.CeoLogPath(dir.FullName));

            Assert.Contains("building.rented ", rentLine);
            Assert.Equal("ba:street_seventhavenue", rented.Street);
            Assert.Equal(15, rented.Number);
            Assert.Equal("ba:businesstype_giftshop", laid.BusinessType);
            Assert.True(laid.Applied);
            Assert.Contains("layout.loaded ", layoutLine);
            Assert.Contains("street=" + rented.Street, layoutLine);
            Assert.Contains("number=15", layoutLine);
            Assert.Contains("type=ba:businesstype_giftshop", layoutLine);
            Assert.Equal("Cleaner", assigned.Cleaner);
            Assert.Equal("CleaningStationController.AssignEmployee", assigned.CleanerCall);
            Assert.True(plan.Applied);
            Assert.True(staff.CleanerStaffed);
            Assert.Contains("ops.assigned ", opsLine);
            Assert.Contains("street=" + rented.Street, opsLine);
            Assert.Contains("number=" + rented.Number.ToString(), opsLine);
            Assert.Contains("cleaner=Cleaner", opsLine);
            Assert.Contains("cleanerCall=" + assigned.CleanerCall, opsLine);
            Assert.Contains("uniforms=false", opsLine);
            Assert.DoesNotContain("guard=Guard", opsLine);
            Assert.True(text.IndexOf(layoutLine, StringComparison.Ordinal) < text.IndexOf(opsLine, StringComparison.Ordinal));
            Assert.Contains("ops.cleaner ", text);
            Assert.Contains("shift=true", text);
            Assert.DoesNotContain("not-inside", text);
            Assert.DoesNotContain("ops.missed", text);
            Assert.DoesNotContain("nostation", text);
        }
        finally
        {
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void Chain_open_rent_remote_layout_ops_remote_leaves_the_schedule_when_the_player_is_inside()
    {
        // Chain: open rent → remote layout → ops remote
        var dir = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "ceo-remote-ops-chain-inside-" + Guid.NewGuid().ToString("N")));
        try
        {
            var api = GameApi.Live();
            var rented = Rent.Choose(
                new[] { new RentLot("ba:street_seventhavenue", 15, 40m) },
                cash: 15000m,
                anyRented: false,
                api);
            var rentLine = Rent.Record(dir.FullName, rented, 1);
            var pick = Starter.Choose(Starter.StageOne(), alreadyOpen: false, api);
            var laid = RemoteLayout.Decide(entered: true, rented, "", pick);
            var layoutLine = RemoteLayout.Open(dir.FullName, rented, entered: true, "", pick, 1);
            var opsLine = RemoteOps.Open(
                dir.FullName,
                rented,
                entered: true,
                laid,
                pick,
                StoreFixtures.GiftShopWholesalers(),
                StoreOps.Opening(),
                1,
                "station-1");
            var text = File.ReadAllText(LoadLog.CeoLogPath(dir.FullName));

            Assert.Contains("street=" + rented.Street, rentLine);
            Assert.True(laid.Applied);
            Assert.Contains("type=ba:businesstype_giftshop", layoutLine);
            Assert.Equal("", opsLine);
            Assert.Contains(layoutLine, text);
            Assert.DoesNotContain("ops.assigned", text);
            Assert.DoesNotContain("ops.cleaner", text);
            Assert.DoesNotContain("ops.missed", text);
        }
        finally
        {
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void Chain_open_rent_remote_layout_ops_remote_when_the_prior_store_is_unstaffed()
    {
        // Chain: open rent → remote layout → fit-out ops while the prior store is unstaffed → ops remote
        var dir = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "ceo-remote-ops-unstaffed-" + Guid.NewGuid().ToString("N")));
        try
        {
            var api = GameApi.Live();
            var rented = Rent.Choose(
                new[] { new RentLot("ba:street_ninthavenue", 15, 33m) },
                cash: 15000m,
                anyRented: false,
                api);
            var rentLine = Rent.Record(dir.FullName, rented, 20);
            var pick = Starter.Choose(Starter.StageOne(), alreadyOpen: false, api);
            var laid = RemoteLayout.Decide(entered: false, rented, "ba:businesstype_empty", pick);
            var layoutLine = RemoteLayout.Open(
                dir.FullName,
                rented,
                entered: false,
                "ba:businesstype_empty",
                pick,
                20);
            var staff = StoreStaff.Sense(registerAssigned: true, cleanerAssigned: false, satisfaction: 80f);
            var prior = StoreHealth.Assess(
                new StoreFacts(
                    "ba:street_seventhavenue 15",
                    temporarilyClosed: false,
                    Wholesaler.GiftShopWholesalersStock().ToArray(),
                    new Dictionary<string, int>
                    {
                        ["ba:itemname_cheapgift"] = 50,
                        ["ba:itemname_cheapflower"] = 50,
                        ["ba:itemname_sodacan"] = 50,
                        ["ba:itemname_paperbag"] = 50,
                        ["ba:itemname_umbrella"] = 50,
                    },
                    staff.RegisterStaffed,
                    staff.CleanerStaffed,
                    staff.Satisfaction,
                    scheduledOpen: true,
                    dailyCustomers: 12,
                    dailyRevenue: 400m,
                    recentOpenDayProfits: new[] { 80m, 90m, 100m }));
            var ninth = rented.Street + " " + rented.Number.ToString();
            var progress = new FitOutProgress(laid.Applied, false, false, false, false);
            var fit = FitOut.Decide(new[] { prior }, ninth, progress);
            var opsLine = RemoteOps.Open(
                dir.FullName,
                rented,
                entered: false,
                laid,
                pick,
                StoreFixtures.GiftShopWholesalers(),
                StoreOps.Opening(),
                20,
                "station-1");
            var text = File.ReadAllText(LoadLog.CeoLogPath(dir.FullName));

            Assert.Contains("street=ba:street_ninthavenue", rentLine);
            Assert.True(laid.Applied);
            Assert.Contains("type=ba:businesstype_giftshop", layoutLine);
            Assert.False(prior.Staffed);
            Assert.False(prior.Ready);
            Assert.True(fit.Chosen);
            Assert.Equal("ops", fit.Step);
            Assert.Equal(ninth, fit.Place);
            Assert.Contains("ops.assigned ", opsLine);
            Assert.Contains("number=15", opsLine);
            Assert.Contains("cleaner=Cleaner", opsLine);
            Assert.Contains("ops.cleaner ", text);
            Assert.Contains("shift=true", text);
            Assert.DoesNotContain("ops.missed", text);
            Assert.True(text.IndexOf(layoutLine, StringComparison.Ordinal) < text.IndexOf(opsLine, StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void Chain_open_rent_remote_layout_ops_remote_stays_quiet_when_the_layout_does_not_apply()
    {
        // Chain: open rent → remote layout → ops remote
        var dir = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "ceo-remote-ops-chain-hold-" + Guid.NewGuid().ToString("N")));
        try
        {
            var api = GameApi.Live();
            var rented = Rent.Choose(
                new[] { new RentLot("ba:street_seventhavenue", 15, 40m) },
                cash: 15000m,
                anyRented: false,
                api);
            var rentLine = Rent.Record(dir.FullName, rented, 1);
            var pick = Starter.Choose(Starter.StageOne(), alreadyOpen: true, api);
            var laid = RemoteLayout.Decide(entered: false, rented, "", pick);
            var layoutLine = RemoteLayout.Open(dir.FullName, rented, entered: false, "", pick, 1);
            var opsLine = RemoteOps.Open(
                dir.FullName,
                rented,
                entered: false,
                laid,
                pick,
                StoreFixtures.GiftShopWholesalers(),
                StoreOps.Opening(),
                1,
                "station-1");
            var text = File.ReadAllText(LoadLog.CeoLogPath(dir.FullName));

            Assert.Contains("street=" + rented.Street, rentLine);
            Assert.False(pick.Loaded);
            Assert.False(laid.Applied);
            Assert.Equal("", layoutLine);
            Assert.Equal("", opsLine);
            Assert.DoesNotContain("layout.loaded", text);
            Assert.DoesNotContain("ops.assigned", text);
            Assert.DoesNotContain("ops.missed", text);
            Assert.DoesNotContain("ops.cleaner", text);
        }
        finally
        {
            Directory.Delete(dir.FullName, true);
        }
    }
}

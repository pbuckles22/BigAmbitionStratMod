using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class FitOutTests
{
    [Fact]
    public void StoreStaff_sense_locks_G3_from_register_and_cleaner_stations()
    {
        // Harvest: live G3 used aiEmployees.Count / no-employee warning and lied green without a cashier.
        Assert.False(StoreStaff.Sense(registerAssigned: false, cleanerAssigned: true, satisfaction: 80f).Staffed);
        Assert.False(StoreStaff.Sense(registerAssigned: true, cleanerAssigned: false, satisfaction: 80f).Staffed);
        Assert.False(StoreStaff.Sense(registerAssigned: true, cleanerAssigned: true, satisfaction: 49f).Staffed);
        Assert.False(StoreStaff.Sense(registerAssigned: false, cleanerAssigned: false, satisfaction: 80f).Staffed);
        Assert.False(StoreStaff.Sense(null).Staffed);

        var ok = StoreStaff.Sense(registerAssigned: true, cleanerAssigned: true, satisfaction: 50f);
        Assert.True(ok.Staffed);
        Assert.True(ok.RegisterStaffed);
        Assert.True(ok.CleanerStaffed);
        Assert.Equal(50f, ok.Satisfaction);
        Assert.Equal(StoreHealthGate.SatisfactionFloor, 50f);

        // Headcount alone is not enough: missing register still fails.
        Assert.False(
            StoreStaff.Sense(
                registerAssigned: false,
                cleanerAssigned: true,
                satisfaction: 80f,
                employeeHeadcount: 3).Staffed);
    }

    [Fact]
    public void StoreStaff_from_shifts_locks_G3_without_scene_stations()
    {
        // Harvest: day-24 hire.cashier + ops.cleaner logged, but HasCashier/HasCleaner needed live controllers → g3=false.
        var hired = new[]
        {
            new StaffShift("cashier-1", "register-item", StaffShift.Register),
            new StaffShift("cleaner-1", "clean-item", StaffShift.Cleaner),
        };
        var ok = StoreStaff.FromShifts(hired, warnedNoEmployee: false);
        Assert.True(ok.Staffed);
        Assert.True(ok.RegisterStaffed);
        Assert.True(ok.CleanerStaffed);
        Assert.Equal(100f, ok.Satisfaction);

        Assert.False(StoreStaff.FromShifts(
            new[] { new StaffShift("cleaner-1", "clean-item", StaffShift.Cleaner) },
            warnedNoEmployee: false).Staffed);
        Assert.False(StoreStaff.FromShifts(
            new[] { new StaffShift("cashier-1", "register-item", StaffShift.Register) },
            warnedNoEmployee: false).Staffed);
        Assert.False(StoreStaff.FromShifts(hired, warnedNoEmployee: true).Staffed);
        Assert.False(StoreStaff.FromShifts(
            new[]
            {
                new StaffShift("", "register-item", StaffShift.Register),
                new StaffShift("cleaner-1", "clean-item", StaffShift.Cleaner),
            },
            warnedNoEmployee: false).Staffed);
        Assert.False(StoreStaff.FromShifts(null, warnedNoEmployee: false).Staffed);
        Assert.False(StoreStaff.FromShifts(Array.Empty<StaffShift>(), warnedNoEmployee: false).Staffed);
    }

    [Fact]
    public void FitOut_decide_runs_Store_N_steps_only_when_owned_stores_are_ready()
    {
        var place = "ba:street_seventhavenue 42";
        var notReady = new[]
        {
            new StoreHealth(true, true, false, true, true, "ba:street_seventhavenue 30"),
        };
        var ready = new[] { StoreHealth.Healthy("ba:street_seventhavenue 30") };

        Assert.False(FitOut.Decide(notReady, place, bare: Bare()).Chosen);
        Assert.False(FitOut.Decide(null, place, bare: Bare()).Chosen);
        Assert.False(FitOut.Decide(Array.Empty<StoreHealth>(), place, bare: Bare()).Chosen);
        Assert.False(FitOut.Decide(ready, "", bare: Bare()).Chosen);
        Assert.False(FitOut.Decide(ready, place, bare: Fitted()).Chosen);

        Assert.Equal("layout", FitOut.Decide(ready, place, Bare()).Step);
        Assert.Equal("ops", FitOut.Decide(ready, place, Bare(layout: true)).Step);
        Assert.Equal("capacity", FitOut.Decide(ready, place, Bare(layout: true, ops: true)).Step);
        Assert.Equal("contract", FitOut.Decide(ready, place, Bare(layout: true, ops: true, capacity: true)).Step);
        Assert.Equal(
            "cashier",
            FitOut.Decide(ready, place, Bare(layout: true, ops: true, capacity: true, contract: true)).Step);
        Assert.Equal(place, FitOut.Decide(ready, place, Bare()).Place);
        Assert.Equal(3, StoreHealthGate.ProfitProofDays);
    }

    [Fact]
    public void FitOut_line_names_the_step_for_the_evidence_sink()
    {
        var dir = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "ceo-fitout-" + Guid.NewGuid().ToString("N")));
        try
        {
            var pick = FitOut.Decide(
                new[] { StoreHealth.Healthy("ba:street_seventhavenue 30") },
                "ba:street_seventhavenue 42",
                FitOutProgress.Bare());
            var line = FitOut.Line(pick, day: 12);
            var written = FitOut.Record(dir.FullName, pick, 12);

            Assert.Equal(
                "store.fitout version=" + LoadLog.Version
                    + " day=12 place=ba:street_seventhavenue 42 step=layout chosen=true",
                line);
            Assert.Equal(line, written);
            Assert.Contains(line, File.ReadAllText(LoadLog.CeoLogPath(dir.FullName)));
            Assert.Equal(
                "store.fitout version=" + LoadLog.Version + " day=12 place= step= chosen=false",
                FitOut.Line(FitOutPick.None(), 12));
        }
        finally
        {
            Directory.Delete(dir.FullName, recursive: true);
        }
    }

    [Fact]
    public void Chain_staff_sense_ready_profit_proof_into_Store_N_fit_out()
    {
        // Chain: station staff → G3 assess → ProfitProofDays ready → FitOut Store N
        var staff = StoreStaff.Sense(registerAssigned: true, cleanerAssigned: true, satisfaction: 80f);
        Assert.True(staff.Staffed);

        var facts = new StoreFacts(
            "ba:street_seventhavenue 30",
            temporarilyClosed: false,
            Wholesaler.GiftShopWholesalersStock().ToArray(),
            Cargo(
                ("ba:itemname_cheapgift", 50),
                ("ba:itemname_cheapflower", 50),
                ("ba:itemname_sodacan", 50),
                ("ba:itemname_paperbag", 50),
                ("ba:itemname_umbrella", 50)),
            staff.RegisterStaffed,
            staff.CleanerStaffed,
            staff.Satisfaction,
            scheduledOpen: true,
            dailyCustomers: 12,
            dailyRevenue: 400m,
            recentOpenDayProfits: new[] { 80m, 90m, 100m });
        Assert.Equal(StoreHealthGate.ProfitProofDays, facts.RecentOpenDayProfits.Count);

        var health = StoreHealth.Assess(facts);
        Assert.True(health.Staffed);
        Assert.True(health.Ready);
        Assert.False(StoreHeal.Decide(health).Chosen);
        Assert.True(StoreHealthGate.ExpansionAllowed(new[] { health }));

        var next = "ba:street_seventhavenue 42";
        var fit = FitOut.Decide(new[] { health }, next, FitOutProgress.Bare());
        Assert.True(fit.Chosen);
        Assert.Equal("layout", fit.Step);
        Assert.Equal(next, fit.Place);

        var shortProof = StoreHealth.Assess(
            new StoreFacts(
                facts.Place,
                facts.TemporarilyClosed,
                facts.NeededItems,
                facts.CargoOnHand,
                staff.RegisterStaffed,
                staff.CleanerStaffed,
                staff.Satisfaction,
                facts.ScheduledOpen,
                facts.DailyCustomers,
                facts.DailyRevenue,
                recentOpenDayProfits: new[] { 10m, -20m, 5m }));
        Assert.False(shortProof.Ready);
        Assert.False(FitOut.Decide(new[] { shortProof }, next, FitOutProgress.Bare()).Chosen);

        var unstaffed = StoreStaff.Sense(registerAssigned: false, cleanerAssigned: true, satisfaction: 80f);
        var g3Fail = StoreHealth.Assess(
            new StoreFacts(
                facts.Place,
                facts.TemporarilyClosed,
                facts.NeededItems,
                facts.CargoOnHand,
                unstaffed.RegisterStaffed,
                unstaffed.CleanerStaffed,
                unstaffed.Satisfaction,
                facts.ScheduledOpen,
                facts.DailyCustomers,
                facts.DailyRevenue,
                facts.RecentOpenDayProfits));
        Assert.False(g3Fail.Staffed);
        Assert.Equal("restaff", StoreHeal.Decide(g3Fail).Action);
        Assert.False(FitOut.Decide(new[] { g3Fail }, next, FitOutProgress.Bare()).Chosen);
    }

    [Fact]
    public void FitOut_an_open_address_still_gets_ops_when_the_prior_store_is_unstaffed()
    {
        // Harvest: 1.19.2.1 morning. Seventh cleaners=0, Ninth already named, TryFitOut wrote nothing.
        var prior = new StoreHealth(true, true, false, true, false, "ba:street_seventhavenue 15");
        var ninth = "ba:street_ninthavenue 15";
        var open = new FitOutProgress(true, false, true, true, false);
        var pick = FitOut.Decide(new[] { prior }, ninth, open);

        Assert.False(prior.Ready);
        Assert.True(pick.Chosen);
        Assert.Equal("ops", pick.Step);
        Assert.Equal(ninth, pick.Place);
        Assert.Equal("", pick.Gate);
        Assert.False(FitOut.Decide(new[] { prior }, ninth, FitOutProgress.Bare()).Chosen);
        var capacity = FitOut.Decide(
            new[] { prior },
            ninth,
            new FitOutProgress(true, true, false, true, false));
        Assert.True(capacity.Chosen);
        Assert.Equal("capacity", capacity.Step);
    }

    [Fact]
    public void FitOut_a_store_that_still_needs_a_cleaner_is_chosen_ahead_of_a_cashier_gap()
    {
        // Harvest: cashier is scene-only, so an outside morning must not stay stuck on Seventh and skip Ninth.
        var seventhCashier = new FitOutSite(
            "ba:street_seventhavenue",
            15,
            false,
            new FitOutProgress(true, true, true, true, false));
        var ninthOps = new FitOutSite(
            "ba:street_ninthavenue",
            15,
            false,
            new FitOutProgress(true, false, false, false, false));
        var seventhAlso = new FitOutSite(
            "ba:street_seventhavenue",
            15,
            false,
            new FitOutProgress(true, false, true, true, false));

        Assert.Equal(
            "ba:street_ninthavenue 15",
            FitOut.PlaceNeedingOps(new[] { seventhCashier, ninthOps }));
        Assert.Equal(
            "ba:street_seventhavenue 15",
            FitOut.PlaceNeedingOps(new[] { seventhAlso, ninthOps }));
        Assert.Equal("", FitOut.PlaceNeedingOps(new[] { seventhCashier }));
        Assert.Equal("", FitOut.PlaceNeedingOps(null));
    }

    private static FitOutProgress Bare(
        bool layout = false,
        bool ops = false,
        bool capacity = false,
        bool contract = false,
        bool cashier = false)
    {
        return new FitOutProgress(layout, ops, capacity, contract, cashier);
    }

    private static FitOutProgress Fitted()
    {
        return new FitOutProgress(true, true, true, true, true);
    }

    private static IReadOnlyDictionary<string, int> Cargo(params (string Item, int Count)[] rows)
    {
        var map = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            map[row.Item] = row.Count;
        }

        return map;
    }
}

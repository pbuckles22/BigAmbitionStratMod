using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class StoreOpsTests
{
    [Fact]
    public void StoreOps_names_the_cleaner_and_the_guard()
    {
        var api = GameApi.Live();
        var layout = Starter.Choose(Starter.StageOne(), alreadyOpen: false, api);
        var assigned = StoreOps.Assign(
            layout,
            new StoreOpsFacts(dirtSpots: 1, sales: 100m, theft: 2m, uniformsDemanded: true, canMeetUniforms: true),
            api);
        var opening = StoreOps.Assign(layout, StoreOps.Opening(), api);
        var rent = new BuildingRent("ba:street_seventhavenue", 20, true, api.Call("rent"));
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "ceo-ops-" + Guid.NewGuid().ToString("N")));
        try
        {
            var written = StoreOps.Record(dir.FullName, assigned, rent, 1);
            var again = StoreOps.Record(dir.FullName, assigned, rent, 1);

            Assert.True(layout.Loaded);
            Assert.True(assigned.Assigned);
            Assert.Equal("Cleaner", assigned.Cleaner);
            Assert.Equal("Guard", assigned.Guard);
            Assert.True(assigned.Uniforms);
            Assert.Equal("CleaningStationController.AssignEmployee", assigned.CleanerCall);
            Assert.Equal("Controllers.SecurityGuardLockerController.AssignEmployee", assigned.GuardCall);
            Assert.Equal("BuildingRegistration.uniformsBySkill", assigned.UniformsCall);
            Assert.Equal(assigned.CleanerCall, api.Call("cleaner"));
            Assert.Equal(assigned.GuardCall, api.Call("guard"));
            Assert.Equal(assigned.UniformsCall, api.Call("uniforms"));
            Assert.Equal("Cleaner", opening.Cleaner);
            Assert.Equal("Guard", opening.Guard);
            Assert.False(opening.Uniforms);
            Assert.Equal("", opening.UniformsCall);
            Assert.Equal(assigned.CleanerCall, opening.CleanerCall);
            Assert.Equal(
                "ops.assigned version=" + LoadLog.Version
                    + " day=1 street=ba:street_seventhavenue number=20 cleaner=Cleaner guard=Guard uniforms=true"
                    + " cleanerCall=" + assigned.CleanerCall
                    + " guardCall=" + assigned.GuardCall
                    + " uniformsCall=" + assigned.UniformsCall,
                written);
            Assert.Equal(written, again);
            Assert.Equal(1, File.ReadAllLines(LoadLog.CeoLogPath(dir.FullName)).Count(line => line.StartsWith("ops.assigned ")));
        }
        finally
        {
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void StoreOps_an_exact_theft_line_keeps_the_guard_off_and_a_closed_layout_keeps_every_name_blank()
    {
        var api = GameApi.Live();
        var layout = Starter.Choose(Starter.StageOne(), alreadyOpen: false, api);
        var clean = StoreOps.Assign(
            layout,
            new StoreOpsFacts(dirtSpots: 0, sales: 100m, theft: 2m, uniformsDemanded: true, canMeetUniforms: true),
            api);
        var line = StoreOps.Assign(
            layout,
            new StoreOpsFacts(dirtSpots: 2, sales: 100m, theft: 1m, uniformsDemanded: false, canMeetUniforms: true),
            api);
        var unmet = StoreOps.Assign(
            layout,
            new StoreOpsFacts(dirtSpots: 2, sales: 100m, theft: 2m, uniformsDemanded: true, canMeetUniforms: false),
            api);
        var noSales = StoreOps.Assign(
            layout,
            new StoreOpsFacts(dirtSpots: 1, sales: 0m, theft: 5m, uniformsDemanded: true, canMeetUniforms: true),
            api);
        var closed = StoreOps.Assign(
            Starter.Choose(Starter.StageOne(), alreadyOpen: true, api),
            StoreOps.Opening(),
            api);
        var noCall = StoreOps.Assign(layout, StoreOps.Opening(), api: null);
        var guardOnly = new ApiNote(
            "3682",
            "",
            "",
            "",
            "",
            "",
            new Dictionary<string, string>
            {
                ["guard"] = "Controllers.SecurityGuardLockerController.AssignEmployee",
                ["uniforms"] = "BuildingRegistration.uniformsBySkill",
            });
        var missingCleaner = StoreOps.Assign(
            layout,
            new StoreOpsFacts(dirtSpots: 3, sales: 100m, theft: 2m, uniformsDemanded: true, canMeetUniforms: true),
            guardOnly);
        var rent = new BuildingRent("ba:street_seventhavenue", 20, true, api.Call("rent"));
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "ceo-ops-" + Guid.NewGuid().ToString("N")));
        try
        {
            var written = StoreOps.Record(dir.FullName, line, rent, 1);
            var skipped = StoreOps.Record(dir.FullName, closed, rent, 1);

            Assert.Equal("", clean.Cleaner);
            Assert.Equal("", clean.CleanerCall);
            Assert.Equal("Guard", clean.Guard);
            Assert.True(clean.Uniforms);
            Assert.Equal("Cleaner", line.Cleaner);
            Assert.Equal("", line.Guard);
            Assert.Equal("", line.GuardCall);
            Assert.False(line.Uniforms);
            Assert.Equal("", line.UniformsCall);
            Assert.True(line.Assigned);
            Assert.Equal("Cleaner", unmet.Cleaner);
            Assert.Equal("Guard", unmet.Guard);
            Assert.False(unmet.Uniforms);
            Assert.Equal("Cleaner", noSales.Cleaner);
            Assert.Equal("", noSales.Guard);
            Assert.False(closed.Assigned);
            Assert.Equal("", closed.Cleaner);
            Assert.Equal("", closed.Guard);
            Assert.False(closed.Uniforms);
            Assert.Equal("", closed.CleanerCall);
            Assert.Equal("", closed.GuardCall);
            Assert.Equal("", closed.UniformsCall);
            Assert.False(noCall.Assigned);
            Assert.Equal("", noCall.Cleaner);
            Assert.Equal("", noCall.Guard);
            Assert.Equal("", missingCleaner.Cleaner);
            Assert.Equal("", missingCleaner.CleanerCall);
            Assert.Equal("Guard", missingCleaner.Guard);
            Assert.True(missingCleaner.Uniforms);
            Assert.Contains("cleaner=Cleaner guard= uniforms=false", written);
            Assert.DoesNotContain("guard=Guard", written);
            Assert.Equal("", skipped);
            Assert.DoesNotContain(
                File.ReadAllLines(LoadLog.CeoLogPath(dir.FullName)),
                row => row.StartsWith("ops.assigned ") && row.Contains("guard=Guard", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void StoreOps_a_negative_dirt_count_keeps_the_cleaner_off_and_a_cent_over_the_line_names_the_guard()
    {
        var api = GameApi.Live();
        var layout = Starter.Choose(Starter.StageOne(), alreadyOpen: false, api);
        var negative = StoreOps.Assign(
            layout,
            new StoreOpsFacts(dirtSpots: -1, sales: 100m, theft: 2m, uniformsDemanded: true, canMeetUniforms: true),
            api);
        var over = StoreOps.Assign(
            layout,
            new StoreOpsFacts(dirtSpots: 0, sales: 100m, theft: 1.01m, uniformsDemanded: false, canMeetUniforms: true),
            api);
        var noUniformsCall = new ApiNote(
            "3682",
            "",
            "",
            "",
            "",
            "",
            new Dictionary<string, string>
            {
                ["cleaner"] = "CleaningStationController.AssignEmployee",
                ["guard"] = "Controllers.SecurityGuardLockerController.AssignEmployee",
            });
        var blankUniforms = StoreOps.Assign(layout, StoreOps.Opening(), noUniformsCall);

        Assert.Equal("", negative.Cleaner);
        Assert.Equal("Guard", negative.Guard);
        Assert.True(negative.Uniforms);
        Assert.Equal("", over.Cleaner);
        Assert.Equal("Guard", over.Guard);
        Assert.False(over.Uniforms);
        Assert.Equal("Cleaner", blankUniforms.Cleaner);
        Assert.Equal("Guard", blankUniforms.Guard);
        Assert.False(blankUniforms.Uniforms);
        Assert.Equal("", blankUniforms.UniformsCall);
    }

    [Fact]
    public void StoreOps_a_gift_shop_without_a_guard_locker_still_names_the_cleaner()
    {
        var api = GameApi.Live();
        var layout = Starter.Choose(Starter.StageOne(), alreadyOpen: false, api);
        var assigned = StoreOps.Assign(layout, StoreOps.Opening(), StoreFixtures.GiftShopWholesalers(), api);
        var bare = StoreOps.Assign(layout, StoreOps.Opening(), new StoreFixtures(false, false, false), api);
        var rent = new BuildingRent("ba:street_seventhavenue", 15, true, api.Call("rent"));
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "ceo-ops-" + Guid.NewGuid().ToString("N")));
        try
        {
            var written = StoreOps.Record(dir.FullName, assigned, rent, 1);

            Assert.True(assigned.Assigned);
            Assert.Equal("Cleaner", assigned.Cleaner);
            Assert.Equal("", assigned.Guard);
            Assert.Equal("", assigned.GuardCall);
            Assert.False(assigned.Uniforms);
            Assert.Equal("CleaningStationController.AssignEmployee", assigned.CleanerCall);
            Assert.Equal("", assigned.UniformsCall);
            Assert.False(bare.Assigned);
            Assert.Equal("", bare.Cleaner);
            Assert.Equal("", bare.Guard);
            Assert.False(bare.Uniforms);
            Assert.Contains("cleaner=Cleaner guard= uniforms=false", written);
            Assert.DoesNotContain("guard=Guard", written);
        }
        finally
        {
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void StoreOps_a_hire_without_a_person_spawns_one_instead_of_missing()
    {
        var api = GameApi.Live();
        var layout = Starter.Choose(Starter.StageOne(), alreadyOpen: false, api);
        var assigned = StoreOps.Assign(layout, StoreOps.Opening(), StoreFixtures.GiftShopWholesalers(), api);
        var source = StoreOps.BodySource(assigned, stationHasPerson: false, recordHasPerson: false);
        var stationed = StoreOps.BodySource(assigned, stationHasPerson: true, recordHasPerson: true);
        var recorded = StoreOps.BodySource(assigned, stationHasPerson: false, recordHasPerson: true);
        var uniforms = StoreOps.Assign(
            layout,
            new StoreOpsFacts(dirtSpots: 0, sales: 0m, theft: 0m, uniformsDemanded: true, canMeetUniforms: true),
            StoreFixtures.GiftShopWholesalers(),
            api);

        Assert.Equal("Characters/HumanDefinitionLow", StoreOps.SpawnPrefab);
        Assert.Equal("spawn", source);
        Assert.Equal("station", stationed);
        Assert.Equal("record", recorded);
        Assert.Equal("assigned", StoreOps.Outcome(assigned, source));
        Assert.Equal("missed", StoreOps.Outcome(assigned, ""));
        Assert.Equal("assigned", StoreOps.Outcome(uniforms, ""));
        Assert.Equal("", StoreOps.BodySource(uniforms, stationHasPerson: false, recordHasPerson: false));
        var guard = StoreOps.Assign(
            layout,
            new StoreOpsFacts(dirtSpots: 0, sales: 100m, theft: 2m, uniformsDemanded: false, canMeetUniforms: true),
            api);
        Assert.Equal("Guard", guard.Guard);
        Assert.Equal("", guard.Cleaner);
        Assert.Equal("spawn", StoreOps.BodySource(guard, stationHasPerson: false, recordHasPerson: false));
        Assert.Equal("assigned", StoreOps.Outcome(guard, "spawn"));
    }

    [Fact]
    public void StoreOps_an_unstarted_station_uses_the_cleaner_type_and_a_started_role_stays_blank()
    {
        var api = GameApi.Live();
        var layout = Starter.Choose(Starter.StageOne(), alreadyOpen: false, api);
        var assigned = StoreOps.Assign(layout, StoreOps.Opening(), StoreFixtures.GiftShopWholesalers(), api);

        Assert.Equal("Cleaner", assigned.Cleaner);
        Assert.Equal("", assigned.Guard);
        Assert.Equal("CleanerEmployee", StoreOps.UnstartedType(assigned.Cleaner));
        Assert.Equal("SecurityGuardEmployee", StoreOps.UnstartedType("Guard"));
        Assert.Equal("", StoreOps.UnstartedType(assigned.Guard));
        Assert.Equal("", StoreOps.UnstartedType(""));
        Assert.Equal("", StoreOps.UnstartedType(null));
    }

    [Fact]
    public void StoreOps_an_unstarted_station_waits_and_an_exception_does_not()
    {
        Assert.True(StoreOps.Retry("notstarted"));
        Assert.True(StoreOps.Retry("nostation"));
        Assert.True(StoreOps.Retry("nouniform"));
        Assert.True(StoreOps.Retry("nocharacter"));
        Assert.False(StoreOps.Retry("exception"));
        Assert.False(StoreOps.Retry("noemployee"));
        Assert.False(StoreOps.Retry(""));
        Assert.False(StoreOps.Retry(null));
    }

    [Fact]
    public void StoreOps_verify_runs_once_after_the_wait_and_not_before()
    {
        var watch = new StoreOpsWatch();
        Assert.False(watch.VerifyDue());

        watch.Arm("ba:street_seventhavenue", 26, 1, 3);
        Assert.False(watch.VerifyDue());
        Assert.False(watch.VerifyDue());
        Assert.True(watch.VerifyDue());
        Assert.Equal("ba:street_seventhavenue", watch.VerifyStreet);
        Assert.Equal(26, watch.VerifyNumber);
        Assert.Equal(1, watch.VerifyDay);
        Assert.False(watch.VerifyDue());
        Assert.False(watch.VerifyDue());
        Assert.Equal(StoreOps.VerifyFrames, 300);
    }

    [Fact]
    public void StoreOps_a_retry_after_the_cleaner_is_assigned_keeps_it_and_never_spawns_a_second()
    {
        var pick = new StoreOpsPick(true, "Cleaner", "", true, "c", "", "u");

        Assert.Equal("kept", StoreOps.BodySource(pick, stationHasPerson: false, recordHasPerson: false, stationAssigned: true));
        Assert.Equal("kept", StoreOps.BodySource(pick, stationHasPerson: true, recordHasPerson: true, stationAssigned: true));
        Assert.Equal("spawn", StoreOps.BodySource(pick, stationHasPerson: false, recordHasPerson: false, stationAssigned: false));
        Assert.Equal("assigned", StoreOps.Outcome(pick, "kept"));
    }

    [Fact]
    public void StoreOps_a_placed_cleaner_logs_who_where_and_whether_it_held()
    {
        var rent = new BuildingRent("ba:street_seventhavenue", 20, true, "");
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "ceo-ops-" + Guid.NewGuid().ToString("N")));
        try
        {
            var placed = StoreOps.CleanerLine(
                dir.FullName,
                rent,
                1,
                new StoreOpsCleaner("emp1", "Jo Ann Lee", "item7", shift: true, person: true, type: "CleanerEmployee"));
            var again = StoreOps.CleanerLine(
                dir.FullName,
                rent,
                1,
                new StoreOpsCleaner("emp1", "Jo Ann Lee", "item7", shift: true, person: true, type: "CleanerEmployee"));
            var lost = StoreOps.CleanerLine(
                dir.FullName,
                rent,
                1,
                new StoreOpsCleaner("emp1", "", "item7", shift: false, person: true, type: "Employee"));

            Assert.Equal(
                "ops.cleaner version=" + LoadLog.Version
                    + " day=1 street=ba:street_seventhavenue number=20 employee=emp1 name=Jo_Ann_Lee station=item7 shift=true person=true type=CleanerEmployee placed=true",
                placed);
            Assert.Equal(placed, again);
            Assert.EndsWith(" name= station=item7 shift=false person=true type=Employee placed=false", lost);
            Assert.Equal(2, File.ReadAllLines(LoadLog.CeoLogPath(dir.FullName)).Count(line => line.StartsWith("ops.cleaner ")));
            Assert.Equal("", StoreOps.CleanerLine(dir.FullName, rent, 1, null));
        }
        finally
        {
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void StoreOps_a_uniform_failure_names_the_missing_piece_in_order()
    {
        Assert.Equal("nolocker", StoreOps.UniformReason(locker: false, saveReady: true, presetId: "p1", dressed: true));
        Assert.Equal("nosave", StoreOps.UniformReason(locker: true, saveReady: false, presetId: "p1", dressed: true));
        Assert.Equal("nopreset", StoreOps.UniformReason(locker: true, saveReady: true, presetId: "", dressed: false));
        Assert.Equal("noskill", StoreOps.UniformReason(locker: true, saveReady: true, presetId: "p1", dressed: false));
        Assert.Equal("", StoreOps.UniformReason(locker: true, saveReady: true, presetId: "p1", dressed: true));
        Assert.True(StoreOps.Retry("nolocker"));
        Assert.False(StoreOps.Retry("nopreset"));
        Assert.False(StoreOps.Retry("noskill"));
    }

    [Fact]
    public void StoreOps_a_new_save_uses_its_first_saved_preset_and_an_empty_save_uses_the_business_default()
    {
        Assert.Equal("saved1", StoreOps.UniformPreset(new[] { "saved1", "saved2" }, "giftdefault"));
        Assert.Equal("giftdefault", StoreOps.UniformPreset(new string[0], "giftdefault"));
        Assert.Equal("giftdefault", StoreOps.UniformPreset(new[] { "", null! }, "giftdefault"));
        Assert.Equal("", StoreOps.UniformPreset(null, null));
    }

    [Fact]
    public void StoreOps_uniforms_cover_the_cleaner_and_the_business_skills_once_each()
    {
        Assert.Equal(
            new[] { "ba:skill_cleaning", "ba:skill_customerservice" },
            StoreOps.UniformSkills(
                new[] { "ba:skill_customerservice" },
                new[] { "ba:skill_cleaning", "" },
                "ba:skill_cleaning"));
        Assert.Equal(new[] { "ba:skill_cleaning" }, StoreOps.UniformSkills(null, null, "ba:skill_cleaning"));
    }

    [Fact]
    public void StoreOps_seeding_fills_empty_skills_and_keeps_a_player_choice()
    {
        var bySkill = new Dictionary<string, string> { ["ba:skill_customerservice"] = "mine" };

        var seeded = StoreOps.SeedUniforms(bySkill, new[] { "ba:skill_cleaning", "ba:skill_customerservice" }, "default");

        Assert.Equal(new[] { "ba:skill_cleaning" }, seeded);
        Assert.Equal("default", bySkill["ba:skill_cleaning"]);
        Assert.Equal("mine", bySkill["ba:skill_customerservice"]);
        Assert.Empty(StoreOps.SeedUniforms(bySkill, new[] { "ba:skill_cleaning" }, "default"));
        Assert.Empty(StoreOps.SeedUniforms(new Dictionary<string, string>(), new[] { "ba:skill_cleaning" }, ""));
        Assert.True(StoreOps.Dressed(bySkill, "ba:skill_cleaning"));
        Assert.False(StoreOps.Dressed(new Dictionary<string, string>(), "ba:skill_cleaning"));
    }

    [Fact]
    public void StoreOps_a_cleaner_on_shift_and_in_the_room_is_kept_and_a_destroyed_one_is_not()
    {
        var api = GameApi.Live();
        var layout = Starter.Choose(Starter.StageOne(), alreadyOpen: false, api);
        var assigned = StoreOps.Assign(layout, StoreOps.Opening(), StoreFixtures.GiftShopWholesalers(), api);
        var uniformsOnly = StoreOps.Assign(
            layout,
            new StoreOpsFacts(dirtSpots: 0, sales: 0m, theft: 0m, uniformsDemanded: true, canMeetUniforms: true),
            StoreFixtures.GiftShopWholesalers(),
            api);
        var rent = new BuildingRent("ba:street_seventhavenue", 26, true, api.Call("rent"));
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "ceo-ops-" + Guid.NewGuid().ToString("N")));
        try
        {
            Assert.True(StoreOps.Kept(assigned, shift: true, person: true, type: "CleanerEmployee", uniform: "p1"));
            Assert.False(StoreOps.Kept(assigned, shift: true, person: false, type: "", uniform: "p1"));
            Assert.False(StoreOps.Kept(assigned, shift: false, person: true, type: "CleanerEmployee", uniform: "p1"));
            Assert.False(StoreOps.Kept(assigned, shift: true, person: true, type: "Employee", uniform: "p1"));
            Assert.True(StoreOps.Kept(assigned, shift: true, person: true, type: "CleanerEmployee", uniform: ""));
            Assert.True(StoreOps.Kept(uniformsOnly, shift: false, person: false, type: "", uniform: "p1"));
            Assert.False(StoreOps.Kept(uniformsOnly, shift: false, person: false, type: "", uniform: ""));
            Assert.False(StoreOps.Kept(null, shift: true, person: true, type: "CleanerEmployee", uniform: "p1"));

            var kept = StoreOps.Verify(dir.FullName, assigned, rent, 1, shift: true, person: true, type: "CleanerEmployee", uniform: "p1");
            var lost = StoreOps.Verify(dir.FullName, assigned, rent, 1, shift: true, person: false, type: "", uniform: "");

            Assert.Equal(
                "ops.verified version=" + LoadLog.Version
                    + " day=1 street=ba:street_seventhavenue number=26 cleaner=Cleaner shift=true person=true type=CleanerEmployee uniform=p1 kept=true",
                kept);
            Assert.Equal(
                "ops.verified version=" + LoadLog.Version
                    + " day=1 street=ba:street_seventhavenue number=26 cleaner=Cleaner shift=true person=false type= uniform=nopreset kept=false",
                lost);
            Assert.Equal(2, File.ReadAllLines(LoadLog.CeoLogPath(dir.FullName)).Count(line => line.StartsWith("ops.verified ")));
            Assert.Equal("", StoreOps.Verify(dir.FullName, assigned, null, 1, true, true, "CleanerEmployee", "p1"));
        }
        finally
        {
            Directory.Delete(dir.FullName, true);
        }
    }
}

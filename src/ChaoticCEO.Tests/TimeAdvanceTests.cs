using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class TimeAdvanceTests
{
    [Fact]
    public void TimeAdvance_need_floor_is_twenty_and_locks_the_day_span()
    {
        Assert.Equal(20f, TimeAdvance.NeedFloor);
        Assert.Equal("1d", TimeAdvance.DaySpan);
        Assert.Equal(7, TimeAdvance.WeekDays);
    }

    [Fact]
    public void TimeAdvance_a_need_at_or_below_the_floor_is_due_and_one_above_is_not()
    {
        Assert.True(TimeAdvance.BelowFloor(new PlayerNeeds(20f, 100f, 100f, true, true)));
        Assert.True(TimeAdvance.BelowFloor(new PlayerNeeds(100f, 19.9f, 100f, true, true)));
        Assert.True(TimeAdvance.BelowFloor(new PlayerNeeds(100f, 100f, 20f, true, true)));
        Assert.False(TimeAdvance.BelowFloor(new PlayerNeeds(20.01f, 20.01f, 20.01f, true, true)));
        Assert.False(TimeAdvance.BelowFloor(Needs.Hold()));
    }

    [Fact]
    public void TimeAdvance_cares_with_sleep_when_energy_hits_the_floor()
    {
        var api = GameApi.Live();
        var care = TimeAdvance.Care(new PlayerNeeds(20f, 80f, 80f, true, true), api);

        Assert.True(care.Due);
        Assert.True(care.Slept);
        Assert.False(care.Ate);
        Assert.Equal("BedController.PerformActivity", care.SleepCall);
        Assert.Equal(care.SleepCall, api.Call("sleep"));
        Assert.Equal("", care.FoodCall);
    }

    [Fact]
    public void TimeAdvance_cares_with_food_when_hunger_hits_the_floor()
    {
        var api = GameApi.Live();
        var care = TimeAdvance.Care(new PlayerNeeds(80f, 20f, 80f, true, true), api);

        Assert.True(care.Due);
        Assert.False(care.Slept);
        Assert.True(care.Ate);
        Assert.Equal("ItemHelper.TryToConsume", care.FoodCall);
        Assert.Equal(care.FoodCall, api.Call("food"));
        Assert.Equal("", care.SleepCall);
    }

    [Fact]
    public void TimeAdvance_happiness_at_the_floor_sleeps_and_eats()
    {
        var api = GameApi.Live();
        var care = TimeAdvance.Care(new PlayerNeeds(80f, 80f, 20f, true, true), api);

        Assert.True(care.Due);
        Assert.True(care.Slept);
        Assert.True(care.Ate);
        Assert.Equal("BedController.PerformActivity", care.SleepCall);
        Assert.Equal("ItemHelper.TryToConsume", care.FoodCall);
    }

    [Fact]
    public void TimeAdvance_energy_and_hunger_both_low_sleeps_and_eats()
    {
        var api = GameApi.Live();
        var care = TimeAdvance.Care(new PlayerNeeds(10f, 5f, 90f, true, true), api);

        Assert.True(care.Due);
        Assert.True(care.Slept);
        Assert.True(care.Ate);
        Assert.Equal(api.Call("sleep"), care.SleepCall);
        Assert.Equal(api.Call("food"), care.FoodCall);
    }

    [Fact]
    public void TimeAdvance_skips_care_when_every_need_is_above_the_floor()
    {
        var api = GameApi.Live();
        var care = TimeAdvance.Care(new PlayerNeeds(20.01f, 50f, 50f, true, true), api);
        var noCall = TimeAdvance.Care(new PlayerNeeds(10f, 10f, 10f, true, true), api: null);

        Assert.False(care.Due);
        Assert.False(care.Slept);
        Assert.False(care.Ate);
        Assert.Equal("", care.SleepCall);
        Assert.Equal("", care.FoodCall);
        Assert.False(noCall.Due);
    }

    [Fact]
    public void TimeAdvance_advances_one_day_when_needs_are_safe()
    {
        var api = GameApi.Live();
        var advanced = TimeAdvance.Skip(Needs.Hold(), alreadyAdvanced: false, api);
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "ceo-time-" + Guid.NewGuid().ToString("N")));
        try
        {
            var written = TimeAdvance.RecordSkip(dir.FullName, advanced, 1);
            var again = TimeAdvance.RecordSkip(dir.FullName, advanced, 1);

            Assert.True(advanced.Advanced);
            Assert.Equal(TimeAdvance.DaySpan, advanced.Span);
            Assert.Equal("Timemachine.TimeMachine.SkipTime", advanced.Call);
            Assert.Equal(advanced.Call, api.Call("time advance"));
            Assert.Equal(
                "time.advanced version=" + LoadLog.Version
                    + " day=1 span=1d call=" + advanced.Call,
                written);
            Assert.Equal(written, again);
            Assert.Equal(1, File.ReadAllLines(LoadLog.CeoLogPath(dir.FullName)).Count(line => line.StartsWith("time.advanced ")));
        }
        finally
        {
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void TimeAdvance_records_sleep_and_food_care()
    {
        var api = GameApi.Live();
        var care = TimeAdvance.Care(new PlayerNeeds(10f, 5f, 90f, true, true), api);
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "ceo-time-" + Guid.NewGuid().ToString("N")));
        try
        {
            var written = TimeAdvance.RecordCare(dir.FullName, care, 1);
            var again = TimeAdvance.RecordCare(dir.FullName, care, 1);

            Assert.Equal(
                "time.cared version=" + LoadLog.Version
                    + " day=1 sleep=true food=true sleepCall=" + care.SleepCall
                    + " foodCall=" + care.FoodCall,
                written);
            Assert.Equal(written, again);
            Assert.Equal(1, File.ReadAllLines(LoadLog.CeoLogPath(dir.FullName)).Count(line => line.StartsWith("time.cared ")));
        }
        finally
        {
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void TimeAdvance_refuses_a_skip_at_the_floor_and_a_second_skip()
    {
        var api = GameApi.Live();
        var blocked = TimeAdvance.Skip(new PlayerNeeds(20f, 100f, 100f, true, true), alreadyAdvanced: false, api);
        var again = TimeAdvance.Skip(Needs.Hold(), alreadyAdvanced: true, api);
        var noCall = TimeAdvance.Skip(Needs.Hold(), alreadyAdvanced: false, api: null);

        Assert.False(blocked.Advanced);
        Assert.Equal("", blocked.Call);
        Assert.False(again.Advanced);
        Assert.False(noCall.Advanced);
    }

    [Fact]
    public void TimeAdvance_watch_stays_due_past_week_days_for_hands_off_soak()
    {
        // Harvest: WeekDays=7 stopped TryTime after day 8; soak cannot force-skip forever.
        var watch = new TimeAdvanceWatch();
        var save = new object();
        for (var i = 0; i < TimeAdvance.WeekDays + 3; i++)
        {
            Assert.True(watch.Due(save));
            watch.WaitFor(i + 1);
            Assert.True(watch.Arrive(i + 1));
        }

        Assert.True(watch.Due(save));
        Assert.True(watch.Completed > TimeAdvance.WeekDays);
    }

    [Fact]
    public void TimeAdvance_watch_keeps_skipping_after_week_and_ignores_finish_mark()
    {
        // Harvest: TryTime Mark after WeekDays made Finished=true; soak fell back to slow game speed.
        var watch = new TimeAdvanceWatch();
        var save = new object();
        for (var i = 0; i < TimeAdvance.WeekDays; i++)
        {
            watch.WaitFor(i + 1);
            Assert.True(watch.Arrive(i + 1));
        }

        Assert.Equal(TimeAdvance.WeekDays, watch.Completed);
        Assert.True(TimeAdvance.KeepSkipping(watch, save));
        Assert.False(watch.Finished(save));

        watch.Mark(save);
        Assert.True(watch.Finished(save));
        Assert.True(TimeAdvance.KeepSkipping(watch, save));
        Assert.True(watch.Due(save));
    }

    [Fact]
    public void TimeAdvance_ClearWait_unblocks_stuck_skip_after_interrupted_SkipTime()
    {
        // Harvest: opening F9 / looking at stats mid-SkipTime left Waiting(day) true forever.
        var watch = new TimeAdvanceWatch();
        watch.WaitFor(12);
        Assert.True(watch.Waiting(11));
        watch.ClearWait();
        Assert.False(watch.Waiting(11));
        Assert.False(watch.Waiting(12));
    }

    [Fact]
    public void TimeAdvance_soak_boosts_game_speed_before_SkipTime()
    {
        // Harvest: SkipTime(span, useConstantSpeed:true) stalled soak; boost FastForward instead.
        Assert.True(TimeAdvance.SoakBoostsGameSpeed);
        Assert.Equal("1d", TimeAdvance.DaySpan);
    }

    [Fact]
    public void BankLoan_SoakUnlocked_accepts_save_loan_without_log_line()
    {
        // Harvest: loaded save with Loans.Count>0 but StartSession wiped loan.taken — Resume did nothing.
        Assert.True(BankLoan.SoakUnlocked(saveHasLoan: true, logRecorded: false));
        Assert.True(BankLoan.SoakUnlocked(saveHasLoan: false, logRecorded: true));
        Assert.False(BankLoan.SoakUnlocked(saveHasLoan: false, logRecorded: false));
    }

    [Fact]
    public void TimeAdvance_a_week_is_seven_safe_day_skips()
    {
        var api = GameApi.Live();
        var days = 0;
        for (var i = 0; i < TimeAdvance.WeekDays; i++)
        {
            var needs = Needs.Hold();
            Assert.False(TimeAdvance.BelowFloor(needs));
            var care = TimeAdvance.Care(needs, api);
            Assert.False(care.Due);
            var advanced = TimeAdvance.Skip(needs, alreadyAdvanced: false, api);
            Assert.True(advanced.Advanced);
            Assert.Equal("1d", advanced.Span);
            days++;
        }

        Assert.Equal(7, days);
    }

    [Fact]
    public void TimeAdvance_records_sleep_only_care_and_skips_a_blank_care()
    {
        var api = GameApi.Live();
        var sleepOnly = TimeAdvance.Care(new PlayerNeeds(12f, 80f, 80f, true, true), api);
        var none = TimeAdvance.Care(Needs.Hold(), api);
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "ceo-time-" + Guid.NewGuid().ToString("N")));
        try
        {
            var written = TimeAdvance.RecordCare(dir.FullName, sleepOnly, 2);
            var blank = TimeAdvance.RecordCare(dir.FullName, none, 2);

            Assert.True(sleepOnly.Slept);
            Assert.False(sleepOnly.Ate);
            Assert.Equal(
                "time.cared version=" + LoadLog.Version
                    + " day=2 sleep=true food=false sleepCall=" + sleepOnly.SleepCall
                    + " foodCall=",
                written);
            Assert.Equal("", blank);
            Assert.Equal(1, File.ReadAllLines(LoadLog.CeoLogPath(dir.FullName)).Count(line => line.StartsWith("time.cared ")));
        }
        finally
        {
            Directory.Delete(dir.FullName, true);
        }
    }
}

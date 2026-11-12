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
    public void TimeAdvance_hands_off_calls_SkipTime_after_the_boost()
    {
        // Harvest: day 126 logged time.boosted boosted=false and never time.advanced.
        var api = GameApi.Live();
        var pick = TimeAdvance.HandsOff(Needs.Hold(), api);

        Assert.True(TimeAdvance.PreferFastForward);
        Assert.True(TimeAdvance.NeedsBoost(alreadyFast: false));
        Assert.False(TimeAdvance.NeedsBoost(alreadyFast: true));
        Assert.True(pick.Advanced);
        Assert.Equal("1d", pick.Span);
        Assert.Equal("Timemachine.TimeMachine.SkipTime", pick.Call);
    }

    [Fact]
    public void TimeAdvance_does_not_call_a_skip_started_when_the_machine_is_stopped()
    {
        // Harvest: time.advanced logged and WaitFor latched while Speed stayed 0/1 and boost=false.
        // Winner: paused. A stopped machine with a clock that is paused is "paused", not "slow".
        Assert.False(TimeAdvance.Engaged(called: true, machineRunning: false));
        Assert.False(TimeAdvance.Engaged(called: false, machineRunning: true));
        Assert.True(TimeAdvance.Engaged(called: true, machineRunning: true));
        Assert.Equal("paused", TimeAdvance.IdleReason(clockFound: true, paused: true, fast: false, running: false));
        Assert.Equal("slow", TimeAdvance.IdleReason(clockFound: true, paused: false, fast: false, running: false));
        Assert.Equal("noclock", TimeAdvance.IdleReason(clockFound: false, paused: true, fast: false, running: false));
        Assert.Equal("notrunning", TimeAdvance.IdleReason(clockFound: true, paused: false, fast: true, running: false));
        Assert.Equal("", TimeAdvance.IdleReason(clockFound: true, paused: true, fast: false, running: true));
        Assert.Equal("call", TimeAdvance.Next(machineRunning: false, armed: false, idleFrames: 0));
        Assert.Equal("wait", TimeAdvance.Next(machineRunning: false, armed: true, idleFrames: TimeAdvance.RetryFrames - 1));
        Assert.Equal("call", TimeAdvance.Next(machineRunning: false, armed: true, idleFrames: TimeAdvance.RetryFrames));
        Assert.Equal("advanced", TimeAdvance.Next(machineRunning: true, armed: true, idleFrames: 0));

        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "ceo-time-" + Guid.NewGuid().ToString("N")));
        try
        {
            var written = TimeAdvance.RecordIdle(dir.FullName, 131, "paused");
            var again = TimeAdvance.RecordIdle(dir.FullName, 131, "paused");
            Assert.Equal(
                "time.idle version=" + LoadLog.Version + " day=131 reason=paused",
                written);
            Assert.Equal(written, again);
            Assert.Equal(1, File.ReadAllLines(LoadLog.CeoLogPath(dir.FullName)).Count(line => line.StartsWith("time.idle ")));
        }
        finally
        {
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void TimeAdvance_start_retries_until_the_machine_is_running_and_a_new_save_clears_the_wait()
    {
        // Chain: safe needs → SkipTime → not running → retry → running → reload → SkipTime
        var api = GameApi.Live();
        var needs = Needs.Hold();
        Assert.False(TimeAdvance.BelowFloor(needs));
        var pick = TimeAdvance.HandsOff(needs, api);
        Assert.True(pick.Advanced);
        Assert.Equal("Timemachine.TimeMachine.SkipTime", pick.Call);
        Assert.False(TimeAdvance.Engaged(pick.Advanced, machineRunning: false));

        var watch = new TimeAdvanceWatch();
        var first = new object();
        Assert.True(watch.Bind(first));
        Assert.Equal("call", TimeAdvance.Next(machineRunning: false, watch.Armed, watch.IdleFrames));
        watch.Arm();
        Assert.Equal("wait", TimeAdvance.Next(machineRunning: false, watch.Armed, watch.IdleFrames));
        Assert.False(watch.Waiting(131));
        for (var i = 0; i < TimeAdvance.RetryFrames; i++)
        {
            watch.NoteIdle();
        }

        Assert.Equal("call", TimeAdvance.Next(machineRunning: false, watch.Armed, watch.IdleFrames));
        Assert.True(TimeAdvance.Engaged(pick.Advanced, machineRunning: true));
        Assert.Equal("advanced", TimeAdvance.Next(machineRunning: true, watch.Armed, watch.IdleFrames));
        watch.ClearArm();
        watch.WaitFor(132);
        Assert.True(watch.Waiting(131));
        Assert.False(watch.Bind(first));
        Assert.True(watch.Waiting(131));

        var reloaded = new object();
        Assert.True(watch.Bind(reloaded));
        Assert.False(watch.Waiting(131));
        Assert.False(watch.Armed);
        var again = TimeAdvance.HandsOff(needs, api);
        Assert.True(again.Advanced);
        Assert.Equal("1d", again.Span);
        Assert.Equal("Timemachine.TimeMachine.SkipTime", again.Call);
        Assert.True(TimeAdvance.ConstantSkip);
    }

    [Fact]
    public void TimeAdvance_resume_clears_the_wait_and_calls_SkipTime_again()
    {
        // Chain: safe needs → SkipTime → resume → SkipTime
        var api = GameApi.Live();
        var watch = new TimeAdvanceWatch();
        var first = TimeAdvance.HandsOff(Needs.Hold(), api);
        watch.WaitFor(127);
        Assert.True(watch.Waiting(126));
        watch.ClearWait();
        Assert.False(watch.Waiting(126));
        var second = TimeAdvance.HandsOff(Needs.Hold(), api);

        Assert.True(first.Advanced);
        Assert.Equal("Timemachine.TimeMachine.SkipTime", first.Call);
        Assert.True(second.Advanced);
        Assert.Equal("1d", second.Span);
        Assert.Equal("Timemachine.TimeMachine.SkipTime", second.Call);
        Assert.True(TimeAdvance.ReadyToSkip(saveHasLoan: false, logRecorded: false, day: 126));
        Assert.True(TimeAdvance.ReadyToSkip(saveHasLoan: false, logRecorded: false, day: 1));
        Assert.True(TimeAdvance.ReadyToSkip(saveHasLoan: true, logRecorded: false, day: 1));
    }

    [Fact]
    public void TimeAdvance_soak_boosts_game_speed_before_SkipTime()
    {
        // Harvest: the skip screen came up and stayed slow. FastForward cannot move it.
        // Constant speed is the fast rate for a 1-day skip.
        Assert.True(TimeAdvance.SoakBoostsGameSpeed);
        Assert.True(TimeAdvance.ConstantSkip);
        Assert.Equal("1d", TimeAdvance.DaySpan);
    }

    [Fact]
    public void TimeAdvance_sync_holds_a_same_day_recall_and_stops_on_toggle()
    {
        // Harvest: SkipTime("1d") on every day reset timeSpeedCurve to the slow start.
        // Winner: a start already issued for this day holds. It does not start again.
        var sync = new TimeSkipSync();
        var save = new object();

        Assert.Equal("hold", sync.Apply(clockReady: false, skipping: false, save, toggle: false, allowed: true, day: 5));
        Assert.Equal("start", sync.Apply(clockReady: true, skipping: false, save, toggle: false, allowed: true, day: 5));
        Assert.True(sync.DesiredOn);
        Assert.Equal("hold", sync.Apply(clockReady: true, skipping: false, save, toggle: false, allowed: true, day: 5));
        Assert.Equal("hold", sync.Apply(clockReady: false, skipping: false, save, toggle: false, allowed: true, day: 6));
        Assert.Equal("stop", sync.Apply(clockReady: false, skipping: false, save, toggle: true, allowed: true, day: 6));
        Assert.False(sync.DesiredOn);
        Assert.Equal("hold", sync.Apply(clockReady: true, skipping: false, save, toggle: false, allowed: true, day: 5));
        Assert.Equal("start", sync.Apply(clockReady: true, skipping: false, save, toggle: true, allowed: false, day: 5));
        Assert.True(sync.DesiredOn);
    }

    [Fact]
    public void TimeAdvance_a_loaded_day_past_one_starts_skip()
    {
        // Chain: day 1 with no loan → ready → activity start
        Assert.True(TimeAdvance.ReadyToSkip(saveHasLoan: false, logRecorded: false, day: 2));
        Assert.True(TimeAdvance.ReadyToSkip(saveHasLoan: false, logRecorded: false, day: 1));
        var sync = new TimeSkipSync();
        var save = new object();
        Assert.Equal("start", sync.Apply(clockReady: true, skipping: false, save, toggle: false, allowed: true, day: 2));
        Assert.True(sync.DesiredOn);
    }

    [Fact]
    public void TimeAdvance_load_screen_blocks_skip_on_a_loaded_day()
    {
        // Chain: day past 1 → loading screen still up → hold
        Assert.True(TimeAdvance.ReadyToSkip(saveHasLoan: false, logRecorded: false, day: 886));
        Assert.False(TimeAdvance.CityReady(loadFlag: true, screenUp: false));
        Assert.False(TimeAdvance.CityReady(loadFlag: false, screenUp: true));
        Assert.True(TimeAdvance.CityReady(loadFlag: false, screenUp: false));
    }

    [Fact]
    public void TimeAdvance_resume_stays_on_when_the_clock_is_not_ready()
    {
        var sync = new TimeSkipSync();
        var save = new object();
        Assert.Equal("stop", sync.Apply(clockReady: true, skipping: false, save, toggle: true, allowed: true, day: 3));
        Assert.False(sync.DesiredOn);
        Assert.Equal("hold", sync.Resume(clockReady: false, skipping: false, save, day: 3));
        Assert.True(sync.DesiredOn);
    }

    [Fact]
    public void TimeAdvance_resume_restarts_a_held_skip_that_is_not_running()
    {
        var sync = new TimeSkipSync();
        var save = new object();
        Assert.Equal("start", sync.Apply(clockReady: true, skipping: false, save, toggle: false, allowed: true, day: 20));
        Assert.Equal("hold", sync.Apply(clockReady: true, skipping: false, save, toggle: false, allowed: true, day: 20));
        Assert.Equal("start", sync.Resume(clockReady: true, skipping: false, save, day: 20));
        Assert.True(sync.DesiredOn);
        Assert.Equal("hold", sync.Resume(clockReady: true, skipping: true, save, day: 20));
        Assert.True(sync.DesiredOn);
    }

    [Fact]
    public void TimeAdvance_resume_skip_starts_again_after_f6()
    {
        // Chain: safe needs → SkipTime → F6 stop → Resume skip → SkipTime
        var api = GameApi.Live();
        var needs = Needs.Hold();
        Assert.False(TimeAdvance.BelowFloor(needs));
        var pick = TimeAdvance.Skip(needs, alreadyAdvanced: false, api);
        Assert.True(pick.Advanced);
        Assert.Equal("Timemachine.TimeMachine.SkipTime", pick.Call);

        var sync = new TimeSkipSync();
        var save = new object();
        Assert.Equal("start", sync.Apply(clockReady: true, skipping: false, save, toggle: false, allowed: true, day: 12));
        Assert.Equal("stop", sync.Apply(clockReady: true, skipping: true, save, toggle: true, allowed: true, day: 12));
        Assert.False(sync.DesiredOn);
        Assert.Equal("start", sync.Resume(clockReady: true, skipping: false, save, day: 12));
        Assert.True(sync.DesiredOn);
    }

    [Fact]
    public void TimeAdvance_esc_on_the_same_day_disarms_skip()
    {
        // Chain: safe needs → SkipTime → Esc on the same day → hold
        var api = GameApi.Live();
        var needs = Needs.Hold();
        Assert.False(TimeAdvance.BelowFloor(needs));
        var pick = TimeAdvance.Skip(needs, alreadyAdvanced: false, api);
        Assert.True(pick.Advanced);
        Assert.Equal("Timemachine.TimeMachine.SkipTime", pick.Call);

        var sync = new TimeSkipSync();
        var save = new object();
        Assert.Equal("start", sync.Apply(clockReady: true, skipping: false, save, toggle: false, allowed: true, day: 12));
        Assert.Equal("hold", sync.Apply(clockReady: true, skipping: true, save, toggle: false, allowed: true, day: 12));
        Assert.Equal("stop", sync.Apply(clockReady: true, skipping: false, save, toggle: false, allowed: true, day: 12));
        Assert.False(sync.DesiredOn);
        Assert.Equal("hold", sync.Apply(clockReady: true, skipping: false, save, toggle: false, allowed: true, day: 12));
        Assert.False(sync.DesiredOn);
        Assert.Equal("hold", sync.Apply(clockReady: true, skipping: false, save, toggle: false, allowed: true, day: 13));
    }

    [Fact]
    public void TimeAdvance_sync_chains_the_next_day_after_safe_needs()
    {
        // Chain: safe needs → one 365d constant skip → hold the next day → stop while the overlay hides the clock
        var api = GameApi.Live();
        var needs = Needs.Hold();
        Assert.False(TimeAdvance.BelowFloor(needs));
        var pick = TimeAdvance.Skip(needs, alreadyAdvanced: false, api);
        Assert.True(pick.Advanced);
        Assert.Equal("1d", pick.Span);
        Assert.Equal("Timemachine.TimeMachine.SkipTime", pick.Call);
        Assert.True(TimeAdvance.ConstantSkip);
        Assert.Equal("365d", TimeAdvance.SoakSpan);

        var sync = new TimeSkipSync();
        var save = new object();
        Assert.Equal("hold", sync.Apply(clockReady: true, skipping: false, save, toggle: false, allowed: false, day: 8));
        Assert.Equal("start", sync.Apply(clockReady: true, skipping: false, save, toggle: false, allowed: true, day: 8));
        Assert.Equal("hold", sync.Apply(clockReady: true, skipping: true, save, toggle: false, allowed: true, day: 8));
        // Winner: the next day holds. A new SkipTime here is the pause between jumps.
        Assert.Equal("hold", sync.Apply(clockReady: true, skipping: false, save, toggle: false, allowed: true, day: 9));
        Assert.Equal("hold", sync.Apply(clockReady: false, skipping: false, save, toggle: false, allowed: true, day: 40));

        var paused = sync.Apply(clockReady: true, skipping: true, save, toggle: true, allowed: true, day: 9);
        Assert.Equal("stop", paused);
        Assert.False(sync.DesiredOn);
        var reloaded = new object();
        Assert.Equal("start", sync.Apply(clockReady: true, skipping: false, reloaded, toggle: false, allowed: true, day: 1));
        Assert.True(sync.DesiredOn);

        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "ceo-time-" + Guid.NewGuid().ToString("N")));
        try
        {
            var started = TimeAdvance.RecordSynced(dir.FullName, 9, "start", "frame", 120.4f);
            var stopped = TimeAdvance.RecordSynced(dir.FullName, 9, "stop", "f6");
            Assert.Equal(
                "time.advanced version=" + LoadLog.Version
                    + " day=9 span=365d rate=120 reason=frame call=Timemachine.TimeMachine.SkipTime",
                started);
            Assert.Equal(
                "time.stopped version=" + LoadLog.Version
                    + " day=9 reason=f6 call=Timemachine.TimeMachine.CancelTimeMachine",
                stopped);
        }
        finally
        {
            Directory.Delete(dir.FullName, true);
        }
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

    [Fact]
    public void TimeAdvance_day_one_runs_without_a_loan()
    {
        Assert.True(TimeAdvance.ReadyToSkip(saveHasLoan: false, logRecorded: false, day: 1));
        Assert.False(TimeAdvance.ReadyToSkip(saveHasLoan: true, logRecorded: true, day: 0));
    }

    [Fact]
    public void TimeAdvance_sleep_rate_is_the_top_of_the_sleep_curve()
    {
        // Harvest: 6× GameSpeed moved 1494 → 1494. Sleep runs the time machine on timeSpeedCurve.
        Assert.Equal(180f, TimeAdvance.SleepRate(new[] { 5f, 180f, 40f }));
        Assert.Equal(SleepSkip.FallbackRate, TimeAdvance.SleepRate(null));
        Assert.Equal(SleepSkip.FallbackRate, TimeAdvance.SleepRate(new[] { 0f, float.NaN }));
        Assert.Equal(SleepSkip.MaxRate, TimeAdvance.SleepRate(new[] { 525600f }));
    }

    [Fact]
    public void TimeAdvance_a_not_ready_frame_holds_a_wanted_skip()
    {
        // Harvest: 1.17.5 logged time.stopped reason=frame on the frame after F6 turned it on.
        var skip = new SleepSkip();
        var save = new object();
        Assert.Equal("hold", skip.Sync(sceneReady: false, save, allowed: true, running: false));
        Assert.True(skip.DesiredOn);
        Assert.Equal("hold", skip.Sync(sceneReady: true, save, allowed: false, running: false));
        Assert.True(skip.DesiredOn);
        Assert.Equal("start", skip.Sync(sceneReady: true, save, allowed: true, running: false));
        Assert.Equal("hold", skip.Sync(sceneReady: false, save, allowed: true, running: true));
        Assert.True(skip.DesiredOn);
        Assert.True(skip.Started);
    }

    [Fact]
    public void TimeAdvance_f6_off_then_on_starts_the_skip_again()
    {
        var skip = new SleepSkip();
        var save = new object();
        Assert.Equal("start", skip.Sync(sceneReady: true, save, allowed: true, running: false));
        Assert.False(skip.Toggle());
        Assert.Equal("hold", skip.Sync(sceneReady: true, save, allowed: true, running: false));
        Assert.True(skip.Toggle());
        Assert.Equal("start", skip.Sync(sceneReady: true, save, allowed: true, running: false));
    }

    [Fact]
    public void TimeAdvance_esc_ends_the_skip_and_a_new_save_rearms_it()
    {
        // Winner: a started skip that stopped running is "ended", not a fresh "start".
        var skip = new SleepSkip();
        var first = new object();
        Assert.Equal("start", skip.Sync(sceneReady: true, first, allowed: true, running: false));
        Assert.Equal("ended", skip.Sync(sceneReady: true, first, allowed: true, running: false));
        Assert.False(skip.DesiredOn);
        Assert.Equal("hold", skip.Sync(sceneReady: true, first, allowed: true, running: false));
        var second = new object();
        Assert.Equal("start", skip.Sync(sceneReady: true, second, allowed: true, running: false));
        Assert.True(skip.DesiredOn);
    }

    [Fact]
    public void TimeAdvance_a_sleep_the_player_runs_is_not_our_skip()
    {
        var skip = new SleepSkip();
        var save = new object();
        Assert.Equal("hold", skip.Sync(sceneReady: true, save, allowed: true, running: true));
        Assert.False(skip.Started);
        Assert.Equal("start", skip.Sync(sceneReady: true, save, allowed: true, running: false));
    }

    [Fact]
    public void TimeAdvance_sleep_skip_chain_from_load_through_f6_and_esc()
    {
        // Chain: load screen up → city ready → day 1 ready → sleep-rate start → day roll holds → F6 off → F6 on → Esc ends
        var skip = new SleepSkip();
        var save = new object();
        var loading = TimeAdvance.CityReady(loadFlag: true, screenUp: true);
        var ready = TimeAdvance.ReadyToSkip(saveHasLoan: false, logRecorded: false, day: 1);
        Assert.Equal("hold", skip.Sync(loading, save, ready, running: false));

        var city = TimeAdvance.CityReady(loadFlag: false, screenUp: false);
        var started = skip.Sync(city, save, ready, running: false);
        Assert.Equal("start", started);
        var rate = TimeAdvance.SleepRate(new[] { 10f, 240f, 90f });
        Assert.Equal(240f, rate);
        Assert.False(TimeAdvanceBridge.Defer(new TimeSkipPick(true, TimeAdvance.DaySpan, "Timemachine.TimeMachine.SkipTime")));
        Assert.Equal("hold", skip.Sync(city, save, ready, running: started == "start"));

        Assert.False(skip.Toggle());
        Assert.Equal("hold", skip.Sync(city, save, ready, running: false));
        Assert.True(skip.Toggle());
        var again = skip.Sync(city, save, ready, running: false);
        Assert.Equal("start", again);
        Assert.Equal("ended", skip.Sync(city, save, ready, running: false));

        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "ceo-time-" + Guid.NewGuid().ToString("N")));
        try
        {
            var line = TimeAdvance.RecordSynced(dir.FullName, 1, again, "f6", rate);
            Assert.Equal(
                "time.advanced version=" + LoadLog.Version
                    + " day=1 span=365d rate=240 reason=f6 call=" + SleepSkip.Call,
                line);
        }
        finally
        {
            Directory.Delete(dir.FullName, true);
        }
    }
}

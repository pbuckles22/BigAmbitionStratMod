using System;
using System.Globalization;
using System.IO;

namespace ChaoticCEO
{
    public sealed class TimeCarePick
    {
        public TimeCarePick(bool slept, bool ate, string sleepCall, string foodCall)
        {
            Slept = slept;
            Ate = ate;
            SleepCall = sleepCall ?? "";
            FoodCall = foodCall ?? "";
        }

        public bool Slept { get; }

        public bool Ate { get; }

        public bool Due => Slept || Ate;

        public string SleepCall { get; }

        public string FoodCall { get; }
    }

    public sealed class TimeSkipPick
    {
        public TimeSkipPick(bool advanced, string span, string call)
        {
            Advanced = advanced;
            Span = span ?? "";
            Call = call ?? "";
        }

        public bool Advanced { get; }

        public string Span { get; }

        public string Call { get; }
    }

    public static class TimeAdvance
    {
        public const float NeedFloor = 20f;

        public const string DaySpan = "1d";

        // One long constant-speed skip. A fresh SkipTime every midnight stops the overlay,
        // hitches the frame, then starts again.
        public const string SoakSpan = "365d";

        public const int SoakDays = 365;

        public const int WeekDays = 7;

        // Player Normal makes SkipTime crawl. The skip screen then ignores FastForward.
        public const bool SoakBoostsGameSpeed = true;

        // Constant speed lets the bridge pin the rate to sleep's speed for the whole soak span.
        public const bool ConstantSkip = true;

        // A SkipTime call that does not leave the machine running is retried after this many frames.
        public const int RetryFrames = 30;

        public static bool PreferFastForward => SoakBoostsGameSpeed;

        public static bool Engaged(bool called, bool machineRunning)
        {
            return called && machineRunning;
        }

        public static bool LeaveSkipAlone(bool skipping)
        {
            return skipping;
        }

        public static bool MoveDelivery(bool replace, bool skipping)
        {
            return replace && !LeaveSkipAlone(skipping);
        }

        public static bool EscStops(bool escapeDown, bool skipping)
        {
            return escapeDown && skipping;
        }

        public static string Next(bool machineRunning, bool armed, int idleFrames)
        {
            if (machineRunning)
            {
                return "advanced";
            }

            if (!armed || idleFrames >= RetryFrames)
            {
                return "call";
            }

            return "wait";
        }

        public static string IdleReason(bool clockFound, bool paused, bool fast, bool running)
        {
            if (running)
            {
                return "";
            }

            if (!clockFound)
            {
                return "noclock";
            }

            if (paused)
            {
                return "paused";
            }

            if (!fast)
            {
                return "slow";
            }

            return "notrunning";
        }

        public static bool BelowFloor(PlayerNeeds now)
        {
            if (now == null)
            {
                return false;
            }

            return now.Energy <= NeedFloor
                || now.Hunger <= NeedFloor
                || now.Happiness <= NeedFloor;
        }

        public static TimeCarePick Care(PlayerNeeds now, ApiNote? api)
        {
            if (now == null || api == null || !BelowFloor(now))
            {
                return NoneCare();
            }

            var sleepCall = api.Call("sleep");
            var foodCall = api.Call("food");
            var happinessLow = now.Happiness <= NeedFloor;
            var sleep = now.Energy <= NeedFloor || happinessLow;
            var food = now.Hunger <= NeedFloor || happinessLow;
            if (sleep && sleepCall.Length == 0)
            {
                return NoneCare();
            }

            if (food && foodCall.Length == 0)
            {
                return NoneCare();
            }

            return new TimeCarePick(
                sleep,
                food,
                sleep ? sleepCall : "",
                food ? foodCall : "");
        }

        public static bool NeedsBoost(bool alreadyFast)
        {
            return PreferFastForward && !alreadyFast;
        }

        public static bool ReadyToSkip(bool saveHasLoan, bool logRecorded, int day)
        {
            _ = saveHasLoan;
            _ = logRecorded;
            return day >= 1;
        }

        public static bool CityReady(bool loadFlag, bool screenUp)
        {
            return !loadFlag && !screenUp;
        }

        public static TimeSkipPick HandsOff(PlayerNeeds now, ApiNote? api)
        {
            return Skip(now, alreadyAdvanced: false, api);
        }

        public static TimeSkipPick Skip(PlayerNeeds now, bool alreadyAdvanced, ApiNote? api)
        {
            var call = api == null ? "" : api.Call("time advance");
            if (alreadyAdvanced || now == null || BelowFloor(now) || call.Length == 0)
            {
                return NoneSkip();
            }

            return new TimeSkipPick(true, DaySpan, call);
        }

        public static bool KeepSkipping(TimeAdvanceWatch? watch, object? save)
        {
            return watch != null && watch.Due(save);
        }

        public static string RecordCare(string localAppData, TimeCarePick? pick, int day)
        {
            if (pick == null || !pick.Due)
            {
                return "";
            }

            var line = "time.cared version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " sleep=" + Flag(pick.Slept)
                + " food=" + Flag(pick.Ate)
                + " sleepCall=" + pick.SleepCall
                + " foodCall=" + pick.FoodCall;
            return AppendOnce(localAppData, line);
        }

        public static string RecordBoost(string localAppData, int day, bool boosted)
        {
            var line = "time.boosted version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " boosted=" + Flag(boosted);
            return AppendOnce(localAppData, line);
        }

        public static string RecordSkip(string localAppData, TimeSkipPick? pick, int day)
        {
            if (pick == null || !pick.Advanced)
            {
                return "";
            }

            var line = "time.advanced version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " span=" + pick.Span
                + " call=" + pick.Call;
            return AppendOnce(localAppData, line);
        }

        public static string RecordSynced(string localAppData, int day, string? action, string? reason, float rate = 0f)
        {
            if (action == "stop")
            {
                var stop = "time.stopped version=" + LoadLog.Version
                    + " day=" + day.ToString(CultureInfo.InvariantCulture)
                    + " reason=" + (reason ?? "")
                    + " call=" + SleepSkip.StopCall;
                return AppendOnce(localAppData, stop);
            }

            if (action != "start")
            {
                return "";
            }

            var line = "time.advanced version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " span=" + SoakSpan
                + " rate=" + Math.Round(rate).ToString(CultureInfo.InvariantCulture)
                + " reason=" + (reason ?? "")
                + " call=" + SleepSkip.Call;
            return AppendOnce(localAppData, line);
        }

        // Minutes per real second. Sleep runs the time machine on timeSpeedCurve; its top value is sleep's speed.
        public static float SleepRate(float[]? curveSamples)
        {
            var best = 0f;
            if (curveSamples != null)
            {
                foreach (var sample in curveSamples)
                {
                    if (!float.IsNaN(sample) && !float.IsInfinity(sample) && sample > best)
                    {
                        best = sample;
                    }
                }
            }

            return best > 0f ? Math.Min(best, SleepSkip.MaxRate) : SleepSkip.FallbackRate;
        }

        public static string RecordIdle(string localAppData, int day, string? reason)
        {
            if (string.IsNullOrEmpty(reason))
            {
                return "";
            }

            var line = "time.idle version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " reason=" + reason;
            return AppendOnce(localAppData, line);
        }

        private static string AppendOnce(string localAppData, string line)
        {
            var path = LoadLog.CeoLogPath(localAppData ?? "");
            if (File.Exists(path))
            {
                foreach (var existing in File.ReadAllLines(path))
                {
                    if (existing == line)
                    {
                        return line;
                    }
                }
            }

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(path, line + System.Environment.NewLine);
            return line;
        }

        private static string Flag(bool value)
        {
            return value ? "true" : "false";
        }

        private static TimeCarePick NoneCare()
        {
            return new TimeCarePick(false, false, "", "");
        }

        private static TimeSkipPick NoneSkip()
        {
            return new TimeSkipPick(false, "", "");
        }
    }

    public sealed class TimeSkipSync
    {
        private object? _save;
        private bool _armed;
        private int _startDay = -1;
        private bool _sawRunning;
        private int _seenDay = -1;

        public bool DesiredOn { get; private set; } = true;

        public string Apply(bool clockReady, bool skipping, object? save, bool toggle, bool allowed, int day)
        {
            if (save != null && !ReferenceEquals(_save, save))
            {
                var seen = _save != null;
                _save = save;
                _armed = false;
                _startDay = -1;
                _sawRunning = false;
                _seenDay = -1;
                if (!toggle || seen)
                {
                    DesiredOn = true;
                }
            }

            if (toggle)
            {
                DesiredOn = !DesiredOn;
                if (!DesiredOn)
                {
                    _armed = false;
                    _startDay = -1;
                    return Finish(skipping, day, "stop");
                }
            }

            // Esc closed the overlay: the machine was running and this day did not roll.
            if (DesiredOn && _sawRunning && !skipping && day == _seenDay)
            {
                DesiredOn = false;
                _armed = false;
                _startDay = -1;
                return Finish(skipping, day, "stop");
            }

            if (save == null || !clockReady || !DesiredOn || skipping)
            {
                return Finish(skipping, day, "hold");
            }

            // The soak span is still in flight. Another SkipTime would stop the overlay and hitch.
            if (_armed && day < _startDay + TimeAdvance.SoakDays)
            {
                return Finish(skipping, day, "hold");
            }

            if (!allowed && !toggle)
            {
                return Finish(skipping, day, "hold");
            }

            _armed = true;
            _startDay = day;
            return Finish(skipping, day, "start");
        }

        private string Finish(bool skipping, int day, string action)
        {
            _sawRunning = skipping;
            _seenDay = day;
            return action;
        }

        public string Resume(bool clockReady, bool skipping, object? save, int day)
        {
            DesiredOn = true;
            if (!skipping)
            {
                _armed = false;
                _startDay = -1;
                _sawRunning = false;
            }

            return Apply(clockReady, skipping, save, toggle: false, allowed: true, day);
        }

        public void ReleaseStart()
        {
            _armed = false;
            _startDay = -1;
        }
    }

    public sealed class SleepSkip
    {
        public const string Call = "Timemachine.TimeMachine.SkipTime";

        public const string StopCall = "Timemachine.TimeMachine.CancelTimeMachine";

        // Used when timeSpeedCurve cannot be read.
        public const float FallbackRate = 60f;

        // A 365-day constant skip at its own rate moved about 24 days per frame.
        public const float MaxRate = 600f;

        private object? _save;
        private bool _started;

        public bool DesiredOn { get; private set; } = true;

        public bool Started => _started;

        public string Sync(bool sceneReady, object? save, bool allowed, bool running)
        {
            Bind(save);

            // The skip we started ended without F6: Esc, or the goal was reached.
            if (_started && !running)
            {
                _started = false;
                DesiredOn = false;
                return "ended";
            }

            // A frame that is not ready yet never stops a wanted skip.
            if (save == null || !sceneReady || running || !DesiredOn || !allowed)
            {
                return "hold";
            }

            _started = true;
            return "start";
        }

        public bool Toggle()
        {
            DesiredOn = !DesiredOn;
            _started = false;
            return DesiredOn;
        }

        public void Resume()
        {
            DesiredOn = true;
            _started = false;
        }

        public void Stop()
        {
            DesiredOn = false;
            _started = false;
        }

        public void Release()
        {
            _started = false;
        }

        private void Bind(object? save)
        {
            if (save == null || ReferenceEquals(_save, save))
            {
                return;
            }

            _save = save;
            _started = false;
            DesiredOn = true;
        }
    }

    public sealed class SkipSight
    {
        public SkipSight(bool clockFound, bool paused, bool fast, bool running)
        {
            ClockFound = clockFound;
            Paused = paused;
            Fast = fast;
            Running = running;
        }

        public bool ClockFound { get; }

        public bool Paused { get; }

        public bool Fast { get; }

        public bool Running { get; }
    }

    public sealed class TimeAdvanceWatch
    {
        private object? _seen;
        private object? _save;
        private int _completed;
        private int? _waitingForDay;
        private int? _boostDay;
        private bool _armed;
        private int _idle;

        public bool Armed => _armed;

        public int IdleFrames => _idle;

        public bool Bind(object? save)
        {
            if (save == null || ReferenceEquals(_save, save))
            {
                return false;
            }

            _save = save;
            _waitingForDay = null;
            _armed = false;
            _idle = 0;
            _boostDay = null;
            return true;
        }

        public void Arm()
        {
            _armed = true;
            _idle = 0;
        }

        public void ClearArm()
        {
            _armed = false;
            _idle = 0;
        }

        public void NoteIdle()
        {
            _idle++;
        }

        public bool Due(object? save)
        {
            // WeekDays still gates the one-week TryTime finish via Completed + Mark.
            // Hands-off soak must keep Due after day 8 so Autopilot can force-skip.
            return save != null;
        }

        public bool Waiting(int day)
        {
            return _waitingForDay.HasValue && day < _waitingForDay.Value;
        }

        public bool Arrive(int day)
        {
            if (!_waitingForDay.HasValue || day < _waitingForDay.Value)
            {
                return false;
            }

            _waitingForDay = null;
            _completed++;
            return true;
        }

        public void WaitFor(int day)
        {
            _waitingForDay = day;
        }

        public void ClearWait()
        {
            _waitingForDay = null;
        }

        public bool NoteBoost(int day)
        {
            if (_boostDay == day)
            {
                return false;
            }

            _boostDay = day;
            return true;
        }

        public void ArmBoost()
        {
            _boostDay = null;
            _waitingForDay = null;
        }

        public int Completed => _completed;

        public void Mark(object? save)
        {
            _seen = save;
        }

        public bool Finished(object? save)
        {
            return save != null && ReferenceEquals(_seen, save);
        }
    }
}

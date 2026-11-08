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

        public const int WeekDays = 7;

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

        public static TimeSkipPick Skip(PlayerNeeds now, bool alreadyAdvanced, ApiNote? api)
        {
            var call = api == null ? "" : api.Call("time advance");
            if (alreadyAdvanced || now == null || BelowFloor(now) || call.Length == 0)
            {
                return NoneSkip();
            }

            return new TimeSkipPick(true, DaySpan, call);
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

    public sealed class TimeAdvanceWatch
    {
        private object? _seen;
        private int _completed;
        private int? _waitingForDay;

        public bool Due(object? save)
        {
            return save != null && _completed < TimeAdvance.WeekDays;
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

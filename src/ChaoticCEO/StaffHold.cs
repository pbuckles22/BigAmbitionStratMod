using System.Globalization;
using System.IO;

namespace ChaoticCEO
{
    public sealed class StaffState
    {
        public StaffState(
            float satisfaction,
            int ageInDays,
            bool complaining,
            int hoursUntilNextComplaint,
            bool poachPending,
            int nextSickDay = int.MaxValue,
            bool absent = false)
        {
            Satisfaction = satisfaction;
            AgeInDays = ageInDays;
            Complaining = complaining;
            HoursUntilNextComplaint = hoursUntilNextComplaint;
            PoachPending = poachPending;
            NextSickDay = nextSickDay;
            Absent = absent;
        }

        public float Satisfaction { get; }

        public int AgeInDays { get; }

        public bool Complaining { get; }

        public int HoursUntilNextComplaint { get; }

        public bool PoachPending { get; }

        /// <summary>Game day they will call in sick. <see cref="StaffHold.NeverDay"/> means they will not.</summary>
        public int NextSickDay { get; }

        public bool Absent { get; }
    }

    public sealed class StaffHoldPlan
    {
        public StaffHoldPlan(
            bool raise,
            int age,
            bool clearComplaint,
            int complaintHours,
            bool clearPoach,
            int sickDay = -1,
            bool clearAbsent = false)
        {
            Raise = raise;
            Age = age;
            ClearComplaint = clearComplaint;
            ComplaintHours = complaintHours;
            ClearPoach = clearPoach;
            SickDay = sickDay;
            ClearAbsent = clearAbsent;
        }

        public bool Raise { get; }

        /// <summary>New age in days, or -1 to keep.</summary>
        public int Age { get; }

        public bool ClearComplaint { get; }

        /// <summary>New hours until the next complaint roll, or -1 to keep.</summary>
        public int ComplaintHours { get; }

        public bool ClearPoach { get; }

        /// <summary>New sick day, or -1 to keep. <see cref="StaffHold.NeverDay"/> means they will not call in.</summary>
        public int SickDay { get; }

        public bool ClearAbsent { get; }

        public bool Any => Raise || Age >= 0 || ClearComplaint || ComplaintHours >= 0 || ClearPoach || SickDay >= 0 || ClearAbsent;
    }

    /// <summary>
    /// TEST CHEAT (doc/TEST_CHEATS.md): employees stay at full satisfaction, never complain-resign,
    /// never take a rival poach, never reach the retirement notice, and never call in sick.
    /// Turn off before balancing HR (14.10).
    /// </summary>
    public static class StaffHold
    {
        public static bool Enabled = true;

        public const float Satisfaction = 100f;

        /// <summary>Game retires at 67 and sends the notice one year earlier.</summary>
        public const int RetirementAge = 67;

        /// <summary>Reset to this age once a worker passes <see cref="CeilingYears"/>.</summary>
        public const int HoldAgeYears = 60;

        /// <summary>One year under the retirement notice.</summary>
        public const int CeilingYears = RetirementAge - 2;

        /// <summary>
        /// A sick day the daily check never reaches. The game calls them in when
        /// <c>nextSickDay</c> is today or earlier, and only skips the call when it is later.
        /// </summary>
        public const int NeverDay = int.MaxValue;

        public static StaffHoldPlan Decide(StaffState state, int daysPerYear)
        {
            var year = daysPerYear > 0 ? daysPerYear : 1;
            var raise = state.Satisfaction < Satisfaction;
            var age = state.AgeInDays >= CeilingYears * year ? HoldAgeYears * year : -1;
            var hours = state.HoursUntilNextComplaint < year * 24 ? year * 24 : -1;
            var sick = state.NextSickDay >= NeverDay ? -1 : NeverDay;
            return new StaffHoldPlan(raise, age, state.Complaining, hours, state.PoachPending, sick, state.Absent);
        }

        public static StaffState Apply(StaffState state, StaffHoldPlan plan)
        {
            return new StaffState(
                plan.Raise ? Satisfaction : state.Satisfaction,
                plan.Age >= 0 ? plan.Age : state.AgeInDays,
                !plan.ClearComplaint && state.Complaining,
                plan.ComplaintHours >= 0 ? plan.ComplaintHours : state.HoursUntilNextComplaint,
                !plan.ClearPoach && state.PoachPending,
                plan.SickDay >= 0 ? plan.SickDay : state.NextSickDay,
                !plan.ClearAbsent && state.Absent);
        }

        /// <summary>
        /// Why the game can take this worker away, in the register's reason order
        /// (<see cref="CashierCover.Reason"/>): retired, then quit (zero satisfaction or an open complaint), then poached.
        /// Empty when none apply.
        /// </summary>
        public static string Leaving(StaffState state, int daysPerYear)
        {
            var year = daysPerYear > 0 ? daysPerYear : 1;
            var retired = state.AgeInDays >= RetirementAge * year;
            var quit = state.Satisfaction <= 0f || state.Complaining;
            if (!retired && !quit && !state.PoachPending)
            {
                return "";
            }

            return CashierCover.Reason(quit, retired, state.PoachPending);
        }

        public static string Line(int day, int employees, int raised, int aged, int complaints, int poach, int sick)
        {
            return "staff.held version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " employees=" + employees.ToString(CultureInfo.InvariantCulture)
                + " raised=" + raised.ToString(CultureInfo.InvariantCulture)
                + " aged=" + aged.ToString(CultureInfo.InvariantCulture)
                + " complaints=" + complaints.ToString(CultureInfo.InvariantCulture)
                + " poach=" + poach.ToString(CultureInfo.InvariantCulture)
                + " sick=" + sick.ToString(CultureInfo.InvariantCulture);
        }
    }

    /// <summary>One pass per game hour. Logs the first pass of each day and any pass that saved a worker.</summary>
    public sealed class StaffHoldWatch
    {
        private int _day = -1;
        private int _hour = -1;

        public bool Due(int day, int hour)
        {
            return day != _day || hour != _hour;
        }

        public string Tick(string? localAppData, int day, int hour, int employees, int raised, int aged, int complaints, int poach, int sick)
        {
            var newDay = day != _day;
            _day = day;
            _hour = hour;
            if (!newDay && aged == 0 && complaints == 0 && poach == 0 && sick == 0)
            {
                return "";
            }

            var line = StaffHold.Line(day, employees, raised, aged, complaints, poach, sick);
            if (!string.IsNullOrEmpty(localAppData))
            {
                var path = LoadLog.CeoLogPath(localAppData!);
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.AppendAllText(path, line + System.Environment.NewLine);
            }

            return line;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace ChaoticCEO
{
    public sealed class WinRunFrame
    {
        public WinRunFrame(
            int day,
            decimal netWorth,
            string? bar,
            string? health,
            PlaybookStep? step,
            PlayerNeeds? needs = null)
        {
            Day = day;
            NetWorth = netWorth;
            Bar = bar;
            Health = health;
            Step = step;
            Needs = needs;
        }

        public int Day { get; }

        public decimal NetWorth { get; }

        public string? Bar { get; }

        public string? Health { get; }

        public PlaybookStep? Step { get; }

        public PlayerNeeds? Needs { get; }
    }

    public sealed class WinRunDay
    {
        public WinRunDay(AutopilotDay loop, string reached)
        {
            Loop = loop ?? AutopilotDay.Skipped("");
            Reached = reached ?? "";
        }

        public AutopilotDay Loop { get; }

        public string Reached { get; }
    }

    public sealed class WinRunResult
    {
        public WinRunResult(
            string reached,
            int winDay,
            IReadOnlyList<WinRunDay> days,
            IReadOnlyList<int> telemetryDays,
            bool autopilotStillOn)
        {
            Reached = reached ?? "";
            WinDay = winDay;
            Days = days ?? Array.Empty<WinRunDay>();
            TelemetryDays = telemetryDays ?? Array.Empty<int>();
            AutopilotStillOn = autopilotStillOn;
        }

        public string Reached { get; }

        public int WinDay { get; }

        public IReadOnlyList<WinRunDay> Days { get; }

        public IReadOnlyList<int> TelemetryDays { get; }

        public bool AutopilotStillOn { get; }

        public bool AdvancedAfterWin
        {
            get
            {
                if (WinDay <= 0 || TelemetryDays.Count == 0)
                {
                    return false;
                }

                var after = TelemetryDays.Where(day => day > WinDay).ToList();
                if (after.Count == 0)
                {
                    return false;
                }

                for (var index = 1; index < after.Count; index++)
                {
                    if (after[index] <= after[index - 1])
                    {
                        return false;
                    }
                }

                return after[0] > WinDay;
            }
        }
    }

    public static class WinRun
    {
        private static string _pendingOpen = "";

        public static string PendingOpen => _pendingOpen;

        public static void ClearPending()
        {
            _pendingOpen = "";
        }

        public static void NotePending(string? type)
        {
            _pendingOpen = type ?? "";
        }

        public static PlaybookStep Next(
            int stage,
            IReadOnlyList<OpenCandidate>? opens,
            string? pendingOpen,
            IReadOnlyList<SiteCandidate>? sites,
            decimal cash,
            decimal reserve,
            MoneyPick? money,
            RunPick? run,
            IReadOnlyList<StoreHealth>? ownedHealth = null)
        {
            var pending = pendingOpen ?? "";
            OpenPick open;
            SitePick where;
            if (pending.Length > 0)
            {
                where = Where.Decide(sites, cash, reserve);
                if (!where.Chosen)
                {
                    ClearPending();
                }

                // Suppress open on this same tick even after clear, so money/run can run.
                open = OpenPick.None();
            }
            else
            {
                open = Open.Decide(stage, opens, truckLoadFraction: 0m, ownedHealth);
                where = SitePick.None();
            }

            return Playbook.Decide(
                open,
                where,
                LayoutPick.None(),
                PricePick.None(),
                SupplyPick.None(),
                TruckPick.None(),
                WarehousePick.None(),
                ManagerPick.None(),
                money ?? MoneyPick.None(),
                EstatePick.None(),
                ClosePick.None(),
                run ?? RunPick.None());
        }

        public static WinRunDay Day(
            string? localAppData,
            int day,
            decimal netWorth,
            ApiNote? api,
            AuditWorld? world,
            AuditGuard? guard,
            Func<AuditHeal, HealFate>? heal,
            PlaybookStep? step,
            PlayerNeeds? needs,
            string? bar,
            string? health,
            Func<TimeSkipPick, bool>? timeBridge = null,
            Func<PlaybookStep, string, bool>? actionBridge = null)
        {
            if (string.IsNullOrWhiteSpace(localAppData) || day <= 0)
            {
                return new WinRunDay(AutopilotDay.Skipped(""), "");
            }

            var loop = Autopilot.Day(
                localAppData,
                day,
                api,
                world,
                guard,
                heal,
                step,
                needs,
                bar,
                health,
                timeBridge,
                actionBridge);
            if (!loop.Ran)
            {
                return new WinRunDay(loop, "");
            }

            var reached = Win.Record(LoadLog.CeoLogPath(localAppData), day, netWorth);
            if (step != null && step.Chosen && step.Kind == "open" && step.Detail.Length > 0)
            {
                NotePending(step.Detail);
            }
            else if (step != null && step.Chosen && step.Kind == "where")
            {
                ClearPending();
            }

            return new WinRunDay(loop, reached);
        }

        public static WinRunResult Run(
            string? localAppData,
            IReadOnlyList<WinRunFrame>? frames,
            ApiNote? api,
            AuditWorld? world,
            AuditGuard? guard,
            Func<AuditHeal, HealFate>? heal,
            Func<TimeSkipPick, bool>? timeBridge = null,
            Func<PlaybookStep, string, bool>? actionBridge = null)
        {
            if (string.IsNullOrWhiteSpace(localAppData) || frames == null || frames.Count == 0)
            {
                return new WinRunResult("", 0, Array.Empty<WinRunDay>(), Array.Empty<int>(), Autopilot.Enabled);
            }

            var days = new List<WinRunDay>();
            var telemetryDays = new List<int>();
            var reached = "";
            var winDay = 0;
            foreach (var frame in frames)
            {
                if (frame == null || frame.Day <= 0)
                {
                    continue;
                }

                var day = Day(
                    localAppData,
                    frame.Day,
                    frame.NetWorth,
                    api,
                    world,
                    guard,
                    heal,
                    frame.Step,
                    frame.Needs,
                    frame.Bar,
                    frame.Health,
                    timeBridge,
                    actionBridge);
                days.Add(day);
                if (day.Loop.Ran)
                {
                    telemetryDays.Add(frame.Day);
                }

                if (reached.Length == 0 && day.Reached.Length > 0)
                {
                    reached = day.Reached;
                    winDay = frame.Day;
                }
            }

            return new WinRunResult(reached, winDay, days, telemetryDays, Autopilot.Enabled);
        }

        public static IReadOnlyList<int> TelemetryDays(string? localAppData)
        {
            var path = Telemetry.CsvPath(localAppData ?? "");
            if (!File.Exists(path))
            {
                return Array.Empty<int>();
            }

            var days = new List<int>();
            foreach (var line in File.ReadAllLines(path).Skip(1))
            {
                var cell = line.Split(',')[0];
                if (int.TryParse(cell, NumberStyles.Integer, CultureInfo.InvariantCulture, out var day) && day > 0)
                {
                    days.Add(day);
                }
            }

            return days;
        }
    }
}

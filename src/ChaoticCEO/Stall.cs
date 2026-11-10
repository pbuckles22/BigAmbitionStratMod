using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ChaoticCEO
{
    public sealed class StallPick
    {
        public StallPick(string gate, string action, bool pause, bool chosen)
        {
            Gate = gate ?? "";
            Action = action ?? "";
            Pause = pause;
            Chosen = chosen;
        }

        public string Gate { get; }

        public string Action { get; }

        public bool Pause { get; }

        public bool Chosen { get; }

        public static StallPick None()
        {
            return new StallPick("", "", false, false);
        }
    }

    public static class Stall
    {
        public const int StuckDays = 3;

        private static readonly Dictionary<string, StallState> States =
            new Dictionary<string, StallState>(StringComparer.Ordinal);

        public static string FailedGate(StoreHealth? health)
        {
            if (health == null || health.Ready)
            {
                return "";
            }

            if (!health.Open)
            {
                return "g1";
            }

            if (!health.Stocked)
            {
                return "g2";
            }

            if (!health.Staffed)
            {
                return "g3";
            }

            if (!health.Selling)
            {
                return "g4";
            }

            if (!health.Profitable)
            {
                return "g5";
            }

            return "";
        }

        public static IReadOnlyList<string> Remedies(string? gate)
        {
            switch (gate)
            {
                case "g1":
                    return new[] { "reopen" };
                case "g2":
                    return new[] { "restock" };
                case "g3":
                    return new[] { "restaff" };
                case "g4":
                    return new[] { "prices" };
                case "g5":
                    return new[] { "restock", "prices" };
                default:
                    return Array.Empty<string>();
            }
        }

        public static StallPick Decide(
            StoreHealth? health,
            int falseDays,
            IReadOnlyList<string>? tried)
        {
            var gate = FailedGate(health);
            if (gate.Length == 0 || falseDays < StuckDays)
            {
                return StallPick.None();
            }

            var remedies = Remedies(gate);
            foreach (var remedy in remedies)
            {
                if (AlreadyTried(tried, remedy))
                {
                    continue;
                }

                return new StallPick(gate, remedy, pause: false, chosen: true);
            }

            return new StallPick(gate, "", pause: true, chosen: true);
        }

        public static StallPick Track(string localAppData, StoreHealth? health, int day)
        {
            if (health == null || string.IsNullOrEmpty(health.Place) || day <= 0)
            {
                return StallPick.None();
            }

            var gate = FailedGate(health);
            if (gate.Length == 0)
            {
                States.Remove(health.Place);
                return StallPick.None();
            }

            if (!States.TryGetValue(health.Place, out var state) || state.Gate != gate)
            {
                state = new StallState(gate, falseDays: 1, day, new List<string>());
                States[health.Place] = state;
            }
            else if (state.LastDay != day)
            {
                state.FalseDays += 1;
                state.LastDay = day;
            }

            var dueAt = StuckDays + state.Tried.Count * StoreHealthGate.ProfitProofDays;
            if (state.FalseDays < dueAt)
            {
                return StallPick.None();
            }

            var pick = Decide(health, StuckDays, state.Tried);
            if (!pick.Chosen)
            {
                return pick;
            }

            if (pick.Pause)
            {
                RecordPause(localAppData, health.Place, pick.Gate);
                Controls.Pause();
                States.Remove(health.Place);
                return pick;
            }

            if (pick.Action.Length > 0 && !AlreadyTried(state.Tried, pick.Action))
            {
                state.Tried.Add(pick.Action);
                RecordRemedy(localAppData, day, health.Place, pick.Gate, pick.Action);
            }

            return pick;
        }

        public static string PauseLine(string? place, string? gate)
        {
            return "autopilot.paused version=" + LoadLog.Version
                + " reason=stuck"
                + " gate=" + (gate ?? "")
                + " place=" + (place ?? "");
        }

        public static string RecordPause(string localAppData, string? place, string? gate)
        {
            var line = PauseLine(place, gate);
            Append(localAppData, line);
            return line;
        }

        public static string RemedyLine(int day, string? place, string? gate, string? action)
        {
            return "stall.remedy version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " place=" + (place ?? "")
                + " gate=" + (gate ?? "")
                + " action=" + (action ?? "");
        }

        public static string RecordRemedy(
            string localAppData,
            int day,
            string? place,
            string? gate,
            string? action)
        {
            var line = RemedyLine(day, place, gate, action);
            Append(localAppData, line);
            return line;
        }

        public static void Clear()
        {
            States.Clear();
        }

        private static void Append(string localAppData, string line)
        {
            if (string.IsNullOrWhiteSpace(localAppData) || string.IsNullOrEmpty(line))
            {
                return;
            }

            var path = LoadLog.CeoLogPath(localAppData);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(path, line + Environment.NewLine);
        }

        private static bool AlreadyTried(IReadOnlyList<string>? tried, string remedy)
        {
            if (tried == null || tried.Count == 0)
            {
                return false;
            }

            foreach (var row in tried)
            {
                if (row == remedy)
                {
                    return true;
                }
            }

            return false;
        }

        private sealed class StallState
        {
            public StallState(string gate, int falseDays, int lastDay, List<string> tried)
            {
                Gate = gate;
                FalseDays = falseDays;
                LastDay = lastDay;
                Tried = tried;
            }

            public string Gate { get; }

            public int FalseDays { get; set; }

            public int LastDay { get; set; }

            public List<string> Tried { get; }
        }
    }
}


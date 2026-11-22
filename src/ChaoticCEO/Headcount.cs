using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ChaoticCEO
{
    public sealed class HeadcountSeat
    {
        public HeadcountSeat(string id, string skill, decimal hourly, bool scheduled)
            : this(id, skill, hourly, scheduled, "")
        {
        }

        public HeadcountSeat(string id, string skill, decimal hourly, bool scheduled, string building)
        {
            Id = id ?? "";
            Skill = skill ?? "";
            Hourly = hourly;
            Scheduled = scheduled;
            Building = building ?? "";
        }

        public string Id { get; }

        public string Skill { get; }

        public decimal Hourly { get; }

        public bool Scheduled { get; }

        public string Building { get; }
    }

    public sealed class HeadcountRole
    {
        public HeadcountRole(int day, int count, bool hire, string keep, IReadOnlyList<string> drop)
        {
            Day = day;
            Count = count;
            Hire = hire;
            Keep = keep ?? "";
            Drop = drop ?? new string[0];
        }

        public int Day { get; }

        public int Count { get; }

        public bool Hire { get; }

        public string Keep { get; }

        public IReadOnlyList<string> Drop { get; }
    }

    public sealed class HeadcountPlan
    {
        public HeadcountPlan(HeadcountRole cashier, HeadcountRole cleaner)
        {
            Cashier = cashier;
            Cleaner = cleaner;
        }

        public HeadcountRole Cashier { get; }

        public HeadcountRole Cleaner { get; }
    }

    public static class Headcount
    {
        public static int Open(int day, int coveredDay)
        {
            return Morning.CoverDay(null, autopilotOn: false, day, coveredDay);
        }

        public static HeadcountPlan Decide(IReadOnlyList<HeadcountSeat>? roster)
        {
            var cashier = Cashier(1, roster);
            return new HeadcountPlan(cashier, Cleaner(cashier, roster));
        }

        public static HeadcountRole Cashier(int day, IReadOnlyList<HeadcountSeat>? roster)
        {
            return Tally(day, roster, ChaoticCEO.Cashier.Skill);
        }

        public static HeadcountRole Cleaner(HeadcountRole? cashier, IReadOnlyList<HeadcountSeat>? roster)
        {
            var day = cashier == null ? 0 : cashier.Day;
            return Tally(day, roster, ChaoticCEO.Cleaner.Skill);
        }

        public static IReadOnlyList<string> Extra(HeadcountRole? cashier, HeadcountRole? cleaner)
        {
            var keep = new HashSet<string>(System.StringComparer.Ordinal);
            if (cashier != null && cashier.Keep.Length > 0)
            {
                keep.Add(cashier.Keep);
            }

            if (cleaner != null && cleaner.Keep.Length > 0)
            {
                keep.Add(cleaner.Keep);
            }

            var drop = new List<string>();
            AddExtra(drop, keep, cashier);
            AddExtra(drop, keep, cleaner);
            return drop;
        }

        public static string Record(string localAppData, HeadcountRole? cashier, HeadcountRole? cleaner)
        {
            if (cashier == null || cleaner == null || cashier.Day <= 0)
            {
                return "";
            }

            var line = "headcount.held version=" + LoadLog.Version
                + " day=" + cashier.Day.ToString(CultureInfo.InvariantCulture)
                + " cashiers=" + cashier.Count.ToString(CultureInfo.InvariantCulture)
                + " cleaners=" + cleaner.Count.ToString(CultureInfo.InvariantCulture)
                + " dropped=" + Extra(cashier, cleaner).Count.ToString(CultureInfo.InvariantCulture);
            return AppendOnce(localAppData ?? "", line);
        }

        private static HeadcountRole Tally(int day, IReadOnlyList<HeadcountSeat>? roster, string skill)
        {
            var seats = new List<HeadcountSeat>();
            if (roster != null)
            {
                foreach (var seat in roster)
                {
                    if (seat != null && seat.Skill == skill)
                    {
                        seats.Add(seat);
                    }
                }
            }

            var keep = "";
            HeadcountSeat? best = null;
            var drop = new List<string>();
            foreach (var group in Groups(seats))
            {
                HeadcountSeat? local = null;
                foreach (var seat in group)
                {
                    if (local == null || Prefer(seat, local))
                    {
                        local = seat;
                    }
                }

                if (local != null && (best == null || Prefer(local, best)))
                {
                    best = local;
                    keep = local.Id;
                }

                if (day <= 0 || group.Count < 2 || local == null)
                {
                    continue;
                }

                foreach (var seat in group)
                {
                    if (seat.Id.Length > 0 && seat.Id != local.Id)
                    {
                        drop.Add(seat.Id);
                    }
                }
            }

            return new HeadcountRole(day, seats.Count, day > 0 && seats.Count == 0, day > 0 ? keep : "", drop);
        }

        private static List<List<HeadcountSeat>> Groups(List<HeadcountSeat> seats)
        {
            var order = new List<string>();
            var groups = new Dictionary<string, List<HeadcountSeat>>(System.StringComparer.Ordinal);
            foreach (var seat in seats)
            {
                var key = seat.Building ?? "";
                if (!groups.TryGetValue(key, out var group))
                {
                    group = new List<HeadcountSeat>();
                    groups[key] = group;
                    order.Add(key);
                }

                group.Add(seat);
            }

            var rows = new List<List<HeadcountSeat>>();
            foreach (var key in order)
            {
                rows.Add(groups[key]);
            }

            return rows;
        }

        private static bool Prefer(HeadcountSeat next, HeadcountSeat current)
        {
            if (next.Scheduled != current.Scheduled)
            {
                return next.Scheduled;
            }

            var nextPaid = next.Hourly > 0m;
            var currentPaid = current.Hourly > 0m;
            if (nextPaid != currentPaid)
            {
                return nextPaid;
            }

            return true;
        }

        private static void AddExtra(List<string> drop, HashSet<string> keep, HeadcountRole? role)
        {
            if (role == null)
            {
                return;
            }

            foreach (var id in role.Drop)
            {
                if (id.Length == 0 || keep.Contains(id) || drop.Contains(id))
                {
                    continue;
                }

                drop.Add(id);
            }
        }

        private static string AppendOnce(string localAppData, string line)
        {
            var path = LoadLog.CeoLogPath(localAppData);
            if (File.Exists(path))
            {
                foreach (var existing in File.ReadAllLines(path))
                {
                    if (existing == line)
                    {
                        CeoLedger.Ingest(line);
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
            CeoLedger.Ingest(line);
            return line;
        }
    }
}

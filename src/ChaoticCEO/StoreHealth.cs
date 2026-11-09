using System;
using System.Collections.Generic;
using System.IO;

namespace ChaoticCEO
{
    public sealed class CargoAmount
    {
        public CargoAmount(string item, int amount)
        {
            Item = item ?? "";
            Amount = amount;
        }

        public string Item { get; }

        public int Amount { get; }
    }

    public sealed class StoreFacts
    {
        public StoreFacts(
            string place,
            bool temporarilyClosed,
            IReadOnlyList<string>? neededItems,
            IReadOnlyDictionary<string, int>? cargoOnHand,
            bool registerStaffed,
            bool cleanerStaffed,
            float staffSatisfaction,
            bool scheduledOpen,
            int dailyCustomers,
            decimal dailyRevenue,
            IReadOnlyList<decimal>? recentOpenDayProfits)
        {
            Place = place ?? "";
            TemporarilyClosed = temporarilyClosed;
            NeededItems = neededItems ?? Array.Empty<string>();
            CargoOnHand = cargoOnHand ?? new Dictionary<string, int>();
            RegisterStaffed = registerStaffed;
            CleanerStaffed = cleanerStaffed;
            StaffSatisfaction = staffSatisfaction;
            ScheduledOpen = scheduledOpen;
            DailyCustomers = dailyCustomers;
            DailyRevenue = dailyRevenue;
            RecentOpenDayProfits = recentOpenDayProfits ?? Array.Empty<decimal>();
        }

        public string Place { get; }

        public bool TemporarilyClosed { get; }

        public IReadOnlyList<string> NeededItems { get; }

        public IReadOnlyDictionary<string, int> CargoOnHand { get; }

        public bool RegisterStaffed { get; }

        public bool CleanerStaffed { get; }

        public float StaffSatisfaction { get; }

        public bool ScheduledOpen { get; }

        public int DailyCustomers { get; }

        public decimal DailyRevenue { get; }

        public IReadOnlyList<decimal> RecentOpenDayProfits { get; }
    }

    public sealed class StoreHealth
    {
        private static string _seen = "";

        public StoreHealth(
            bool open,
            bool stocked,
            bool staffed,
            bool selling,
            bool profitable,
            string place = "")
        {
            Open = open;
            Stocked = stocked;
            Staffed = staffed;
            Selling = selling;
            Profitable = profitable;
            Place = place ?? "";
        }

        public bool Open { get; }

        public bool Stocked { get; }

        public bool Staffed { get; }

        public bool Selling { get; }

        public bool Profitable { get; }

        public string Place { get; }

        public bool Ready => Open && Stocked && Staffed && Selling && Profitable;

        public static StoreHealth Healthy(string place = "")
        {
            return new StoreHealth(true, true, true, true, true, place);
        }

        public static StoreHealth Assess(StoreFacts? facts)
        {
            if (facts == null)
            {
                return new StoreHealth(false, false, false, false, false);
            }

            var open = !facts.TemporarilyClosed;
            var stocked = facts.NeededItems.Count == 0
                || FixtureCargoProof(facts.NeededItems, facts.CargoOnHand);
            var staffed = facts.RegisterStaffed
                && facts.CleanerStaffed
                && facts.StaffSatisfaction >= StoreHealthGate.SatisfactionFloor;
            var selling = !facts.ScheduledOpen
                || (facts.DailyCustomers > 0 && facts.DailyRevenue > 0m);
            var profitable = ProfitableProof(facts.RecentOpenDayProfits);
            return new StoreHealth(open, stocked, staffed, selling, profitable, facts.Place);
        }

        public static bool FixtureCargoProof(
            IReadOnlyList<string>? neededItems,
            IReadOnlyDictionary<string, int>? cargoOnHand)
        {
            if (neededItems == null || neededItems.Count == 0 || cargoOnHand == null)
            {
                return false;
            }

            foreach (var item in neededItems)
            {
                if (string.IsNullOrEmpty(item))
                {
                    continue;
                }

                if (!cargoOnHand.TryGetValue(item, out var count) || count <= 0)
                {
                    return false;
                }
            }

            return true;
        }

        public static IReadOnlyDictionary<string, int> CountCargoAmounts(IEnumerable<CargoAmount>? rows)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            if (rows == null)
            {
                return counts;
            }

            foreach (var row in rows)
            {
                if (row == null || string.IsNullOrEmpty(row.Item) || row.Amount <= 0)
                {
                    continue;
                }

                counts.TryGetValue(row.Item, out var prior);
                counts[row.Item] = prior + row.Amount;
            }

            return counts;
        }

        public static string Line(StoreHealth? health)
        {
            if (health == null)
            {
                return "store.health version=" + LoadLog.Version
                    + " place= g1=false g2=false g3=false g4=false g5=false ready=false";
            }

            return "store.health version=" + LoadLog.Version
                + " place=" + health.Place
                + " g1=" + Flag(health.Open)
                + " g2=" + Flag(health.Stocked)
                + " g3=" + Flag(health.Staffed)
                + " g4=" + Flag(health.Selling)
                + " g5=" + Flag(health.Profitable)
                + " ready=" + Flag(health.Ready);
        }

        public static string Record(string localAppData, StoreHealth? health)
        {
            var line = Line(health);
            var key = (localAppData ?? "") + "\n" + line;
            if (key == _seen)
            {
                return line;
            }

            _seen = key;
            var path = LoadLog.CeoLogPath(localAppData ?? "");
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(path, line + Environment.NewLine);
            return line;
        }

        private static bool ProfitableProof(IReadOnlyList<decimal> openDayProfits)
        {
            if (openDayProfits == null || openDayProfits.Count == 0)
            {
                return false;
            }

            if (openDayProfits[openDayProfits.Count - 1] <= 0m)
            {
                return false;
            }

            var take = Math.Min(StoreHealthGate.ProfitProofDays, openDayProfits.Count);
            var sum = 0m;
            for (var index = openDayProfits.Count - take; index < openDayProfits.Count; index++)
            {
                sum += openDayProfits[index];
            }

            return sum > 0m;
        }

        private static string Flag(bool value)
        {
            return value ? "true" : "false";
        }
    }

    public sealed class StoreHealPick
    {
        public StoreHealPick(string action, bool chosen)
        {
            Action = action ?? "";
            Chosen = chosen;
        }

        public string Action { get; }

        public bool Chosen { get; }

        public static StoreHealPick None()
        {
            return new StoreHealPick("", false);
        }
    }

    public static class StoreHeal
    {
        public static StoreHealPick Decide(StoreHealth? health)
        {
            if (health == null)
            {
                return StoreHealPick.None();
            }

            if (!health.Open)
            {
                return new StoreHealPick("reopen", true);
            }

            if (!health.Stocked)
            {
                return new StoreHealPick("restock", true);
            }

            if (!health.Staffed)
            {
                return new StoreHealPick("restaff", true);
            }

            if (!health.Selling)
            {
                return new StoreHealPick("prices", true);
            }

            if (!health.Profitable)
            {
                return new StoreHealPick("hold", true);
            }

            return StoreHealPick.None();
        }
    }

    public static class StoreHealthGate
    {
        public const int ProfitProofDays = 3;

        public const float SatisfactionFloor = 50f;

        public static bool ExpansionAllowed(IReadOnlyList<StoreHealth>? owned)
        {
            if (owned == null || owned.Count == 0)
            {
                return true;
            }

            foreach (var store in owned)
            {
                if (store == null || !store.Ready)
                {
                    return false;
                }
            }

            return true;
        }
    }
}

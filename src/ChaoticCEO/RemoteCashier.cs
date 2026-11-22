using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ChaoticCEO
{
    public sealed class ServiceRegister
    {
        public ServiceRegister(string? id, string? skill)
        {
            Id = Text(id);
            Skill = Text(skill);
        }

        public string Id { get; }

        public string Skill { get; }

        private static string Text(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? "" : value.Trim();
        }
    }

    public sealed class PostedShift
    {
        public PostedShift(int dayIndex, string? employeeId, string? itemId, bool orphan = false)
        {
            DayIndex = dayIndex;
            EmployeeId = string.IsNullOrWhiteSpace(employeeId) ? "" : employeeId.Trim();
            ItemId = string.IsNullOrWhiteSpace(itemId) ? "" : itemId.Trim();
            Orphan = orphan;
        }

        public int DayIndex { get; }

        public string EmployeeId { get; }

        public string ItemId { get; }

        public bool Orphan { get; }
    }

    public sealed class RemoteCashierPlan
    {
        public RemoteCashierPlan(
            bool applied,
            string reason,
            string skill,
            IReadOnlyList<string> registerIds,
            IReadOnlyList<int> shiftDays,
            int shifts,
            int closedDays,
            int purged,
            IReadOnlyList<PostedShift> stale,
            bool hire,
            string employeeId)
        {
            Applied = applied;
            Reason = reason ?? "";
            Skill = skill ?? "";
            RegisterIds = registerIds ?? new string[0];
            ShiftDays = shiftDays ?? new int[0];
            Shifts = shifts;
            ClosedDays = closedDays;
            Purged = purged;
            Stale = stale ?? new PostedShift[0];
            Hire = hire;
            EmployeeId = employeeId ?? "";
        }

        public bool Applied { get; }

        public string Reason { get; }

        public string Skill { get; }

        public IReadOnlyList<string> RegisterIds { get; }

        public IReadOnlyList<int> ShiftDays { get; }

        public int Shifts { get; }

        public int ClosedDays { get; }

        public int Purged { get; }

        public IReadOnlyList<PostedShift> Stale { get; }

        public bool Hire { get; }

        public string EmployeeId { get; }
    }

    public static class RemoteCashier
    {
        public const int StartHour = 8;

        public const int EndHour = 16;

        public static RemoteCashierPlan Decide(
            bool inScene,
            CashierPick? pick,
            IEnumerable<ServiceRegister>? registers,
            IReadOnlyList<bool>? openDays,
            IEnumerable<PostedShift>? posted,
            IEnumerable<string>? liveEmployeeIds)
        {
            if (inScene)
            {
                return Miss("inscene");
            }

            if (pick == null || !pick.Hired)
            {
                return Miss("empty");
            }

            var registerIds = Registers(registers);
            if (registerIds.Count == 0)
            {
                return Miss("nostation");
            }

            if (openDays == null || openDays.Count == 0)
            {
                return Miss("nodays");
            }

            var shiftDays = new List<int>();
            var closed = 0;
            for (var day = 0; day < openDays.Count; day++)
            {
                if (openDays[day])
                {
                    shiftDays.Add(day);
                }
                else
                {
                    closed++;
                }
            }

            if (shiftDays.Count == 0)
            {
                return Miss("closed");
            }

            var live = Live(liveEmployeeIds);
            var service = new HashSet<string>(registerIds, StringComparer.Ordinal);
            var stale = new List<PostedShift>();
            var kept = "";
            if (posted != null)
            {
                foreach (var shift in posted)
                {
                    if (shift == null)
                    {
                        continue;
                    }

                    var onRegister = service.Contains(shift.ItemId);
                    if (shift.Orphan || (onRegister && StaleShift(shift, openDays, live)))
                    {
                        stale.Add(shift);
                        continue;
                    }

                    if (onRegister && live.Contains(shift.EmployeeId))
                    {
                        kept = shift.EmployeeId;
                    }
                }
            }

            return new RemoteCashierPlan(
                true,
                "",
                Cashier.Skill,
                registerIds,
                shiftDays,
                shiftDays.Count * registerIds.Count,
                closed,
                stale.Count,
                stale,
                kept.Length == 0,
                kept);
        }

        public static bool Recruit(bool storeWantsHire, int cashiersAtStore)
        {
            return storeWantsHire && cashiersAtStore <= 0;
        }

        public static IReadOnlyList<StaffShift> Shifts(RemoteCashierPlan? plan, string? employeeId)
        {
            var who = Text(employeeId);
            if (plan == null || !plan.Applied || who.Length == 0 || plan.RegisterIds.Count == 0 || plan.ShiftDays.Count == 0)
            {
                return new StaffShift[0];
            }

            return new[]
            {
                new StaffShift(who, plan.RegisterIds[0], StaffShift.Register),
            };
        }

        public static string Open(
            string? localAppData,
            BuildingRent? rent,
            bool entered,
            RemoteLayoutResult? laid,
            string? opsLine,
            StarterPick? pick,
            int day,
            IEnumerable<ServiceRegister>? registers,
            IReadOnlyList<bool>? openDays,
            IEnumerable<PostedShift>? posted,
            IEnumerable<string>? liveEmployeeIds)
        {
            if (entered || rent == null || !rent.Rented || laid == null || !laid.Applied)
            {
                return "";
            }

            if (opsLine == null || opsLine.IndexOf("ops.assigned ", StringComparison.Ordinal) < 0)
            {
                return "";
            }

            var api = GameApi.Live();
            var signed = Wholesaler.Sign(rent, pick, Wholesaler.StageOne(), alreadySigned: false, api);
            var hired = Cashier.Hire(rent, pick, signed, alreadyHired: false, api);
            if (!hired.Hired)
            {
                return "";
            }

            var plan = Decide(false, hired, registers, openDays, posted, liveEmployeeIds);
            var folder = localAppData ?? "";
            if (!plan.Applied)
            {
                return Cashier.Miss(folder, hired, day, plan.Reason);
            }

            var employee = plan.EmployeeId.Length > 0 ? plan.EmployeeId : plan.RegisterIds[0];
            if (!plan.Hire)
            {
                return Note(folder, rent, day, plan, employee);
            }

            var recorded = Cashier.Record(folder, hired, day);
            Note(folder, rent, day, plan, employee);
            return recorded;
        }

        public static string Note(string? localAppData, BuildingRent? rent, int day, RemoteCashierPlan? plan, string? employeeId)
        {
            if (rent == null || !rent.Rented || plan == null || !plan.Applied || plan.RegisterIds.Count == 0)
            {
                return "";
            }

            var who = Text(employeeId);
            if (who.Length == 0)
            {
                who = plan.RegisterIds[0];
            }

            var line = "cashier.shift version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " street=" + rent.Street
                + " number=" + rent.Number.ToString(CultureInfo.InvariantCulture)
                + " skill=" + plan.Skill
                + " register=" + plan.RegisterIds[0]
                + " employee=" + who
                + " shifts=" + plan.Shifts.ToString(CultureInfo.InvariantCulture)
                + " purged=" + plan.Purged.ToString(CultureInfo.InvariantCulture)
                + " closed=" + plan.ClosedDays.ToString(CultureInfo.InvariantCulture)
                + " person=false";
            return Append(localAppData ?? "", line);
        }

        private static List<string> Registers(IEnumerable<ServiceRegister>? registers)
        {
            var ids = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            if (registers == null)
            {
                return ids;
            }

            foreach (var register in registers)
            {
                if (register == null || register.Id.Length == 0 || register.Skill != Cashier.Skill || !seen.Add(register.Id))
                {
                    continue;
                }

                ids.Add(register.Id);
            }

            return ids;
        }

        private static HashSet<string> Live(IEnumerable<string>? liveEmployeeIds)
        {
            var live = new HashSet<string>(StringComparer.Ordinal);
            if (liveEmployeeIds == null)
            {
                return live;
            }

            foreach (var id in liveEmployeeIds)
            {
                var who = Text(id);
                if (who.Length > 0)
                {
                    live.Add(who);
                }
            }

            return live;
        }

        private static bool StaleShift(PostedShift shift, IReadOnlyList<bool> openDays, HashSet<string> live)
        {
            if (shift.EmployeeId.Length == 0
                || shift.DayIndex < 0
                || shift.DayIndex >= openDays.Count
                || !openDays[shift.DayIndex])
            {
                return true;
            }

            return !live.Contains(shift.EmployeeId);
        }

        private static RemoteCashierPlan Miss(string reason)
        {
            return new RemoteCashierPlan(false, reason, "", new string[0], new int[0], 0, 0, 0, new PostedShift[0], false, "");
        }

        private static string Text(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? "" : value.Trim();
        }

        private static string Append(string localAppData, string line)
        {
            var path = LoadLog.CeoLogPath(localAppData ?? "");
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(path, line + Environment.NewLine);
            CeoLedger.Ingest(line);
            return line;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ChaoticCEO
{
    public sealed class HqSeat
    {
        public HqSeat(string role, bool filled, string employee = "")
        {
            Role = role ?? "";
            Filled = filled;
            Employee = employee ?? "";
        }

        public string Role { get; }

        public bool Filled { get; }

        public string Employee { get; }
    }

    public sealed class HiredHand
    {
        public HiredHand(string id, string skill, bool candidate = false)
        {
            Id = id ?? "";
            Skill = skill ?? "";
            Candidate = candidate;
        }

        public string Id { get; }

        public string Skill { get; }

        public bool Candidate { get; }
    }

    public sealed class PlanMove
    {
        public PlanMove(string kind, string role, string employee, string call)
        {
            Kind = kind ?? "";
            Role = role ?? "";
            Employee = employee ?? "";
            Call = call ?? "";
        }

        public string Kind { get; }

        public string Role { get; }

        public string Employee { get; }

        public string Call { get; }
    }

    public static class HqPlans
    {
        public const string LogisticsSkill = "ba:skill_logisticsmanager";

        public const string HrSkill = "ba:skill_hrmanager";

        public const string PurchasingSkill = "ba:skill_purchasingagent";

        public const string HeadhunterSkill = "ba:skill_headhunter";

        private static readonly string[] Roles = { "logistics", "hr", "purchasing", "headhunter" };

        public static IReadOnlyList<PlanMove> Assign(
            ApiNote? api,
            bool pricingPlan,
            IReadOnlyList<HqSeat>? seats,
            IReadOnlyList<HiredHand>? hired)
        {
            var create = api == null ? "" : api.Call("pricing plan create");
            var assign = api == null ? "" : api.Call("plan assign");
            var moves = new List<PlanMove>();
            if (!pricingPlan && create.Length > 0)
            {
                moves.Add(new PlanMove("create", "", "", create));
            }

            if (assign.Length == 0)
            {
                return moves;
            }

            var roster = seats ?? Array.Empty<HqSeat>();
            var hands = hired ?? Array.Empty<HiredHand>();
            var taken = new HashSet<string>();
            TakeFilled(roster, taken);
            foreach (var role in Roles)
            {
                if (Filled(roster, role))
                {
                    continue;
                }

                var employee = Pick(hands, Skill(role), taken);
                if (employee.Length == 0)
                {
                    continue;
                }

                moves.Add(new PlanMove("assign", role, employee, assign));
            }

            return moves;
        }

        public static string Line(int day, PlanMove move)
        {
            var version = "version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture);
            if (move.Kind == "create")
            {
                return "plan.created " + version + " create=" + move.Call;
            }

            return "plan.assigned " + version
                + " role=" + move.Role
                + " employee=" + move.Employee
                + " assign=" + move.Call;
        }

        public static string Summary(int day, int created, int assigned)
        {
            return "plan.summary version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " created=" + created.ToString(CultureInfo.InvariantCulture)
                + " assigned=" + assigned.ToString(CultureInfo.InvariantCulture);
        }

        private static void TakeFilled(IReadOnlyList<HqSeat> seats, HashSet<string> taken)
        {
            foreach (var seat in seats)
            {
                if (seat != null && seat.Filled && seat.Employee.Length > 0)
                {
                    taken.Add(seat.Employee);
                }
            }
        }

        private static bool Filled(IReadOnlyList<HqSeat> seats, string role)
        {
            foreach (var seat in seats)
            {
                if (seat != null && seat.Role == role && seat.Filled)
                {
                    return true;
                }
            }

            return false;
        }

        private static string Pick(IReadOnlyList<HiredHand> hired, string skill, HashSet<string> taken)
        {
            var ids = new List<string>();
            foreach (var hand in hired)
            {
                if (hand == null
                    || hand.Candidate
                    || hand.Skill != skill
                    || hand.Id.Length == 0
                    || taken.Contains(hand.Id)
                    || ids.Contains(hand.Id))
                {
                    continue;
                }

                ids.Add(hand.Id);
            }

            if (ids.Count == 0)
            {
                return "";
            }

            ids.Sort(StringComparer.Ordinal);
            taken.Add(ids[0]);
            return ids[0];
        }

        private static string Skill(string role)
        {
            switch (role)
            {
                case "logistics":
                    return LogisticsSkill;
                case "hr":
                    return HrSkill;
                case "purchasing":
                    return PurchasingSkill;
                case "headhunter":
                    return HeadhunterSkill;
                default:
                    return "";
            }
        }
    }

    public sealed class PlanWatch
    {
        private object? _seen;

        public IReadOnlyList<string> Tick(
            string localAppData,
            object? save,
            int day,
            ApiNote? api,
            bool pricingPlan,
            IReadOnlyList<HqSeat>? seats,
            IReadOnlyList<HiredHand>? hired)
        {
            if (save == null || ReferenceEquals(_seen, save))
            {
                return Array.Empty<string>();
            }

            _seen = save;
            var moves = HqPlans.Assign(api, pricingPlan, seats, hired);
            var lines = new List<string>();
            if (moves.Count == 0)
            {
                lines.Add(HqPlans.Summary(day, 0, 0));
            }
            else
            {
                foreach (var move in moves)
                {
                    lines.Add(HqPlans.Line(day, move));
                }
            }

            var path = LoadLog.CeoLogPath(localAppData ?? "");
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(path, string.Join(Environment.NewLine, lines) + Environment.NewLine);
            return lines;
        }
    }
}

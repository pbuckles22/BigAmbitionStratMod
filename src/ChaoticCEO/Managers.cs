using System;
using System.Collections.Generic;

namespace ChaoticCEO
{
    public sealed class ManagerSeat
    {
        public ManagerSeat(string role, string warehouse, bool assigned)
        {
            Role = role ?? "";
            Warehouse = warehouse ?? "";
            Assigned = assigned;
        }

        public string Role { get; }

        public string Warehouse { get; }

        public bool Assigned { get; }
    }

    public sealed class ManagerPick
    {
        public ManagerPick(string role, string action, string warehouse, bool chosen)
        {
            Role = role ?? "";
            Action = action ?? "";
            Warehouse = warehouse ?? "";
            Chosen = chosen;
        }

        public string Role { get; }

        public string Action { get; }

        public string Warehouse { get; }

        public bool Chosen { get; }

        public static ManagerPick None()
        {
            return new ManagerPick("", "", "", false);
        }
    }

    public static class Managers
    {
        public const int HrSpan = 25;

        public const int HuntSpan = 5;

        public static ManagerPick Decide(
            int stage,
            IReadOnlyList<string>? warehouses,
            int employees,
            int openRoles,
            IReadOnlyList<ManagerSeat>? seats,
            bool pricingPlanExists)
        {
            if (stage < 2 || seats == null)
            {
                return ManagerPick.None();
            }

            if (!pricingPlanExists)
            {
                return new ManagerPick("pricing", "plan", "", true);
            }

            var hubs = new List<string>();
            if (warehouses != null)
            {
                foreach (var warehouse in warehouses)
                {
                    if (!string.IsNullOrEmpty(warehouse))
                    {
                        hubs.Add(warehouse);
                    }
                }
            }

            foreach (var warehouse in hubs)
            {
                if (!HasSeat(seats, "logistics", warehouse))
                {
                    return new ManagerPick("logistics", "hire", warehouse, true);
                }
            }

            foreach (var warehouse in hubs)
            {
                if (!HasSeat(seats, "purchasing", warehouse))
                {
                    return new ManagerPick("purchasing", "hire", warehouse, true);
                }
            }

            var hrTarget = TargetCount(employees, HrSpan);
            if (CountSeats(seats, "hr") < hrTarget)
            {
                return new ManagerPick("hr", "hire", "", true);
            }

            var huntTarget = TargetCount(openRoles, HuntSpan);
            if (CountSeats(seats, "headhunter") < huntTarget)
            {
                return new ManagerPick("headhunter", "hire", "", true);
            }

            return ManagerPick.None();
        }

        private static int TargetCount(int units, int span)
        {
            if (units <= 0 || span <= 0)
            {
                return 1;
            }

            return Math.Max(1, (units + span - 1) / span);
        }

        private static bool HasSeat(IReadOnlyList<ManagerSeat> seats, string role, string warehouse)
        {
            foreach (var seat in seats)
            {
                if (seat != null
                    && seat.Role == role
                    && seat.Warehouse == warehouse
                    && seat.Assigned)
                {
                    return true;
                }
            }

            return false;
        }

        private static int CountSeats(IReadOnlyList<ManagerSeat> seats, string role)
        {
            var count = 0;
            foreach (var seat in seats)
            {
                if (seat != null && seat.Role == role && seat.Assigned)
                {
                    count++;
                }
            }

            return count;
        }
    }
}

using System.Collections.Generic;
using Entities;
using Entities.Employee.JobDemands;
using Helpers;

namespace ChaoticCEO
{
    public static class StaffDemandBridge
    {
        public static bool Live(DemandPick? pick)
        {
            if (pick == null || !pick.Cleared || pick.EmployeeId.Length == 0)
            {
                return false;
            }

            try
            {
                var employee = EmployeeHelper.GetEmployeeById(pick.EmployeeId, showError: false);
                if (employee == null || !employee.CanGiveBonus())
                {
                    return false;
                }

                return employee.GiveBonus();
            }
            catch (System.Exception ex)
            {
                CrashTrace.Seen(ex, "StaffDemandBridge.Live.1", "");
                return false;
            }
        }

        public static IReadOnlyList<(string employeeId, string demand, int unmetDays)> Due()
        {
            var due = new List<(string, string, int)>();
            var current = SaveGameManager.Current;
            if (current == null)
            {
                return due;
            }

            try
            {
                foreach (var employee in EmployeeHelper.GetEmployeeInstances())
                {
                    if (employee?.demands == null || string.IsNullOrEmpty(employee.id))
                    {
                        continue;
                    }

                    if (!employee.CanGiveBonus())
                    {
                        continue;
                    }

                    foreach (var demand in employee.demands)
                    {
                        if (string.IsNullOrEmpty(demand))
                        {
                            continue;
                        }

                        var job = JobDemandHelper.GetByName(demand);
                        if (job == null || job.Fulfilled(employee, null))
                        {
                            continue;
                        }

                        // Opening clears an unfulfilled demand once the game allows a bonus.
                        // The pure StaffDemand.Clear lock still requires unmetDays >= DemandDays.
                        due.Add((employee.id, demand, StaffDemand.DemandDays));
                        break;
                    }
                }
            }
            catch (System.Exception ex)
            {
                CrashTrace.Seen(ex, "StaffDemandBridge.Due.1", "");
                return due;
            }

            return due;
        }
    }
}

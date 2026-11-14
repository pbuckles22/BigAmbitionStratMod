using System.Reflection;
using Entities;

namespace ChaoticCEO
{
    public static class StaffHoldBridge
    {
        private static readonly FieldInfo? PoachDeadline = typeof(EmployeeInstance)
            .GetField("poachDeadlineDays", BindingFlags.NonPublic | BindingFlags.Instance);

        public static void Hold(StaffHoldWatch watch, string localAppData)
        {
            var current = SaveGameManager.Current;
            if (!StaffHold.Enabled || watch == null || current?.EmployeeInstances == null || current.gameVariables == null)
            {
                return;
            }

            if (!watch.Due(current.Day, current.Hour))
            {
                return;
            }

            var year = current.gameVariables.daysPerYear;
            int employees = 0, raised = 0, aged = 0, complaints = 0, poach = 0, sick = 0;
            foreach (var employee in current.EmployeeInstances)
            {
                if (employee == null || employee.IsCandidate || employee.characterData == null)
                {
                    continue;
                }

                employees++;
                var deadline = PoachDeadline?.GetValue(employee) is int days ? days : 0;
                var complaint = employee.complaintData;
                var state = new StaffState(
                    employee.satisfaction,
                    employee.characterData.ageInDays,
                    complaint != null && complaint.isComplaining,
                    complaint == null ? int.MaxValue : complaint.hoursUntilNextComplaint,
                    deadline > 0,
                    employee.nextSickDay,
                    employee.isAbsent);
                var plan = StaffHold.Decide(state, year);
                if (!plan.Any)
                {
                    continue;
                }

                if (plan.Raise)
                {
                    employee.satisfaction = StaffHold.Satisfaction;
                    raised++;
                }

                if (plan.Age >= 0)
                {
                    employee.characterData.ageInDays = plan.Age;
                    aged++;
                }

                if (complaint != null && plan.ClearComplaint)
                {
                    complaint.isComplaining = false;
                    complaint.complaintDeadlineHours = 0;
                    complaints++;
                }

                if (complaint != null && plan.ComplaintHours >= 0)
                {
                    complaint.hoursUntilNextComplaint = plan.ComplaintHours;
                }

                if (plan.ClearPoach && PoachDeadline != null)
                {
                    PoachDeadline.SetValue(employee, 0);
                    employee.poachedByRivalId = string.Empty;
                    poach++;
                }

                if (plan.SickDay >= 0)
                {
                    employee.nextSickDay = plan.SickDay;
                }

                if (plan.ClearAbsent)
                {
                    employee.isAbsent = false;
                }

                if (plan.SickDay >= 0 || plan.ClearAbsent)
                {
                    sick++;
                }
            }

            watch.Tick(localAppData, current.Day, current.Hour, employees, raised, aged, complaints, poach, sick);
        }
    }
}

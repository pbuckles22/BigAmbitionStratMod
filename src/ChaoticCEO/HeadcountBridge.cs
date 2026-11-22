using System.Collections.Generic;
using Entities;
using Helpers;

namespace ChaoticCEO
{
    public sealed class HeadcountWatch
    {
        public int Day { get; private set; }

        public bool Due(int day)
        {
            return day > 0 && day != Day;
        }

        public void Mark(int day)
        {
            Day = day;
        }
    }

    public static class HeadcountBridge
    {
        public static string? Hold(HeadcountWatch watch, string localAppData)
        {
            if (watch == null || string.IsNullOrEmpty(localAppData) || !LiveDump.InCity())
            {
                return null;
            }

            var current = SaveGameManager.Current;
            if (current == null || !watch.Due(current.Day))
            {
                return null;
            }

            var roster = Read(current);
            var day = Headcount.Open(current.Day, watch.Day);
            var cashier = Headcount.Cashier(day, roster);
            var cleaner = Headcount.Cleaner(cashier, roster);
            if (day > 0)
            {
                var planned = Headcount.Extra(cashier, cleaner).Count;
                var removed = Release(current, cashier, cleaner);
                if (removed < planned)
                {
                    Miss(localAppData, day, planned - removed);
                }
            }

            watch.Mark(current.Day);
            return Headcount.Record(localAppData, cashier, cleaner);
        }

        public static bool Blocks(string skill)
        {
            var role = Role(skill);
            return role != null && !role.Hire;
        }

        public static EmployeeInstance? Find(string skill)
        {
            var role = Role(skill);
            var current = SaveGameManager.Current;
            if (role == null || role.Keep.Length == 0 || current?.EmployeeInstances == null)
            {
                return null;
            }

            foreach (var employee in current.EmployeeInstances)
            {
                if (employee != null && employee.id == role.Keep)
                {
                    return employee;
                }
            }

            return null;
        }

        private static HeadcountRole? Role(string skill)
        {
            try
            {
                var current = SaveGameManager.Current;
                if (current == null)
                {
                    return null;
                }

                var roster = Read(current);
                var cashier = Headcount.Cashier(1, roster);
                if (skill == Cleaner.Skill)
                {
                    return Headcount.Cleaner(cashier, roster);
                }

                if (skill == Cashier.Skill)
                {
                    return cashier;
                }
            }
            catch (System.Exception ex)
            {
                CrashTrace.Seen(ex, "HeadcountBridge.Role.1", "");
                return null;
            }

            return null;
        }

        private static int Release(GameInstance current, HeadcountRole cashier, HeadcountRole cleaner)
        {
            var removed = 0;
            if (current.EmployeeInstances == null)
            {
                return removed;
            }

            foreach (var id in Headcount.Extra(cashier, cleaner))
            {
                EmployeeInstance? match = null;
                foreach (var employee in current.EmployeeInstances)
                {
                    if (employee != null && employee.id == id)
                    {
                        match = employee;
                        break;
                    }
                }

                if (match == null)
                {
                    continue;
                }

                try
                {
                    match.RemoveEmployee();
                    removed++;
                }
                catch (System.Exception ex)
                {
                    CrashTrace.Seen(ex, "HeadcountBridge.Release.1", "");
                    try
                    {
                        current.EmployeeInstances.Remove(match);
                        removed++;
                    }
                    catch (System.Exception inner)
                    {
                        CrashTrace.Seen(inner, "HeadcountBridge.Release.2", "");
                    }
                }
            }

            return removed;
        }

        private static void Miss(string localAppData, int day, int left)
        {
            var line = "headcount.missed version=" + LoadLog.Version
                + " day=" + day.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + " left=" + left.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var path = LoadLog.CeoLogPath(localAppData);
            if (System.IO.File.Exists(path))
            {
                foreach (var existing in System.IO.File.ReadAllLines(path))
                {
                    if (existing == line)
                    {
                        return;
                    }
                }
            }

            var directory = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
            }

            System.IO.File.AppendAllText(path, line + System.Environment.NewLine);
        }

        private static List<HeadcountSeat> Read(GameInstance current)
        {
            var seats = new List<HeadcountSeat>();
            if (current.EmployeeInstances == null)
            {
                return seats;
            }

            foreach (var employee in current.EmployeeInstances)
            {
                if (employee == null || string.IsNullOrEmpty(employee.id))
                {
                    continue;
                }

                var scheduled = false;
                try
                {
                    scheduled = employee.IsAssignedToAnyWorkShift();
                }
                catch (System.Exception ex)
                {
                    CrashTrace.Seen(ex, "HeadcountBridge.Read.1", "");
                    scheduled = false;
                }

                var hourly = (decimal)employee.hourlyWage;
                try
                {
                    var building = Building(employee);
                    if (employee.HasSkill(Cashier.Skill))
                    {
                        seats.Add(new HeadcountSeat(employee.id, Cashier.Skill, hourly, scheduled, building));
                    }

                    if (employee.HasSkill(Cleaner.Skill))
                    {
                        seats.Add(new HeadcountSeat(employee.id, Cleaner.Skill, hourly, scheduled, building));
                    }
                }
                catch (System.Exception ex)
                {
                    CrashTrace.Seen(ex, "HeadcountBridge.Read.2", "");
                }
            }

            return seats;
        }

        private static string Building(EmployeeInstance employee)
        {
            try
            {
                var address = employee.assignedAddress;
                if (address == null)
                {
                    return "";
                }

                var street = address.streetName ?? "";
                if (street.Length == 0 || address.streetNumber <= 0)
                {
                    return "";
                }

                return street + " " + address.streetNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            catch (System.Exception ex)
            {
                CrashTrace.Seen(ex, "HeadcountBridge.Building.1", "");
                return "";
            }
        }
    }
}
